namespace WarpSimulation.TransportLayer;

public class TcpSegment : Packets.IPacket
{
    public WarpNode Source { get; set; }

    public WarpNode Destination { get; set; }

    public uint SequenceNumber { get; set; }

    public uint AcknowledgmentNumber { get; set; }

    public byte[] Payload { get; set; } = Array.Empty<byte>();

    public bool IsAck { get; set; }

    public TcpSegment(WarpNode source, WarpNode destination, byte[] payload)
    {
        Source = source;
        Destination = destination;
        Payload = payload;
    }

    public virtual int HeaderSize => 20;

    public int PayloadSize => Payload.Length;

    public int Size => HeaderSize + PayloadSize;
}
