namespace WarpSimulation.Tests;

public class WarpNodeTest
{
    [Fact]
    public void KPathSelection_ShouldPickTopKPaths()
    {
        var graph = new WarpNetworkGraph();

        var nodeA = new WarpNode("A");
        var nodeB = new WarpNode("B");
        var nodeC = new WarpNode("C");
        var nodeD = new WarpNode("D");
        var nodeE = new WarpNode("E");

        graph.AddEdge(nodeA, nodeB, new(1.0));
        graph.AddEdge(nodeB, nodeE, new(1.0));

        graph.AddEdge(nodeA, nodeC, new(4.0));
        graph.AddEdge(nodeC, nodeE, new(4.0));

        graph.AddEdge(nodeA, nodeD, new(3.0));
        graph.AddEdge(nodeD, nodeE, new(2.0));

        nodeA.Database.UpdateDatabaseFromGraph(graph);

        var paths = nodeA
            .KPathSelection(nodeE, 2)
            .ToList();

        paths.Count.ShouldBe(2);
        paths[0].Path
            .ToArray()
            .ShouldBeEquivalentTo(new[] { nodeA, nodeC, nodeE });
        paths[1].Path
            .ToArray()
            .ShouldBeEquivalentTo(new[] { nodeA, nodeD, nodeE });
    }

    [Fact]
    public void KPathSelection_DiscardsFilledBandwidth()
    {
        var graph = new WarpNetworkGraph();

        var nodeA = new WarpNode("A");
        var nodeB = new WarpNode("B");
        var nodeC = new WarpNode("C");
        var nodeD = new WarpNode("D");
        var nodeE = new WarpNode("E");

        graph.AddEdge(nodeA, nodeB, new(1));
        graph.AddEdge(nodeA, nodeC, new(1));

        graph.AddEdge(nodeB, nodeD, new(1));
        graph.AddEdge(nodeC, nodeD, new(1));

        graph.AddEdge(nodeD, nodeE, new(1));

        nodeA.Database.UpdateDatabaseFromGraph(graph);

        var paths = nodeA
            .KPathSelection(nodeE, 2)
            .ToList();

        // even though there are two equal cost paths A-B-D-E and A-C-D-E,
        // and we have picked k = 2,
        // we should only expect one path (the one picked by Dijkstra) to
        // consume all the bandwidth on D <-> E link (the bottleneck), so no
        // other paths should be available
        paths.Count.ShouldBe(1);
    }

    [Fact]
    public void KPathSelection_DiscardsFilledBandwidth2()
    {
        var graph = new WarpNetworkGraph();

        var nodeA = new WarpNode("A");
        var nodeB = new WarpNode("B");
        var nodeC = new WarpNode("C");
        var nodeD = new WarpNode("D");
        var nodeE = new WarpNode("E");
        var nodeF = new WarpNode("F");
        var nodeG = new WarpNode("G");

        graph.AddEdge(nodeA, nodeB, new(2.0));
        graph.AddEdge(nodeA, nodeC, new(8.0));
        graph.AddEdge(nodeA, nodeD, new(1.0));
        graph.AddEdge(nodeB, nodeE, new(2.0));
        graph.AddEdge(nodeC, nodeE, new(8.0));
        graph.AddEdge(nodeD, nodeE, new(1.0));
        graph.AddEdge(nodeE, nodeG, new(10.0));
        graph.AddEdge(nodeD, nodeF, new(1.0));
        graph.AddEdge(nodeF, nodeG, new(1.0));

        nodeA.Database.UpdateDatabaseFromGraph(graph);

        var paths = nodeA
            .KPathSelection(nodeG, 4)
            .ToList();

        paths.Count.ShouldBe(3);
        paths[0].Path.ToArray()
            .ShouldBeEquivalentTo(new[] { nodeA, nodeC, nodeE, nodeG });
        paths[1].Path.ToArray()
            .ShouldBeEquivalentTo(new[] { nodeA, nodeB, nodeE, nodeG });
        paths[2].Path.ToArray()
            .ShouldBeEquivalentTo(new[] { nodeA, nodeD, nodeF, nodeG });
    }
}
