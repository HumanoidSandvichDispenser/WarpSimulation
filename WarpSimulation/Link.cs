using System.Numerics;
using Raylib_cs;

namespace WarpSimulation;

public class Link : IEdge, IEdgeWithEndpoints<WarpNode>
{
    private double _weight = 0;

    public double Weight => _weight;

    /// <summary>
    /// The link's effective bandwidth in bits per second, accounting for
    /// failure rate, processing delay, and other factors. This is the
    /// bandwidth used for routing calculations.
    /// </summary>
    public double EffectiveBandwidth => CalculateEffectiveBandwidth();

    /// <summary>
    /// The link's bandwidth in bits per second. This is the raw bandwidth
    /// used for the actual transmission time.
    /// </summary>
    public double Bandwidth { get; set; }

    public EdgeDrawInfo DrawInfo { get; set; } = new();

    public bool FullDuplex { get; set; } = true;

    public WarpNode[] Vertices { get; set; } = new WarpNode[2];

    public Packets.PhysicalPacket?[] Transit { get; } = new Packets.PhysicalPacket?[2];

    public Link(double bandwidth = 4096)
    {
        Bandwidth = bandwidth;
        CalculateEffectiveBandwidth();
    }

    public double CalculateEffectiveBandwidth()
    {
        double effectiveBandwidth = Bandwidth;

        if (!FullDuplex)
        {
            effectiveBandwidth *= 0.5;
        }

        double loss1 = Vertices[0]?.ByteLossRate ?? 0;
        double loss2 = Vertices[1]?.ByteLossRate ?? 0;

        effectiveBandwidth *= (1.0 - loss1) * (1.0 - loss2);

        if (effectiveBandwidth == 0)
        {
            _weight = double.PositiveInfinity;
        }
        else
        {
            _weight = 1.0 / effectiveBandwidth;
        }

        return effectiveBandwidth;
    }

    public int GetNodeIndex(WarpNode node)
    {
        if (Vertices[0] == node)
        {
            return 0;
        }
        else if (Vertices[1] == node)
        {
            return 1;
        }
        else
        {
            throw new ArgumentException("The provided node is not connected by this link.");
        }
    }

    public WarpNode GetOtherNode(WarpNode node)
    {
        int otherIndex = GetNodeIndex(node) == 0 ? 1 : 0;
        return Vertices[otherIndex];
    }

    public bool TransmitPacket(Packets.PhysicalPacket packet)
    {
        int index = GetNodeIndex(packet.StartNode);
        float transmissionTime = (float)(packet.Size * 8 / EffectiveBandwidth);

        if (Transit[index] is not null)
        {
            return false;
        }
        else if (!FullDuplex)
        {
            int otherIndex = 1 - index;
            if (Transit[otherIndex] is not null)
            {
                return false;
            }
        }

        var simulation = Simulation.Instance;
        simulation.AddUpdateableQueue.Enqueue(packet);
        packet.TransmissionTime = transmissionTime;
        packet.OnTransmissionComplete += OnPacketTransmissionComplete;

        Transit[index] = packet;
        return true;
    }

    private void OnPacketTransmissionComplete(Packets.PhysicalPacket packet)
    {
        packet.OnTransmissionComplete -= OnPacketTransmissionComplete;

        int index = GetNodeIndex(packet.StartNode);
        Transit[index] = null;

        var simulation = Simulation.Instance;
        simulation.RemoveUpdateableQueue.Enqueue(packet);
        packet.EndNode.ReceiveDatagram(packet.Datagram);
    }

    public bool Equals(IEdge? other)
    {
        if (other is Link otherLink)
        {
            return (Vertices[0] == otherLink.Vertices[0] &&
                    Vertices[1] == otherLink.Vertices[1]) ||
                   (Vertices[0] == otherLink.Vertices[1] &&
                    Vertices[1] == otherLink.Vertices[0]);
        }
        return false;
    }

    public void Update(float delta)
    {
        if (FullDuplex)
        {
            // try to transmit packets in both directions
            if (Transit[0] is null)
            {
                TryDequeueNode(0);
            }

            if (Transit[1] is null)
            {
                TryDequeueNode(1);
            }
        }
        else
        {
            // random access for half-duplex links
            Random rand = new();
            int direction = rand.Next(0, 2);
            int otherDirection = 1 - direction;

            if (!TryDequeueNode(direction))
            {
                TryDequeueNode(otherDirection);
            }
        }
    }

    private bool TryDequeueNode(int index)
    {
        WarpNode node = Vertices[index];
        if (node.PacketQueue.ContainsKey(this))
        {
            var queue = node.PacketQueue[this];
            if (queue.Count > 0)
            {
                var packet = queue.Peek();
                if (TransmitPacket(packet))
                {
                    queue.Dequeue();
                    return true;
                }
            }
        }
        return false;
    }

    public void Draw()
    {
        var v1 = Vertices[0].Position;
        var v2 = Vertices[1].Position;
        var color = new Color(200, 200, 200);

        if (Transit[0] is not null || Transit[1] is not null)
        {
            color = new Color(128, 128, 128);
        }

        float thickness = (float)(Bandwidth / 65536);

        if (FullDuplex)
        {
            Raylib.DrawLineEx(v1, v2, thickness, color);
        }
        else
        {
            // draw as a dotted line for half-duplex links
            float segmentDiameter = thickness;
            Vector2 direction = Vector2.Normalize(v2 - v1);
            float totalLength = Vector2.Distance(v1, v2);
            int segmentCount = (int)(totalLength / segmentDiameter);
            for (int i = 0; i < segmentCount; i += 2)
            {
                // draw circles
                Vector2 segmentCenter = v1 + direction * (i + 0.5f) * segmentDiameter;
                Raylib.DrawCircleV(segmentCenter, segmentDiameter / 2, color);
            }
        }

        Vector2 center = (v1 + v2) / 2;

        const int fontSize = 20;
        string text = $"{Bandwidth / 1024.0:0.##}";
        int width = Raylib.MeasureText(text, fontSize);
        Vector2 textPos = new Vector2(
            center.X - width / 2,
            center.Y - fontSize / 2);
        Raylib.DrawText(text, (int)textPos.X, (int)textPos.Y, fontSize, Color.Black);
    }

    public int CompareTo(object? obj)
    {
        if (obj is IEdge other)
        {
            return Weight.CompareTo(other.Weight);
        }

        throw new ArgumentException($"Object is not a {nameof(IEdge)}");
    }

    /// <summary>
    /// Creates a copy of this link.
    /// </summary>
    public Link Clone()
    {
        return new Link(Bandwidth)
        {
            FullDuplex = this.FullDuplex,
        };
    }
}

public struct EdgeDrawInfo
{
    /// <summary>
    /// If this edge is part of a highlighted path, this will indicate the
    /// rank of the path (0 for shortest path, 1 for second shortest, etc.)
    /// </summary>
    public List<int> Rank = [];

    public int MaxRank = 5;

    public EdgeDrawInfo()
    {

    }
}
