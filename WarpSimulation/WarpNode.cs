using System.Numerics;
using Raylib_cs;

namespace WarpSimulation;

using DijkstraResult = UndirectedWeightedGraph<WarpNode, Link>.DijkstraResult;

/// <summary>
/// Represents a node in the WARP network. Each node maintains its own
/// local database of network information and can perform path selection
/// based on that information. This class also contains properties relating
/// to the simulation, such as the node's position and name.
/// </summary>
public class WarpNode
{
    public string Name { get; set; }

    public Vector2 Position { get; set; }

    /// <summary>
    /// The node's local database of network information, including known nodes,
    /// links, and their attributes.
    /// </summary>
    public WarpDatabase Database { get; private set; } = new();

    /// <summary>
    /// Indicates whether routing decisions are made deterministically.
    /// If <c>true</c>, the node will always select absolute shortest path,
    /// which is functionally equivalent to OSPF. If <c>false</c>, the node may
    /// distribute traffic across multiple paths based on path weights.
    /// </summary>
    public bool IsDeterministic { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="WarpNode"/> class.
    /// </summary>
    /// <param name="name">The name of the node.</param>
    /// <param name="position">The position of the node in the simulation.</param>
    public WarpNode(string name = "", Vector2? position = null)
    {
        Name = name;
        Position = position ?? Vector2.Zero;
    }

    /// <summary>
    /// A modification of Yen's Algorithm to support WARP multi-path selection
    /// with path filtering based on link attributes to generate diverse paths.
    /// THe number of paths returned is within the range of 0 to
    /// <paramref name="k"/>.
    /// </summary>
    public IEnumerable<DijkstraResult> KPathSelection(WarpNode destination, int k)
    {
        Console.WriteLine($"Node {Name} performing K-Path Selection to {destination.Name} with k={k}");
        // get the k shortest paths
        var shortestPaths = Database.LocalGraph
            .YensAlgorithm(this, destination)
            .Take(k);

        // contains current usage and capacity for each link
        Dictionary<Link, double> usage = new();
        Dictionary<Link, double> capacity = new();

        // get capacity for each link using the local database
        foreach (var (link, linkRecord) in Database.LinkRecords)
        {
            usage[link] = 0.0;
            capacity[link] = linkRecord.EffectiveBandwidth;
        }

        foreach (var (nextPath, index) in shortestPaths.Select((path, idx) => (path, idx)))
        {
            // the shortest path algorithm returns a list of vertices rather
            // than a list of edges, so here we convert the list of vertices
            // to a list of edges
            var nextPathList = nextPath.Path.ToList();
            var edges = nextPath.Path
                .Zip(nextPath.Path.Skip(1), (a, b) => Database.LocalGraph.GetEdge(a, b)!);

            // minimum available bandwidth is the computed bottleneck along
            // this path, i.e. the edge with the least available bandwidth
            double minAvailBandwidth = edges
                .Min(edge => capacity[edge] - usage[edge]);

            if (index == 0)
            {
                // this is shortest path

                // reserve bandwidth along this path
                foreach (var edge in edges)
                {
                    usage[edge] += minAvailBandwidth;
                }

                yield return nextPath;
            }
            else
            {
                if (minAvailBandwidth <= 0)
                {
                    // no more bandwidth available on some edge in this path
                    continue;
                }

                // path returned by next iteration of Yen's algorithm

                // if all edges have enough available bandwidth, reserve
                // the bandwidth along this path
                if (edges.All(edge => capacity[edge] - usage[edge] >= minAvailBandwidth))
                {
                    foreach (var edge in edges)
                    {
                        usage[edge] += minAvailBandwidth;
                    }

                    yield return nextPath;
                }
            }
        }
    }

    public void Draw()
    {
        Raylib.DrawCircleV(Position, 16.0f, Color.Blue);
        const int fontSize = 20;
        int width = Raylib.MeasureText(Name, fontSize);
        Vector2 textPos = new Vector2(
            Position.X - width / 2,
            Position.Y - fontSize / 2);
        Raylib.DrawText(Name, (int)textPos.X, (int)textPos.Y, fontSize, Color.White);
    }
}
