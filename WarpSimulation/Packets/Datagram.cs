using Raylib_cs;
using WarpSimulation.TransportLayer;

namespace WarpSimulation.Packets;

/// <summary>
/// A datagram packet that contains source and destination information,
/// as well as an optional payload.
/// </summary>
public class Datagram : IPacket
{
    public WarpNode Source { get; set; }

    public WarpNode? Destination { get; set; }

    public IPacket? Payload { get; set; }

    public Datagram(WarpNode source, WarpNode? destination, IPacket? payload = null)
    {
        Source = source;
        Destination = destination;
        Payload = payload;
    }

    public virtual int HeaderSize => 4 + 4;

    public int PayloadSize => Payload?.Size ?? 0;

    public int Size => HeaderSize + PayloadSize;
}
