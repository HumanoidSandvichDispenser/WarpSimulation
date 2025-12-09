using Raylib_cs;

namespace WarpSimulation.Packets;

/// <summary>
/// A datagram packet for the WARP protocol that includes a predefined path
/// of WARP nodes to traverse.
/// </summary>
public class WarpDatagram : Datagram
{
    public List<WarpNode> Path { get; set; }

    public int CurrentHopIndex { get; set; } = 0;

    public WarpDatagram(
        WarpNode source,
        WarpNode destination,
        List<WarpNode> path,
        IPacket? payload)
        : base(source, destination, payload)
    {
        Path = path;
    }

    public override int HeaderSize => base.HeaderSize + 4 + (Path.Count * 4);
}
