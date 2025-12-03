using System.Numerics;
using System.Text.Json;

namespace WarpSimulation;

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

            var paths = vertices[0].KPathSelection(vertices[1], 8);

            foreach (var (path, i) in paths.Select((p, index) => (p, index)))
            {
                Console.WriteLine($"Adding rank {i} to path: " +
                    string.Join(" -> ", path.Path.Select(v => v.Name)));
                var vertexList = path.Path.ToList();
                for (int j = 0; j < vertexList.Count - 1; j++)
                {
                    var edge = NetworkGraph.GetEdge(vertexList[j], vertexList[j + 1]);
                    if (edge != null)
                    {
                        edge.DrawInfo.Rank.Add(i);
                    }
                }
            }
        }
    }
}
