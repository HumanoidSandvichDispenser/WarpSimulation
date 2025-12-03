namespace WarpSimulation;

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
        ulong SequenceNumber,
        WarpNode Node,
        List<LinkRecord> Links);

    /// <summary>
    /// The node's local copy of the graph. Other nodes must pass on their
    /// updates to this node to keep it current.
    /// </summary>
    public WarpNetworkGraph LocalGraph { get; private set; } = new();

    public Dictionary<WarpNode, WarpNodeRecord> NodeRecords { get; private set; } = new();

    public Dictionary<Link, LinkRecord> LinkRecords { get; private set; } = new();

    public record struct LinkRecord(
        Link Link,
        WarpNode ConnectedNode,
        float EffectiveBandwidth);

    public void UpdateDatabase(IEnumerable<WarpNodeRecord> updates)
    {
        foreach (var update in updates)
        {
            LocalGraph.AddVertex(update.Node);
            NodeRecords[update.Node] = update;

            foreach (var link in update.Links)
            {
                LocalGraph.AddVertex(link.ConnectedNode);
                LocalGraph.AddEdge(update.Node, link.ConnectedNode, link.Link);
                LinkRecords[link.Link] = link;
            }
        }
    }

    public void UpdateDatabaseFromGraph(WarpNetworkGraph graph)
    {
        // for each vertex, create a node record and link records
        foreach (var vertex in graph.Vertices)
        {
            var links = new List<LinkRecord>();

            foreach (var (neighbor, edge) in graph.Adjacency[vertex])
            {
                var linkRecord = new LinkRecord(
                    Link: edge,
                    ConnectedNode: neighbor,
                    EffectiveBandwidth: (float)edge.CalculateEffectiveBandwidth());

                links.Add(linkRecord);

                LinkRecords[edge] = linkRecord;
            }

            var nodeRecord = new WarpNodeRecord(
                SequenceNumber: 0,
                Node: vertex,
                Links: links);

            NodeRecords[vertex] = nodeRecord;
        }

        LocalGraph = graph;
    }
}
