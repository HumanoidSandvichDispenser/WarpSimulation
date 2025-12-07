using System.Numerics;
using System.Text.Json;

namespace WarpSimulation;

using DijkstraResult = UndirectedWeightedGraph<WarpNode, Link>.DijkstraResult;

public class Simulation
{
    private static Simulation? _instance = null;

    public static Simulation Instance => _instance ??= new();

    public WarpNetworkGraph NetworkGraph { get; private set; } = new();

    private HashSet<IUpdateable> _updateables = new();

    public Queue<IUpdateable> AddUpdateableQueue { get; } = new();

    public Queue<IUpdateable> RemoveUpdateableQueue { get; } = new();

    /// <summary>
    /// A multiplier affecting the rate of traffic generation. Higher values
    /// result in more traffic.
    /// </summary>
    public double TrafficDensity { get; set; } = 1.0;

    public Simulation()
    {

    }

    public void Update(float delta)
    {
        while (AddUpdateableQueue.Count > 0)
        {
            var packet = AddUpdateableQueue.Dequeue();
            _updateables.Add(packet);
        }

        foreach (var packet in _updateables)
        {
            packet.Update(delta);
        }

        NetworkGraph.Update(delta);

        while (RemoveUpdateableQueue.Count > 0)
        {
            var packet = RemoveUpdateableQueue.Dequeue();
            _updateables.Remove(packet);
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
                if (args.Length < 3)
                {
                    WriteOutput("Usage: send <source> <destination> <message length>");
                    break;
                }
                SendMessage(args[0], args[1], args[2]);
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

        foreach (var packet in _updateables)
        {
            if (packet is IDrawable drawable)
            {
                drawable.Draw();
            }
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

            bool fullDuplex = true;

            if (linkElem.TryGetProperty("fullDuplex", out var fullDuplexElem))
            {
                fullDuplex = fullDuplexElem.GetBoolean();
            }

            var node1 = NetworkGraph.Vertices
                .First(v => v.Name == vertices[0]);

            var node2 = NetworkGraph.Vertices
                .First(v => v.Name == vertices[1]);

            var link = new Link(bandwidth);

            if (!fullDuplex)
            {
                link.FullDuplex = false;
            }

            NetworkGraph.AddEdge(node1, node2, link);
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

    public void SendMessage(string startName, string endName, string messageLength)
    {
        var start = NetworkGraph.Vertices
            .FirstOrDefault(v => v.Name == startName);
        var end = NetworkGraph.Vertices
            .FirstOrDefault(v => v.Name == endName);

        int length = int.Parse(messageLength);

        if (start is null || end is null)
        {
            WriteOutput("Invalid start or end node.");
            return;
        }

        TransportLayer.TcpSession sender = new(start);
        TransportLayer.TcpSession receiver = new(end);
        sender.PeerSession = receiver;
        receiver.PeerSession = sender;

        sender.OnAllDataReceived += (data, time) =>
        {
            WriteOutput($"Message of size {data.Length} bytes " +
                $"received by {endName} from {startName} " +
                $"in {time:0.##} seconds.");
        };

        receiver.OnDataReceived += (data) =>
        {
            WriteOutput($"Node {endName} received {data.Length} bytes of data.");
        };

        AddUpdateableQueue.Enqueue(sender);
        AddUpdateableQueue.Enqueue(receiver);

        sender.SendData(new byte[length]);

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
