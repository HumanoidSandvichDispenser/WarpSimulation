using System.Numerics;
using Raylib_cs;

namespace WarpSimulation.Packets;

/// <summary>
/// Represents a L2/L1 packet in the WARP network, encapsulating
/// a datagram.
/// </summary>
public class PhysicalPacket : IPacket, IUpdateable, IDrawable
{
    public WarpNode StartNode { get; set; } = null!;

    public WarpNode EndNode { get; set; } = null!;

    public float TransmissionProgress { get; set; } = 0.0f;

    public float TransmissionTime { get; set; } = 0.0f;

    public Datagram Datagram { get; set; }

    public int HeaderSize => 0;

    public int PayloadSize => Datagram.Size;

    public int Size => HeaderSize + PayloadSize;

    public event Action<PhysicalPacket>? OnTransmissionComplete;

    public PhysicalPacket(
        WarpNode start,
        WarpNode end,
        Datagram datagram)
    {
        StartNode = start;
        EndNode = end;
        Datagram = datagram;
    }

    public void Update(float deltaTime)
    {
        // update transmission progress and check for completion, invoking
        // event if done (which will be handled by the Link class)
        TransmissionProgress += deltaTime;
        if (TransmissionProgress >= TransmissionTime)
        {
            TransmissionProgress = TransmissionTime;
            OnTransmissionComplete?.Invoke(this);
        }
    }

    public void Draw()
    {
        Vector2 start = StartNode.Position;
        Vector2 end = EndNode.Position;

        float lerpAmount = TransmissionProgress / TransmissionTime;

        Vector2 lerp = Vector2.Lerp(start, end, lerpAmount);
        Raylib.DrawPoly(lerp, 4, 8.0f, 0, Color.Red);
    }
}
