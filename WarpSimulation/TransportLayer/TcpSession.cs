namespace WarpSimulation.TransportLayer;

/// <summary>
/// A simplified TCP session implementation using TCP Reno congestion control
/// algorithm. This implementation omits details related to TCP such as
/// connection establishment or teardown, focusing solely on data transfer.
/// However, it does use 30 second initial RTO to simulate handshake timeout,
/// and this RTO is replaced once RTT measurements are made.
/// </summary>
public class TcpSession : IUpdateable
{
    public WarpNode Node { get; set; }
    public TcpSession? PeerSession { get; set; }

    #region Sender State

    /// <summary>
    /// The lowest unacknowledged sequence number.
    /// </summary>
    public uint SendBase { get; set; }

    public uint NextSeqNum { get; set; }

    private Queue<byte[]> _sendQueue = new();

    /// <summary>
    /// Congestion window timer, used to increase the congestion window per
    /// RTT.
    /// </summary>
    public float CwndTimer { get; set; } = 0.0f;

    public int SendWindowSize { get; set; } = 1;

    public int SlowStartThreshold { get; set; } = int.MaxValue;

    private int _dupAckCount = 0;

    private uint _lastAckedSeqNum = uint.MaxValue;

    #endregion

    #region Receiver State

    public uint ExpectedSeqNum { get; set; }

    private Dictionary<uint, TcpSegment> _receiveBuffer = new();

    private Dictionary<uint, (TcpSegment Segment, float Timer)> _unacked = new();

    public List<byte> ReceivedData { get; private set; } = new();

    #endregion

    /// <summary>
    /// Total elapsed time from the start of the data transfer to completion.
    /// </summary>
    public float ElapsedTime { get; private set; } = 0.0f;

    private float _rto = 30.0f;
    private float? _rtt = null;
    private float _devRtt = 0;
    private const int MSS = 1460; // Maximum Segment Size
    private const float alpha = 0.125f;
    private const float beta = 0.25f;
    private bool _started = false;

    public event Action<byte[]>? OnDataReceived;

    public event Action<byte[], float>? OnAllDataReceived;

    public TcpSession(WarpNode node)
    {
        Node = node;

        node.OnDatagramReceived += (_, datagram) =>
        {
            if (datagram.Payload is TcpSegment segment)
            {
                ReceiveSegment(segment);
            }
        };
    }

    public void SendData(byte[] data)
    {
        _started = true;

        // break data into segments of size MSS
        int offset = 0;

        while (offset < data.Length)
        {
            int chunkSize = Math.Min(MSS, data.Length - offset);
            byte[] chunk = new byte[chunkSize];
            Array.Copy(data, offset, chunk, 0, chunkSize);

            _sendQueue.Enqueue(chunk);
            offset += chunkSize;
        }

        TrySendQueuedData();
    }

    private void TrySendQueuedData()
    {
        // send as many segments as the window allows
        while (_sendQueue.Count > 0 && _unacked.Count < SendWindowSize)
        {
            byte[] chunk = _sendQueue.Dequeue();

            var segment = new TcpSegment(Node, PeerSession!.Node, chunk)
            {
                SequenceNumber = NextSeqNum
            };

            SendSegment(segment);

            // track unacknowledged segment
            _unacked[NextSeqNum] = (segment, 0.0f);

            NextSeqNum++;
        }
    }

    public void SendSegment(TcpSegment segment)
    {
        if (PeerSession == null)
        {
            throw new InvalidOperationException("Peer session is not set.");
        }

        var datagram = new Packets.Datagram(
            Node,
            PeerSession.Node,
            segment);

        Node.ReceiveDatagram(datagram);
    }

    public void ReceiveSegment(TcpSegment segment)
    {
        // check for ACK segment
        if (segment.IsAck)
        {
            HandleAck(segment.AcknowledgmentNumber);
            return;
        }

        if (segment.SequenceNumber == ExpectedSeqNum)
        {
            // if data segment was received in order
            ProcessInOrderSegment(segment);

            // check if any buffered segments can now be processed
            while (_receiveBuffer.ContainsKey(ExpectedSeqNum))
            {
                var bufferedSegment = _receiveBuffer[ExpectedSeqNum];
                ProcessInOrderSegment(bufferedSegment);
                _receiveBuffer.Remove(ExpectedSeqNum - 1);
            }
        }
        else if (segment.SequenceNumber > ExpectedSeqNum)
        {
            // buffer out-of-order segment
            _receiveBuffer[segment.SequenceNumber] = segment;

            // send DUPACK for expected sequence number
            SendAck(ExpectedSeqNum);
        }
        else
        {
            // duplicate segment, resend ACK
            SendAck(ExpectedSeqNum);
        }
    }

