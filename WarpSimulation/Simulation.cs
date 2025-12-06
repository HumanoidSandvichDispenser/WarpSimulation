using System.Numerics;
using System.Text.Json;

namespace WarpSimulation;

using DijkstraResult = UndirectedWeightedGraph<WarpNode, Link>.DijkstraResult;

public class Simulation
{
    private static Simulation? _instance = null;

    public static Simulation Instance => _instance ??= new();

    public WarpNetworkGraph NetworkGraph { get; private set; } = new();

    private HashSet<Packets.PhysicalPacket> _packetsInTransit = new();

    public Queue<Packets.PhysicalPacket> AddPacketQueue { get; } = new();

    public Queue<Packets.PhysicalPacket> RemovePacketQueue { get; } = new();

    public Simulation()
    {

    }

    public void Update(float delta)
    {
        while (AddPacketQueue.Count > 0)
        {
            var packet = AddPacketQueue.Dequeue();
            _packetsInTransit.Add(packet);
        }

        foreach (var packet in _packetsInTransit)
        {
            packet.Update(delta);
        }

        NetworkGraph.Update(delta);

        while (RemovePacketQueue.Count > 0)
        {
            var packet = RemovePacketQueue.Dequeue();
            _packetsInTransit.Remove(packet);
        }
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
            case "send":
                if (args.Length < 4)
                {
                    WriteOutput("Usage: send <source> <destination> <message length>");
                    break;
                }
                SendMessage(args[0], args[1], args[2], args[3]);
                break;
            case "load":
                if (args.Length == 0)
                {
                    WriteOutput("Usage: load <json file>");
                    break;
                }
                LoadFromJsonFile(System.IO.File.ReadAllText(args[0]));
                break;
            case "topk":
                if (args.Length < 2)
                {
                    WriteOutput("Usage: topk <source> <k>");
                    break;
                }
                SetTopK(args[0], args[1]);
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

        foreach (var packet in _packetsInTransit)
        {
            packet.Draw();
        }
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

        foreach (var node in NetworkGraph.Vertices)
        {
            node.Database.UpdateDatabaseFromGraph(NetworkGraph);
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

    public void SendMessage(string startName, string endName, string messageLength, string count)
    {
        var start = NetworkGraph.Vertices
            .FirstOrDefault(v => v.Name == startName);
        var end = NetworkGraph.Vertices
            .FirstOrDefault(v => v.Name == endName);

        int length = int.Parse(messageLength);
        int numMessages = int.Parse(count);

        if (start is null || end is null)
        {
            WriteOutput("Invalid start or end node.");
            return;
        }

        for (int i = 0; i < numMessages; i++)
        {
            var datagram = new Packets.Datagram(start, end, new byte[length]);
            start.ReceiveDatagram(datagram);
        }

        WriteOutput($"Sending message from {startName} to {endName} of size {length} bytes.");
    }

    public void SetTopK(string nodeName, string kStr)
    {
        var node = NetworkGraph.Vertices
            .FirstOrDefault(v => v.Name == nodeName);

        if (node is null)
        {
            WriteOutput("Invalid node.");
            return;
        }

        if (!int.TryParse(kStr, out int k) || k <= 0)
        {
            WriteOutput("Invalid value for k.");
            return;
        }

        node.Database.TopK = k;
        WriteOutput($"Node {nodeName} set to use top {k} paths for routing.");
    }
}
