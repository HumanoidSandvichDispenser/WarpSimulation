namespace WarpSimulation.Packets;

/// <summary>
/// A simple representation of a TCP segment. This class does not include
/// any TCP header fields for simplicity of the simulation.
/// </summary>
public class TcpSegment : IPacket
{
    public int Size => 20;

    public uint SequenceNumber { get; set; }

    public uint AcknowledgmentNumber { get; set; }

    public byte[] Payload { get; set; }
}
