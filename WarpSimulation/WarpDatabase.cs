using System;

namespace WarpSimulation;

using DijkstraResult = UndirectedWeightedGraph<WarpNode, Link>.DijkstraResult;

/// <summary>
/// Attaches information about a candidate route for weight adjustment
/// and deficit tracking.
/// </summary>
public class RouteInformation
{
    /// <summary>
    /// The path on the graph for this route.
    /// </summary>
    public DijkstraResult Path { get; set; }

    /// <summary>
    /// The total bytes sent along this route.
    /// </summary>
    public long TotalBytesSent { get; set; }

    /// <summary>
    /// The number of bytes this route is behind its expected share.
    /// </summary>
    public double DeficitBytes { get; set; }

    /// <summary>
    /// The adjusted weight for this route.
    /// </summary>
    public double AdjustedWeight { get; set; }

    public RouteInformation(DijkstraResult path)
    {
        Path = path;
        TotalBytesSent = 0;
        DeficitBytes = 0;
        AdjustedWeight = 0;
    }
}

/// <summary>
/// A database maintaining the state of warp nodes and their connections.
/// Like OSPF, WARP nodes maintain a local copy of the network graph,
/// which is updated based on LSA-like updates from other nodes.
/// </summary>
public class WarpDatabase
{
    /// <summary>
    /// A record of a warp node's state in the database.
    /// </summary>
    public record struct WarpNodeRecord(
        WarpNode Node,
        List<LinkRecord> Links);

    /// <summary>
    /// The node's local copy of the graph. Other nodes must pass on their
    /// updates to this node to keep it current.
    /// </summary>
    public WarpNetworkGraph LocalGraph { get; private set; } = new();

    public Dictionary<WarpNode, WarpNodeRecord> NodeRecords { get; private set; } = new();

    public Dictionary<Link, LinkRecord> LinkRecords { get; private set; } = new();

    public Dictionary<WarpNode, List<RouteInformation>> Routes { get; private set; } = new();

    public Dictionary<WarpNode, int> SequenceNumbers { get; private set; } = new();

    public int MaxSequenceNumber { get; private set; } = 0;

    public WarpNode Owner { get; init; }

    private int _topK = 8;

    public int TopK
    {
        get => _topK;
        set
        {
            _topK = value;
            Console.WriteLine($"Node {Owner.Name} set to use top {_topK} paths for routing.");
            Routes.Clear();
        }
    }

    public record struct LinkRecord(
        Link Link,
        WarpNode ConnectedNode,
        float EffectiveBandwidth);

    public WarpDatabase(WarpNode owner)
    {
        // initialize with self
        var selfRecord = new WarpNodeRecord(
            Node: owner,
            Links: new List<LinkRecord>());
        UpdateDatabase(selfRecord);

        Owner = owner;
    }

    /// <summary>
    /// Processes an incoming LSA datagram, updating the local database
    /// if the LSA is newer than the stored version.
    /// </summary>
    /// <returns>True if the LSA was processed and the database updated; false otherwise.</returns>
    public bool ProcessLsa(Packets.WarpLsaDatagram lsa)
    {
        if (!SequenceNumbers.ContainsKey(lsa.NodeRecord.Node))
        {
            SequenceNumbers[lsa.NodeRecord.Node] = 0;
        }

        if (lsa.SequenceNumber <= SequenceNumbers[lsa.NodeRecord.Node])
        {
            // stale LSA, ignore
            return false;
        }

        // update max sequence number seen, for transmission purposes
        if (lsa.SequenceNumber > MaxSequenceNumber)
        {
            MaxSequenceNumber = lsa.SequenceNumber;
        }

        SequenceNumbers[lsa.NodeRecord.Node] = lsa.SequenceNumber;
        UpdateDatabase(lsa.NodeRecord);
        return true;
    }

