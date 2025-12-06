using Raylib_cs;

namespace WarpSimulation.Packets;

public class Datagram : IPacket
{
    public WarpNode Source { get; set; }

    public WarpNode Destination { get; set; }

    public byte[] Payload { get; set; }

    public Datagram(WarpNode source, WarpNode destination, byte[] payload)
    {
        Source = source;
        Destination = destination;
        Payload = payload;
    }

    public virtual int Size => 4 + 4 + Payload.Length;
}
