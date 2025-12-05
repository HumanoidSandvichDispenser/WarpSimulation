using System.Numerics;
using System.Text.Json;

namespace WarpSimulation;

using DijkstraResult = UndirectedWeightedGraph<WarpNode, Link>.DijkstraResult;

public class Simulation
{
    private static Simulation? _instance = null;

    public static Simulation Instance => _instance ??= new();

    public WarpNetworkGraph NetworkGraph { get; private set; } = new();

    public Simulation()
    {

    }

    public void Update(float delta)
    {

    }

    public void ProcessCommand(string command, string[] args)
    {
        switch (command)
        {
            case "dumpdb":
                foreach (var node in NetworkGraph.Vertices)
                {
                    if (args.Length == 0 || node.Name == args[0])
                    {
                        WriteOutput(node.DumpDatabase());
                    }
                }
                break;
            case "watch":
                if (args.Length == 0)
                {
                    WriteOutput("Usage: watch <node name>");
                    break;
                }
                WatchNode(args[0]);
                break;
            default:
                WriteOutput($"Unknown command: {command}");
                break;
        }
    }

    public void WriteOutput(string text)
    {
        Program.WriteOutput(text);
    }

    public void Draw()
    {
        NetworkGraph.Draw();
    }

    public void LoadFromJsonFile(string jsonString)
    {
        var jsonDoc = JsonDocument.Parse(jsonString);
        var root = jsonDoc.RootElement;

        var nodesDict = root.GetProperty("nodes");
        var linksArray = root.GetProperty("links");

        foreach (var nodeKv in nodesDict.EnumerateObject())
        {
            string nodeName = nodeKv.Name;
            int x = nodeKv.Value.GetProperty("x").GetInt32();
            int y = nodeKv.Value.GetProperty("y").GetInt32();

            var node = new WarpNode(nodeName, new Vector2(x, y));
            NetworkGraph.AddVertex(node);
        }

        foreach (var linkElem in linksArray.EnumerateArray())
        {
            var vertices = linkElem.GetProperty("vertices").EnumerateArray()
                .Select(v => v.GetString())
                .ToArray();

            var bandwidth = linkElem.GetProperty("bandwidth").GetDouble();

            var node1 = NetworkGraph.Vertices
                .First(v => v.Name == vertices[0]);

            var node2 = NetworkGraph.Vertices
                .First(v => v.Name == vertices[1]);

            NetworkGraph.AddEdge(node1, node2, new Link(bandwidth));
        }

        if (root.TryGetProperty("drawShortestPath", out var drawShortestPath))
        {
            Console.WriteLine("Drawing shortest paths...");

            var vertices = drawShortestPath
                .EnumerateArray()
                .Select(v => v.GetString())
                .Select(v => NetworkGraph.Vertices.First(vertex => vertex.Name == v))
                .ToArray();

            vertices[0].Database.UpdateDatabaseFromGraph(NetworkGraph);
            NetworkGraph.DebugDrawShortestPath(vertices[0], vertices[1]);
        }
    }

    public void WatchNode(string nodeName)
    {
        var node = NetworkGraph.Vertices
            .FirstOrDefault(v => v.Name == nodeName);

        if (node != null)
        {
            node.OnPathAccepted += OnPathAccepted;
            node.OnPathPruned += OnPathPruned;
            WriteOutput($"Watching node '{nodeName}'.");
        }
        else
        {
            WriteOutput($"Node '{nodeName}' not found.");
        }
    }

    private void OnPathAccepted(WarpNode node, DijkstraResult result)
    {
        WriteOutput($"Node {node.Name} accepted path: " +
            $"{string.Join(" -> ", result.Path.Select(n => n.Name))} " +
            $"(Cost: {result.TotalWeight:0.##})");
    }

    private void OnPathPruned(WarpNode node, DijkstraResult result)
    {
        WriteOutput($"Node {node.Name} pruned path: " +
            $"{string.Join(" -> ", result.Path.Select(n => n.Name))} " +
            $"(Cost: {result.TotalWeight:0.##})");
    }
}
