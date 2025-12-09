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

    /// <summary>
    /// The singleton instance of this class.
    /// </summary>
    public static Simulation Instance => _instance ??= new();

    /// <summary>
    /// The actual network topology of the simulation.
    /// </summary>
    public WarpNetworkGraph NetworkGraph { get; private set; } = new();

    private HashSet<IUpdateable> _updateables = new();

    /// <summary>
    /// Queue to add IUpdateable objects on the next frame.
    /// </summary>
    public Queue<IUpdateable> AddUpdateableQueue { get; } = new();

    /// <summary>
    /// Queue to remove IUpdateable objects on the next frame.
    /// </summary>
    public Queue<IUpdateable> RemoveUpdateableQueue { get; } = new();

    public bool IsPaused { get; set; } = false;

    /// <summary>
    /// Determines the node that we are viewing the local topology of. If not
    /// null, the simulation renders the local graph of the specified node.
    /// </summary>
    public WarpNode? ViewingNode { get; set; } = null;

    /// <summary>
    /// Determines if nodes should start with their local topologies populated
    /// with the actual network topology.
    /// </summary>
    public bool PopulateDatabasesOnLoad { get; set; } = false;

    public uint FrameCount { get; set; } = 0;

    // the two nodes to draw the shortest paths of, for demonstration purposes
    private (WarpNode Source, WarpNode Destination)? _drawNodePaths;

    // a list of paths to highlight as the shortest paths returned by some node
    // specified by the user
    private List<DijkstraResult> _drawShortestPaths = [];

    /// <summary>
    /// A multiplier affecting the rate of traffic generation. Higher values
    /// result in more traffic.
    /// </summary>
    public double TrafficDensity { get; set; } = 1.0;

    public Simulation()
    {

    }

    /// <summary>
    /// Called on every frame.
    /// </summary>
    public void Update(float delta)
    {
        FrameCount++;

        // check if just pressed space to pause/unpause
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            IsPaused = !IsPaused;
            WriteOutput(IsPaused ? "Simulation paused." : "Simulation unpaused.");
        }

        // screenshot on F12
        if (Raylib.IsKeyPressed(KeyboardKey.F12))
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"screenshot_{now}.png";
            Raylib.TakeScreenshot(filename);
            WriteOutput($"Screenshot saved to {filename}");
        }

        if (IsPaused)
        {
            return;
        }

        // use a queue to avoid modifying the _updateables collection during
        // iteration
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

    /// <summary>
    /// Takes in a command and list of arguments and processes it, changing
    /// the simulation state.
    /// </summary>
    public void ProcessCommand(string command, string[] args)
    {
        switch (command)
        {
            case "drawpaths":
                DrawPaths(args);
                break;
            case "clearpaths":
                ClearPaths();
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
                    ViewingNode = null;
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

    /// <summary>
    /// Called on every frame to draw the simulation.
    /// </summary>
    public void Draw()
    {
        if (ViewingNode is not null)
        {
            ViewingNode.Database.LocalGraph.Draw();
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

        // if the user specified two nodes to draw the top k paths
        if (_drawNodePaths is not null)
        {
            // draw the node's cached paths
            var start = _drawNodePaths.Value.Source;
            var end = _drawNodePaths.Value.Destination;

            var paths = start.Database.GetRoutes(end)
                .Select((v) => v.Path.Path);

            // draw every edge in every path
            foreach (var path in paths)
            {
                var edges = start.Database.LocalGraph.GetEdges(path);

                Color color = Color.DarkBlue;

                foreach (var edge in edges)
                {
                    Vector2 startV = edge.Vertices[0].Position;
                    Vector2 endV = edge.Vertices[1].Position;

                    Raylib.DrawLineEx(startV, endV, 4f, color);
                }
            }
        }
    }

    /// <summary>
    /// Handler to load a simulation graph from a specific JSON file.
    /// </summary>
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

    /// <summary>
    /// Command handler to draw top k paths from source to destination.
    /// </summary>
    public void DrawPaths(string[] args)
    {
        Argument<WarpNode> source = new("source")
        {
            CustomParser = ParseNodeName,
            Arity = ArgumentArity.ExactlyOne,
        };

        Argument<WarpNode> destination = new("destination")
        {
            CustomParser = ParseNodeName,
            Arity = ArgumentArity.ExactlyOne,
        };

        var rootCommand = new RootCommand("send")
        {
            source,
            destination,
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

        var sourceNode = parseResult.GetValue(source)!;
        var destinationNode = parseResult.GetValue(destination)!;

        _drawNodePaths = (sourceNode, destinationNode);
        WriteOutput($"Drawing paths between {args[0]} and {args[1]}");
    }

    /// <summary>
    /// Command handler to clear all drawn paths.
    /// </summary>
    public void ClearPaths()
    {
        _drawNodePaths = default;
        WriteOutput("Cleared drawn paths");
    }

    /// <summary>
    /// Custom parser for System.CommandLine parser, taking in a node name
    /// and returning the node object of that specified name.
    /// </summary>
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

    /// <summary>
    /// Command handler to send a message of specified length using WARP with
    /// TCP Reno.
    /// </summary>
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

        // init TCP sessions
        TransportLayer.TcpSession sender = new(start);
        TransportLayer.TcpSession receiver = new(end);

        sender.PeerSession = receiver;
        receiver.PeerSession = sender;

        // event fired whenever all bytes have been acknowledged
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

            // remove sessions after data received
            RemoveUpdateableQueue.Enqueue(sender);
            RemoveUpdateableQueue.Enqueue(receiver);
        };

        AddUpdateableQueue.Enqueue(sender);
        AddUpdateableQueue.Enqueue(receiver);

        sender.SendData(new byte[length]);

        WriteOutput($"Sending message from {start.Name} to {end.Name} of size {length} bytes.");
    }

    /// <summary>
    /// Command handler to set the maximum amount of candidate paths
    /// </summary>
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

    /// <summary>
    /// Command handler to toggle a node dead or alive.
    /// </summary>
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

    /// <summary>
    /// Command handler for viewing the specified node's local graph.
    /// </summary>
    public void ViewNodeLocalGraph(string nodeName)
    {
        var node = NetworkGraph.Vertices
            .FirstOrDefault(v => v.Name == nodeName);
        if (node is null)
        {
            WriteOutput("Invalid node.");
            return;
        }
        ViewingNode = node;
        WriteOutput($"Now viewing local graph of node {nodeName}.");
    }
}
