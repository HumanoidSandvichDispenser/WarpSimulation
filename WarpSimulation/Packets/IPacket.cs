namespace WarpSimulation.Packets;

public interface IPacket
{
    /// <summary>
    /// Size of the packet in bytes.
    /// </summary>
    public int Size { get; }
}