    /// <summary>
    /// Updates the local database with the provided node record with an
    /// upsert operation, adding or updating the node and its links, and
    /// removing any links not present in the update.
    /// </summary>
    public void UpdateDatabase(WarpNodeRecord update)
    {
        LocalGraph.AddVertex(update.Node);
        NodeRecords[update.Node] = update;

        // add/update edges from update
        foreach (var link in update.Links)
        {
            LocalGraph.AddVertex(link.ConnectedNode);
            LocalGraph.AddEdge(update.Node, link.ConnectedNode, link.Link.Clone());
            LinkRecords[link.Link] = link;
        }

        // remove edges not in update
        foreach (var (vertex, edge) in LocalGraph.Adjacency[update.Node])
        {
            if (!update.Links.Any(l => l.ConnectedNode == vertex))
            {
                LocalGraph.RemoveEdge(update.Node, vertex);
            }
        }

        Routes.Clear();
    }

    public void UpdateDatabaseFromGraph(WarpNetworkGraph graph)
    {
        LocalGraph.Clear();

        // for each vertex, create a node record and link records
        foreach (var vertex in graph.Vertices)
        {
            var links = new List<LinkRecord>();
            var seenLinks = new HashSet<Link>();

            foreach (var (neighbor, edge) in graph.Adjacency[vertex])
            {
                if (seenLinks.Contains(edge))
                {
                    continue;
                }

                var clonedEdge = edge.Clone();
                var linkRecord = new LinkRecord(
                    Link: clonedEdge,
                    ConnectedNode: neighbor,
                    EffectiveBandwidth: (float)edge.CalculateEffectiveBandwidth());

                links.Add(linkRecord);

                LinkRecords[clonedEdge] = linkRecord;

                seenLinks.Add(edge);

                LocalGraph.AddEdge(vertex, neighbor, clonedEdge);
            }

            var nodeRecord = new WarpNodeRecord(
                Node: vertex,
                Links: links);

            NodeRecords[vertex] = nodeRecord;
            LocalGraph.AddVertex(vertex);
        }
    }

    /// <summary>
    /// Gets possible routes to the specified destination, computing them
    /// if not already cached.
    /// </summary>
    private List<RouteInformation> GetRoutes(WarpNode destination)
    {
        // check cache for available paths to pick
        if (!Routes.ContainsKey(destination))
        {
            // compute k paths to destination
            var ownerOnLocalGraph = LocalGraph.Vertices
                .FirstOrDefault(v => v.Name == Owner.Name);

            if (ownerOnLocalGraph is null)
            {
                throw new NullReferenceException("Owner node not found in local graph");
            }

            Routes[destination] = ownerOnLocalGraph
                .KPathSelection(destination, k: TopK)
                .Select((path) => new RouteInformation(path))
                .ToList();
        }

        return Routes[destination];
    }

    public RouteInformation? PickPath(WarpNode destination, int packetSize)
    {
        List<RouteInformation> routes = GetRoutes(destination);
        RouteInformation? selectedRoute = null;
        var random = new Random();

        // adjust weights based on packet size and deficit
        double totalWeight = 0;

        // loop to find adjusted weights and sum total weight
        foreach (var route in routes)
        {
            // simple weight adjustment: base weight + deficit factor
            double alpha(int size) => 1.0f + size / (size + 512.0f);
            double initialWeight = route.Path.TotalWeight;
            double weight = Math.Pow(initialWeight, alpha(packetSize))
                + route.DeficitBytes / packetSize;
            route.AdjustedWeight = Math.Max(weight, 0.0);
            totalWeight += weight;
        }

        double randValue = random.NextDouble() * totalWeight;
        double cumulative = 0;

        // loop to select route based on adjusted weights
        foreach (var route in routes)
        {
            cumulative += route.AdjustedWeight;
            if (randValue <= cumulative)
            {
                selectedRoute = route;
                break;
            }
        }

        if (selectedRoute is not null)
        {
            selectedRoute.TotalBytesSent += packetSize;
            long globalTotalBytesSent = routes
                .Sum(r => r.TotalBytesSent);
            double globalTotalWeight = routes
                .Sum(r => r.Path.TotalWeight);

            // update deficits for all routes
            foreach (var route in routes)
            {
                double expected = globalTotalBytesSent * route.Path.TotalWeight
                    / globalTotalWeight;
                route.DeficitBytes = expected - route.TotalBytesSent;
            }
        }

        return selectedRoute;
    }
}
