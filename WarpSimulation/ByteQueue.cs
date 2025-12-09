namespace WarpSimulation;

public class ByteQueue
{
    private Queue<Packets.PhysicalPacket> _queue = new();

    /// <summary>
    /// The total number of bytes in this queue.
    /// </summary>
    public int TotalSize { get; private set; } = 0;

    /// <summary>
    /// The maximum number of bytes that can be stored in this queue
    /// before dropping packets.
    /// </summary>
    public int Capacity { get; private set; } = 65536;

    /// <summary>
    /// The total number of packets in the queue.
    /// </summary>
    public int Count => _queue.Count;

    public double QueueRatio => (double)TotalSize / Capacity;

    public ByteQueue(int capacity)
    {
        Capacity = capacity;
    }

    public bool TryEnqueue(Packets.PhysicalPacket packet)
    {
        int newSize = packet.Size + TotalSize;

        if (newSize <= Capacity)
        {
            _queue.Enqueue(packet);
            TotalSize = newSize;
            return true;
        }
        return false;
    }

    public Packets.PhysicalPacket Peek()
    {
        return _queue.Peek();
    }

    public Packets.PhysicalPacket Dequeue()
    {
        var packet = _queue.Dequeue();
        TotalSize -= packet.Size;
        return packet;
    }
}