    private void ProcessInOrderSegment(TcpSegment segment)
    {
        // deliver data to application
        ReceivedData.AddRange(segment.Payload);
        OnDataReceived?.Invoke(segment.Payload);

        ExpectedSeqNum++;
        SendAck(ExpectedSeqNum);
    }

    private void SendAck(uint ackNumber)
    {
        var ackSegment = new TcpSegment(Node, PeerSession!.Node, Array.Empty<byte>())
        {
            AcknowledgmentNumber = ackNumber,
            IsAck = true
        };

        SendSegment(ackSegment);
    }

    private void HandleAck(uint ackNumber)
    {
        // remove all acknowledged segments
        var toRemove = new List<uint>();

        foreach (var seqNum in _unacked.Keys)
        {
            if (seqNum < ackNumber)
            {
                toRemove.Add(seqNum);

                // update RTT estimate
                float sampleRtt = _unacked[seqNum].Timer;
                if (_rtt == null)
                {
                    _rtt = sampleRtt;
                }
                else
                {
                    _rtt = 0.875f * _rtt + 0.125f * sampleRtt;
                    float deviation = Math.Abs(sampleRtt - _rtt.Value);
                    _devRtt = (1 - beta) * _devRtt + beta * deviation;
                }
                _rto = _rtt.Value + 4 * _devRtt;
            }
        }

        foreach (var seqNum in toRemove)
        {
            _unacked.Remove(seqNum);
        }

        if (ackNumber == _lastAckedSeqNum)
        {
            if (++_dupAckCount >= 3)
            {
                // triple duplicate ACKs: fast retransmit
                if (_unacked.ContainsKey(ackNumber))
                {
                    var entry = _unacked[ackNumber];
                    SendSegment(entry.Segment);
                    entry.Timer = 0.0f;
                    _unacked[ackNumber] = entry;

                    // congestion control: on triple duplicate ACKs, halve the
                    // window and enter congestion avoidance

                    SlowStartThreshold = Math.Max(SendWindowSize / 2, 2);
                    SendWindowSize = SlowStartThreshold;

                    Console.WriteLine($"Triple duplicate ACKs for segment {ackNumber}, " +
                        $"reducing window to {SendWindowSize}");
                }
            }
        }
        else
        {
            _dupAckCount = 1;
            _lastAckedSeqNum = ackNumber;
        }

        // update send base
        if (ackNumber > SendBase)
        {
            SendBase = ackNumber;
        }

        // draw a line of all segments, number for acked segment, hash for unacked
        // and empty for not yet sent
        System.Text.StringBuilder sb = new();
        for (int i = 0; i < SendBase; i++)
        {
            sb.Append($"{i.ToString("000")} ");
        }

        foreach (var seqNum in _unacked.Keys)
        {
            sb.Append("### ");
        }

        int totalSegments = (int)NextSeqNum + _sendQueue.Count;

        for (long i = ackNumber + _unacked.Count; i < totalSegments; i++)
        {
            sb.Append("... ");
        }

        //Simulation.Instance.WriteOutput(sb.ToString());

        // now that we have more space in the window, try sending more data
        TrySendQueuedData();

        // check for completion: if ACK number acknowledges all data
        if (SendBase >= NextSeqNum)
        {
            OnAllDataReceived?.Invoke(ReceivedData.ToArray(), ElapsedTime);
        }
    }

    public void Update(float delta)
    {
        if (_started)
        {
            ElapsedTime += delta;
        }

        // update timers for unacknowledged segments
        var keysToCheck = new List<uint>(_unacked.Keys);

        if (_rtt != null && CwndTimer >= _rtt)
        {
            // increase congestion window every RTT
            if (SendWindowSize < SlowStartThreshold)
            {
                // slow start phase: double the window
                SendWindowSize *= 2;
            }
            else
            {
                // congestion avoidance phase: increase by 1 MSS
                SendWindowSize += 1;
            }
            CwndTimer = 0.0f;
        }
        else
        {
            CwndTimer += delta;
        }

        foreach (var seqNum in keysToCheck)
        {
            var entry = _unacked[seqNum];
            entry.Timer += delta;

            if (entry.Timer >= _rto)
            {
                // retransmit segment
                SendSegment(entry.Segment);
                entry.Timer = 0.0f;

                // congestion control: on timeout, halve the window and double
                // RTO
                SlowStartThreshold = Math.Max(SendWindowSize / 2, 2);
                SendWindowSize = 1;
                Console.WriteLine($"Timeout on segment {seqNum}, " +
                    $"reducing window to {SendWindowSize}");
            }

            _unacked[seqNum] = entry;
        }
    }
}
