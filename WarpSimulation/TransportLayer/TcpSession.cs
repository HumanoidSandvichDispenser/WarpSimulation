namespace WarpSimulation.TransportLayer;

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

    public int SendWindowSize { get; set; } = 1;

    private Queue<byte[]> _sendQueue = new();

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

    private float _rto = 1.0f;
    private const int MSS = 1460; // Maximum Segment Size
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
            }
        }

        foreach (var seqNum in toRemove)
        {
            _unacked.Remove(seqNum);
        }

        // update send base
        if (ackNumber > SendBase)
        {
            SendBase = ackNumber;
        }

        // implement congestion control (simple additive increase)
        if (_unacked.Count == 0)
        {
            SendWindowSize = Math.Min(SendWindowSize + 1, 64); // Cap at 64
        }

        // check for completion
        if (_unacked.Count == 0 && _sendQueue.Count == 0 && _started)
        {
            _started = false;
            OnAllDataReceived?.Invoke(Array.Empty<byte>(), ElapsedTime);
            ElapsedTime = 0.0f;
        }

        // now that we have more space in the window, try sending more data
        TrySendQueuedData();
    }

    public void Update(float delta)
    {
        if (_started)
        {
            ElapsedTime += delta;
        }

        // update timers for unacknowledged segments
        var keysToCheck = new List<uint>(_unacked.Keys);

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
                SendWindowSize = Math.Max(1, SendWindowSize / 2);
                _rto = Math.Min(_rto * 2, 60.0f);
            }

            _unacked[seqNum] = entry;
        }
    }
}
