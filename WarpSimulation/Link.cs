using System.Numerics;
using Raylib_cs;

namespace WarpSimulation;

public class Link : IEdge, IEdgeWithEndpoints<WarpNode>
{
    private double _weight = 0;

    public double Weight => _weight;

    public double EffectiveBandwidth => CalculateEffectiveBandwidth();

    public bool IsMarked { get; set; }

    /// <summary>
    /// The link's bandwidth in bits per second.
    /// </summary>
    public double Bandwidth { get; set; }

    private double _failureRate = 0.0;

    public EdgeDrawInfo DrawInfo { get; set; } = new();

    /// <summary>
    /// The link's failure rate, a value between 0.0 and 1.0.
    /// /// </summary>
    public double FailureRate
    {
        get => _failureRate;
        set
        {
            _failureRate = Math.Clamp(value, 0.0, 1.0);
            CalculateEffectiveBandwidth();
        }
    }

    public WarpNode[] Vertices { get; set; } = new WarpNode[2];

    public Link(double bandwidth = 4096)
    {
        Bandwidth = bandwidth;
        CalculateEffectiveBandwidth();
    }

    public double CalculateEffectiveBandwidth()
    {
        double effectiveBandwidth = Bandwidth * (1.0 - FailureRate);
        _weight = 1.0 / effectiveBandwidth;
        return effectiveBandwidth;
    }

    public void Draw()
    {
        var v1 = Vertices[0].Position;
        var v2 = Vertices[1].Position;
        var color = new Color(224, 224, 224);

        if (DrawInfo.Rank.Count > 0)
        {
            // Highlighted path
            float t = (float)DrawInfo.Rank[0] / 2.5f;
            color = Raylib.ColorLerp(Color.Blue, Color.LightGray, t);
        }

        float thickness = (float)(Bandwidth / 1024.0);

        Raylib.DrawLineEx(v1, v2, thickness, color);

        Vector2 center = (v1 + v2) / 2;

        const int fontSize = 20;
        string text = $"{EffectiveBandwidth / 1024.0:0.##}";
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
