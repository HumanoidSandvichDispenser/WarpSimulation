namespace WarpSimulation.Packets;

public interface IPacket
{
    /// <summary>
    /// Size of the packet in bytes.
    /// </summary>
    public int HeaderSize { get; }

    /// <summary>
    /// Size of the payload in bytes.
    /// </summary>
    public int PayloadSize { get; }

    /// <summary>
    /// Size of the packet in bytes.
    /// </summary>
    public int Size { get; }
}
