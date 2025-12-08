using System.CommandLine;
using System.CommandLine.Parsing;
using System.Numerics;
using System.Text.Json;
using Raylib_cs;

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

    public bool IsPaused { get; set; } = false;

    public WarpNetworkGraph? ViewingGraph { get; set; } = null;

    public bool PopulateDatabasesOnLoad { get; set; } = false;

    public uint FrameCount { get; set; } = 0;

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
        FrameCount++;

        // check if just pressed space to pause/unpause
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            IsPaused = !IsPaused;
            WriteOutput(IsPaused ? "Simulation paused." : "Simulation unpaused.");
        }

        if (IsPaused)
        {
            return;
        }

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
                SendMessage(args);
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
            case "toggle":
                if (args.Length == 0)
                {
                    WriteOutput("Usage: toggle <node name>");
                    break;
                }
                ToggleNode(args[0]);
                break;
            case "screenshot":
                if (!Raylib.IsWindowReady())
                {
                    break;
                }
                string filename = args.Length > 0 ? args[0] : $"screenshot_{FrameCount}.png";
                Raylib.TakeScreenshot(filename);
                WriteOutput($"Screenshot saved to {filename}");
                break;
            case "view":
                if (args.Length == 0)
                {
                    ViewingGraph = null;
                    WriteOutput("Reset to viewing full network graph. Use 'view <node name>' to view a specific node's database graph.");
                }
                else
                {
                    ViewNodeLocalGraph(args[0]);
                }
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
        if (ViewingGraph is not null)
        {
            ViewingGraph.Draw();
        }
        else
        {
            NetworkGraph.Draw();
        }

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

        if (PopulateDatabasesOnLoad)
        {
            foreach (var node in NetworkGraph.Vertices)
            {
                node.Database.UpdateDatabaseFromGraph(NetworkGraph);
            }
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

    private WarpNode? ParseNodeName(ArgumentResult result)
    {
        var token = result.Tokens.FirstOrDefault();
        if (token is null)
        {
            result.AddError("No node name provided");
            return null;
        }
        var nodeName = token.Value;
        var node = NetworkGraph.Vertices
            .FirstOrDefault(v => v.Name == nodeName);
        if (node is null)
        {
            result.AddError($"Node '{nodeName}' not found");
            return null;
        }
        return node;
    }

    public void SendMessage(string[] args)
    {
        Argument<WarpNode> startNode = new("start")
        {
            CustomParser = ParseNodeName,
            Arity = ArgumentArity.ExactlyOne,
        };
        Argument<WarpNode> endNode = new("end")
        {
            CustomParser = ParseNodeName,
            Arity = ArgumentArity.ExactlyOne,
        };
        Argument<int> messageLengthArg = new("messageLength")
        {
            Arity = ArgumentArity.ExactlyOne,
        };
        Option<bool> quitOnTransmit = new("--quit-on-transmit")
        {
            Description = "Quit the simulation once all messages have been transmitted.",
            DefaultValueFactory = _ => false,
        };

        var rootCommand = new RootCommand("send")
        {
            startNode,
            endNode,
            messageLengthArg,
            quitOnTransmit,
        };

        var parseResult = rootCommand.Parse(args);

        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                WriteOutput($"Error: {error.Message}");
            }
            return;
        }

        WarpNode start = parseResult.GetValue(startNode)!;
        WarpNode end = parseResult.GetValue(endNode)!;
        int length = parseResult.GetValue(messageLengthArg)!;

        TransportLayer.TcpSession sender = new(start);
        TransportLayer.TcpSession receiver = new(end);

        sender.PeerSession = receiver;
        receiver.PeerSession = sender;

        sender.OnAllDataReceived += (_, time) =>
        {
            WriteOutput($"Message of size {length} bytes " +
                $"received by {end.Name} from {start.Name} " +
                $"in {time:0.####} seconds.");

            if (parseResult.GetValue(quitOnTransmit))
            {
                WriteOutput("All messages transmitted. Quitting simulation.");
                System.Environment.Exit(0);
            }
        };

        AddUpdateableQueue.Enqueue(sender);
        AddUpdateableQueue.Enqueue(receiver);

        sender.SendData(new byte[length]);

        WriteOutput($"Sending message from {start.Name} to {end.Name} of size {length} bytes.");
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

    public void ToggleNode(string nodeName)
    {
        var node = NetworkGraph.Vertices
            .FirstOrDefault(v => v.Name == nodeName);

        if (node is null)
        {
            WriteOutput("Invalid node.");
            return;
        }

        node.IsActive = !node.IsActive;

        WriteOutput($"Node {nodeName} is now {(node.IsActive ? "active" : "inactive")}.");
    }

    public void ViewNodeLocalGraph(string nodeName)
    {
        var node = NetworkGraph.Vertices
            .FirstOrDefault(v => v.Name == nodeName);
        if (node is null)
        {
            WriteOutput("Invalid node.");
            return;
        }
        ViewingGraph = node.Database.LocalGraph;
        WriteOutput($"Now viewing local graph of node {nodeName}.");
    }
}
