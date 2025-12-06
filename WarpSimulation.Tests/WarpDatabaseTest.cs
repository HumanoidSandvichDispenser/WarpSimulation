namespace WarpSimulation.Tests;

public class WarpDatabaseTest
{
    [Fact]
    public void PickPath_ShouldUpdateRoundRobinDeficits()
    {
        var simulation = new Simulation();
        var graph = simulation.NetworkGraph;

        var nodeA = new WarpNode("A");
        var nodeB = new WarpNode("B");
        var nodeC = new WarpNode("C");
        var nodeD = new WarpNode("D");

        graph.AddEdge(nodeA, nodeB, new());
        graph.AddEdge(nodeA, nodeC, new(2048));
        graph.AddEdge(nodeD, nodeB, new());
        graph.AddEdge(nodeD, nodeC, new(2048));
        graph.AddEdge(nodeA, nodeD, new(1024));

        nodeA.Database.UpdateDatabaseFromGraph(graph);

        nodeA.Database.PickPath(nodeD, 64);

        nodeA.Database.Routes.ShouldNotBeEmpty();
        nodeA.Database.Routes[nodeD].ShouldNotBeEmpty();
        nodeA.Database.Routes[nodeD].Any(r => r.DeficitBytes > 0).ShouldBeTrue();
    }

    [Fact]
    public void DeficitBytes_ShouldAddUpToZero()
    {
        var simulation = new Simulation();
        var graph = simulation.NetworkGraph;

        var nodeA = new WarpNode("A");
        var nodeB = new WarpNode("B");
        var nodeC = new WarpNode("C");
        var nodeD = new WarpNode("D");

        graph.AddEdge(nodeA, nodeB, new());
        graph.AddEdge(nodeA, nodeC, new(2048));
        graph.AddEdge(nodeD, nodeB, new());
        graph.AddEdge(nodeD, nodeC, new(2048));
        graph.AddEdge(nodeA, nodeD, new(1024));

        nodeA.Database.UpdateDatabaseFromGraph(graph);

        for (int i = 0; i < 5; i++)
        {
            nodeA.Database.PickPath(nodeD, 32);
        }

        nodeA.Database.Routes[nodeD].Sum(r => r.DeficitBytes).ShouldBe(0);
    }
}
