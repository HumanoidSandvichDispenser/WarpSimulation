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
    public WarpDatabase Database { get; private set; }

    public Dictionary<Link, Queue<Packets.PhysicalPacket>> PacketQueue { get; } = new();

    /// <summary>
    /// The processing delay at this node in seconds.
    /// </summary>
    public float ProcessingDelay { get; set; } = 1e-6f;

    /// <summary>
    /// Total number of packets dropped at this node due to routing issues.
    /// </summary>
    public int TotalDroppedPackets { get; private set; } = 0;

    public double ByteLossRate { get; private set; } = 0.0f;

    /// <summary>
    /// Initializes a new instance of the <see cref="WarpNode"/> class.
    /// </summary>
    /// <param name="name">The name of the node.</param>
    /// <param name="position">The position of the node in the simulation.</param>
    public WarpNode(string name = "", Vector2? position = null)
    {
        Name = name;
        Position = position ?? Vector2.Zero;
        Database = new WarpDatabase(this);
    }

    /// <summary>
    /// Event invoked when a datagram destined for this node has been
    /// received.
    /// </summary>
    public event Action<WarpNode, Packets.Datagram>? OnDatagramReceived;

    /// <summary>
    /// Event triggered when a path is accepted during path selection. Useful
    /// for logging and monitoring.
    /// </summary>
    public event Action<WarpNode, DijkstraResult>? OnPathAccepted;

    /// <summary>
    /// Event triggered when a path is rejected during path selection. Useful
    /// for logging and monitoring.
    /// </summary>
    public event Action<WarpNode, DijkstraResult>? OnPathPruned;

    /// <summary>
    /// A modification of Yen's Algorithm to support WARP multi-path selection
    /// with path filtering based on link attributes to generate diverse paths.
    /// THe number of paths returned is within the range of 0 to
    /// <paramref name="k"/>.
    /// </summary>
    public IEnumerable<DijkstraResult> KPathSelection(WarpNode destination, int k)
    {
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

            // minimum available capacity is the computed bottleneck along this
            // path, i.e. the edge with the least available bandwidth
            double minAvailCapacity = edges
                .Min(edge => capacity[edge] - usage[edge]);

            if (index == 0)
            {
                // this is shortest path

                // consume along this path
                foreach (var edge in edges)
                {
                    usage[edge] += minAvailCapacity;
                }

                OnPathAccepted?.Invoke(this, nextPath);
                yield return nextPath;
            }
            else
            {
                if (minAvailCapacity <= 0)
                {
                    // no more bandwidth available on some edge in this path
                    OnPathPruned?.Invoke(this, nextPath);
                    continue;
                }

                // path returned by next iteration of Yen's algorithm

                // if all edges have enough available capacity, consume along
                // this path
                if (edges.All(edge => capacity[edge] - usage[edge] >= minAvailCapacity))
                {
                    foreach (var edge in edges)
                    {
                        usage[edge] += minAvailCapacity;
                    }

                    OnPathAccepted?.Invoke(this, nextPath);
                    yield return nextPath;
                }
                else
                {
                    // at least one edge does not have enough available
                    // capacity
                    OnPathPruned?.Invoke(this, nextPath);
                }
            }
        }
    }

    /// <summary>
    /// Determines the next hop for a given datagram based on its destination
    /// and the node's path selection logic.
    /// </summary>
    /// <returns>
    /// A tuple containing the (possibly modified) datagram and the next hop
    /// node. If there is no valid next hop, the next hop will be <c>null</c>,
    /// indicating that the datagram should be dropped.
    /// </returns>
    public (Packets.Datagram Datagram, WarpNode? Next) NextHop(Packets.Datagram datagram)
    {
        if (datagram.Destination == this)
        {
            return (datagram, null);
        }

        if (datagram is Packets.WarpDatagram warpDatagram)
        {
            // next hop is determined by the path in the datagram
            int currentHopIndex = warpDatagram.CurrentHopIndex;
            currentHopIndex += 1;
            if (currentHopIndex < warpDatagram.Path.Count)
            {
                WarpNode nextHop = warpDatagram.Path[currentHopIndex];
                warpDatagram.CurrentHopIndex = currentHopIndex;
                return (warpDatagram, nextHop);
            }
            else
            {
                // path exhausted, drop datagram
                TotalDroppedPackets += 1;
                return (datagram, null);
            }
        }
        else
        {
            // transform datagram into a WarpDatagram using K-Path Selection
            var route = Database.PickPath(datagram.Destination, datagram.Size);

            if (route is not null)
            {
                // construct WarpDatagram
                var paths = route.Path.Path.ToList();
                var newWarpDatagram = new Packets.WarpDatagram(
                    datagram.Source,
                    datagram.Destination,
                    paths,
                    datagram.Payload);
                newWarpDatagram.CurrentHopIndex = 1;

                // next hop is the second node in the path
                WarpNode nextHop = paths[1];
                return (newWarpDatagram, nextHop);
            }
        }

        return (datagram, null);
    }

    public void ReceiveDatagram(Packets.Datagram datagram)
    {
        if (datagram.Destination == this)
        {
            OnDatagramReceived?.Invoke(this, datagram);
        }
        else
        {
            var (nextDatagram, nextHop) = NextHop(datagram);

            if (nextHop != null)
            {
                SendDatagram(nextHop, nextDatagram);
            }
            else
            {
                // drop datagram
            }
        }
    }

    public void SendDatagram(WarpNode endpoint, Packets.Datagram datagram)
    {
        var graph = Simulation.Instance.NetworkGraph;
        var link = graph.GetEdge(this, endpoint);

        if (link is not null)
        {
            Packets.PhysicalPacket physicalPacket = new(
                start: this,
                end: endpoint,
                datagram: datagram);

            if (!PacketQueue.ContainsKey(link))
            {
                PacketQueue[link] = new();
            }
            PacketQueue[link].Enqueue(physicalPacket);
        }
    }

    public void Update(float deltaTime)
    {
        foreach (var (link, queue) in PacketQueue)
        {
            if (queue.Count > 0)
            {
                var packet = queue.Peek();
                if (link.TransmitPacket(packet))
                {
                    queue.Dequeue();
                }
            }
        }
    }

    /// <summary>
    /// Dumps the contents of the node's database to a string for
    /// visualization, monitoring, or debugging purposes.
    /// </summary>
    public string DumpDatabase()
    {
        System.Text.StringBuilder sb = new();

        sb.AppendLine($"Node {Name} Database Dump:");

        foreach (var (node, record) in Database.NodeRecords)
        {
            sb.AppendLine($"  Node: {node.Name}");
            sb.AppendLine($"    Links:");
            foreach (var linkRecord in record.Links)
            {
                var link = linkRecord.Link;
                sb.AppendLine($"      Link to {link.GetOtherNode(node).Name}:");
                sb.AppendLine($"        Effective Bandwidth: {linkRecord.EffectiveBandwidth}");
            }
        }

        return sb.ToString();
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
