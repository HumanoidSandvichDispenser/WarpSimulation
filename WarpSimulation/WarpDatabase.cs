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
        List<LinkRecord> Links,
        double HighestObservedQueueRate = 0);

    /// <summary>
    /// The node's local copy of the graph. Other nodes must pass on their
    /// updates to this node to keep it current.
    /// </summary>
    public WarpNetworkGraph LocalGraph { get; private set; } = new();

    public Dictionary<WarpNode, WarpNodeRecord> NodeRecords { get; private set; } = new();

    /// <summary>
    /// The direct neighbors of this node, with the time since the last LSA was
    /// received from them.
    /// </summary>
    public Dictionary<WarpNode, float> DirectNeighbors { get; private set; } = new();

    /// <summary>
    /// The link records for each link in the local graph.
    /// </summary>
    public Dictionary<Link, LinkRecord> LinkRecords { get; private set; } = new();

    /// <summary>
    /// Cached routes to each destination node.
    /// </summary>
    public Dictionary<WarpNode, List<RouteInformation>> Routes { get; private set; } = new();

    /// <summary>
    /// The latest sequence numbers seen from each node.
    /// </summary>
    public Dictionary<WarpNode, int> SequenceNumbers { get; private set; } = new();

    /// <summary>
    /// The latest sequence numbers seen from each node.
    /// </summary>
    public Dictionary<WarpNode, WarpNode> SequenceNumberOrigin { get; private set; } = new();

    /// <summary>
    /// The maximum sequence number seen from any node, used for generating new
    /// sequence numbers for LSAs.
    /// </summary>
    public int MaxSequenceNumber { get; private set; } = 0;

    /// <summary>
    /// The timeout in seconds for considering a direct neighbor dead or
    /// unreachable if no LSAs are received.
    /// </summary>
    public int LsaNeighborTimeout { get; set; } = 8;

    private float _elapsedTime = 0;

    public WarpNode Owner { get; init; }

    private int _topK = 8;

    /// <summary>
    /// The number of top paths to consider for routing decisions.
    /// </summary>
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
        double EffectiveBandwidth);

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
    /// <returns>
    /// <c>true</c> if the database updated; <c>false</c> otherwise.
    /// </returns>
    public bool ProcessLsa(Packets.WarpLsaDatagram lsa)
    {
        if (!SequenceNumbers.ContainsKey(lsa.NodeRecord.Node))
        {
            SequenceNumbers[lsa.NodeRecord.Node] = 0;
            SequenceNumberOrigin[lsa.NodeRecord.Node] = lsa.ForwardingNode;
        }

        if (lsa.SequenceNumber <= SequenceNumbers[lsa.NodeRecord.Node])
        {
            // stale LSA, ignore

            // but if it's from a direct neighbor (forwarded by them), reset
            // timer
            if (DirectNeighbors.ContainsKey(lsa.ForwardingNode))
            {
                DirectNeighbors[lsa.ForwardingNode] = 0;
            }

            return false;
        }

        // update max sequence number seen, for transmission purposes
        if (lsa.SequenceNumber > MaxSequenceNumber)
        {
            MaxSequenceNumber = lsa.SequenceNumber;
        }

        SequenceNumbers[lsa.NodeRecord.Node] = lsa.SequenceNumber;
        SequenceNumberOrigin[lsa.NodeRecord.Node] = lsa.ForwardingNode;
        UpdateDatabase(lsa.NodeRecord);

        // then add direct neighbor as an edge
        AddNeighborFromLsa(lsa);

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

        // if edge exists, update it; else add it
        foreach (var linkRecord in update.Links)
        {
            var link = LocalGraph.GetEdge(update.Node, linkRecord.ConnectedNode);

            LinkRecord newLinkRecord = linkRecord;

            // update effective bandwidth based on our current records
            if (link is not null)
            {
                // update existing link
                newLinkRecord.Link = link;
            }
            else
            {
                // add new edge to graph
                link = linkRecord.Link.Clone();
                LocalGraph.AddEdge(update.Node, linkRecord.ConnectedNode, link);
                newLinkRecord.Link = link;
            }

            // update link effective bandwidth
            // check if both ends of the link have records
            if (NodeRecords.ContainsKey(linkRecord.ConnectedNode))
            {
                if (NodeRecords.ContainsKey(update.Node))
                {
                    // only recalculate based on queue rates if we have
                    // multiple paths to choose from
                    if (TopK > 1)
                    {
                        double bw = link.CalculateEffectiveBandwidthFromNodeRecords(
                                NodeRecords[update.Node],
                                NodeRecords[linkRecord.ConnectedNode]);
                        newLinkRecord = newLinkRecord with { EffectiveBandwidth = bw };
                    }
                }
            }

            LinkRecords[link] = newLinkRecord;
        }

        // remove edges not in update (only applicable when not applying an
        // update from self)
        if (update.Node == Owner)
        {
            Routes.Clear();
            return;
        }

        Queue<WarpNode>? endpointsToRemove = null;
        foreach (var (vertex, edge) in LocalGraph.GetNeighbors(update.Node))
        {
            // check for each edge to see if it's in the update
            if (!update.Links.Any(link => edge.Equals(link.Link)))
            {
                // remove edge
                endpointsToRemove ??= new();
                endpointsToRemove.Enqueue(vertex);
                LinkRecords.Remove(edge);
            }
        }

        if (endpointsToRemove is not null)
        {
            while (endpointsToRemove.Count > 0)
            {
                var endpoint = endpointsToRemove.Dequeue();
                LocalGraph.RemoveEdge(update.Node, endpoint);
            }
        }

        Routes.Clear();
    }

    /// <summary>
    /// Rebuilds the entire local database from the provided network graph.
    /// This is used at initialization to skip the initial LSA exchange
    /// process, allowing nodes to start with a full view of the network.
    /// </summary>
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

                // clone the edge to avoid shared references, since adding
                // edges modifies their endpoints
                var clonedEdge = edge.Clone();
                var linkRecord = new LinkRecord(
                    Link: clonedEdge,
                    ConnectedNode: neighbor,
                    EffectiveBandwidth: edge.CalculateEffectiveBandwidth());

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

        // then for each neighbor of ours, add to direct neighbors
        foreach (var (neighbor, _) in LocalGraph.GetNeighbors(Owner))
        {
            DirectNeighbors[neighbor] = 0;
        }
    }

    /// <summary>
    /// Gets possible routes to the specified destination, computing them
    /// if not already cached.
    /// </summary>
    public List<RouteInformation> GetRoutes(WarpNode destination)
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

    /// <summary>
    /// Picks a path to the specified destination based on adjusted weights
    /// and deficits, updating the deficits accordingly.
    /// </summary>
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
                + route.DeficitBytes / alpha(packetSize);
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

    /// <summary>
    /// Gets the next sequence number for an LSA datagram
    /// to be sent from the owner node.
    /// </summary>
    public int GetNextSequenceNumber()
    {
        return MaxSequenceNumber + 1;
    }

    /// <summary>
    /// Creates a node record for the owner node based on its current links.
    /// </summary>
    public WarpNodeRecord CreateNodeRecord()
    {
        var links = new List<LinkRecord>();

        foreach (var (neighbor, edge) in LocalGraph.GetNeighbors(Owner))
        {
            var link = LocalGraph.GetEdge(Owner, neighbor)!;
            double bw = link.CalculateEffectiveBandwidth();

            var linkRecord = new LinkRecord(
                Link: link,
                ConnectedNode: neighbor,
                EffectiveBandwidth: bw);

            links.Add(linkRecord);
        }

        var nodeRecord = new WarpNodeRecord(
            Node: Owner,
            Links: links);

        var queueLinks = Owner.PacketQueue.Keys;
        nodeRecord.HighestObservedQueueRate = queueLinks
            .Select(link => Owner.PacketQueue[link].QueueRatio)
            .DefaultIfEmpty(0)
            .Max();

        return nodeRecord;
    }

    // adds a direct neighbor from an LSA if applicable
    private void AddNeighborFromLsa(Packets.WarpLsaDatagram lsa)
    {
        var localLink = LocalGraph.GetEdge(
            Owner,
            lsa.ForwardingNode);

        if (localLink is not null)
        {
            // reset timer for direct neighbor
            DirectNeighbors[lsa.ForwardingNode] = 0;
            return;
        }

        var directLink = Simulation.Instance.NetworkGraph.GetEdge(
            Owner,
            lsa.ForwardingNode);

        if (directLink is not null)
        {
            var clonedLink = directLink.Clone();
            var newRecord = CreateNodeRecord();
            newRecord.Links.Add(new LinkRecord(
                Link: clonedLink,
                ConnectedNode: lsa.ForwardingNode,
                EffectiveBandwidth: clonedLink.CalculateEffectiveBandwidth()));
            UpdateDatabase(newRecord);
            DirectNeighbors[lsa.ForwardingNode] = 0;
        }
    }

    // declares a neighbor dead due to timeout
    private void DeclareNeighborDead(WarpNode neighbor)
    {
        // first remove from direct neighbors and node records
        DirectNeighbors.Remove(neighbor);
        NodeRecords.Remove(neighbor);

        Console.WriteLine($"Node {Owner.Name} declared neighbor {neighbor.Name} dead.");

        // remove link record
        var link = LocalGraph.GetEdge(Owner, neighbor)!;
        LinkRecords.Remove(link);

        // get neighbors of the dead neighbor to notify
        var neighborsOfNeighbor = LocalGraph.GetNeighbors(neighbor)
            .Select(n => n.Vertex)
            .Where(n => n != Owner)
            .ToList();

        // update graph to remove edge
        LocalGraph.RemoveEdge(Owner, neighbor);

        Routes.Clear();

        // Then notify other neighbors that the link between you and neighbor
        // is down by sending updated LSA. This just means our node and the
        // dead neighbor are not connected, not that the neighbor is down; it
        // is the responsibility of other nodes to determine that based on
        // their own direct connections
        foreach (var neighborOfNeighbor in neighborsOfNeighbor)
        {
            if (neighborOfNeighbor == Owner)
            {
                continue;
            }

            var lsa = new Packets.WarpLsaDatagram(Owner, neighborOfNeighbor)
            {
                SequenceNumber = GetNextSequenceNumber(),
                NodeRecord = CreateNodeRecord()
            };

            Owner.SendDatagram(neighborOfNeighbor, lsa);
        }
    }

    public void Update(float deltaTime)
    {
        _elapsedTime += deltaTime;

        // update direct neighbor timers

        // make a hashset of neighbors to remove to avoid modifying during
        // iteration
        HashSet<WarpNode>? neighborsToRemove = null;

        foreach (var neighbor in DirectNeighbors.Keys.ToList())
        {
            DirectNeighbors[neighbor] += deltaTime;
            if (DirectNeighbors[neighbor] >= LsaNeighborTimeout)
            {
                // neighbor considered dead
                neighborsToRemove ??= new HashSet<WarpNode>();
                neighborsToRemove.Add(neighbor);
            }
        }

        if (neighborsToRemove is not null)
        {
            foreach (var neighbor in neighborsToRemove)
            {
                DeclareNeighborDead(neighbor);
            }
        }
    }
}
