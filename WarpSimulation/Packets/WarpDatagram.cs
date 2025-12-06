using Raylib_cs;

namespace WarpSimulation.Packets;

public class WarpDatagram : Datagram
{
    public List<WarpNode> Path { get; set; }

    public int CurrentHopIndex { get; set; } = 0;

    public WarpDatagram(
        WarpNode source,
        WarpNode destination,
        List<WarpNode> path,
        byte[] payload)
        : base(source, destination, payload)
    {
        Path = path;
    }

    public override int Size => base.Size + (Path.Count * 4) + 4;
}
