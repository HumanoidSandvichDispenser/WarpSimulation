namespace WarpSimulation.Tests;

using TestGraph = UndirectedWeightedGraph<int, UndirectedWeightedGraphTest.TestEdge>;

public partial class UndirectedWeightedGraphTest
{
    public class TestEdge : IEdge
    {
        public double Weight { get; private set; }

        public TestEdge(double weight = 1.0)
        {
            Weight = weight;
        }

        public int CompareTo(object? obj)
        {
            if (obj is TestEdge other)
            {
                return Weight.CompareTo(other.Weight);
            }

            throw new ArgumentException($"Object is not a {nameof(TestEdge)}");
        }

        public static implicit operator TestEdge(double weight) => new TestEdge(weight);
    }

    [Fact]
    public void AddingVertex_ShouldAppearInGraph()
    {
        var graph = new TestGraph();

        graph.AddVertex(1);

        graph.Vertices.ShouldContain(1);
    }

    [Fact]
    public void AddingDuplicateVertex_ShouldNotIncreaseVertexCount()
    {
        var graph = new TestGraph();
        graph.AddVertex(1);
        graph.AddVertex(1);
        graph.VertexCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(new int[] { 1, 2, 3 })]
    [InlineData(new int[] { 5, 10, 15, 20 })]
    public void AddingMultipleVertices_ShouldIncreaseVertexCount(int[] vertices)
    {
        var graph = new TestGraph();
        foreach (var vertex in vertices)
        {
            graph.AddVertex(vertex);
        }
        graph.VertexCount.ShouldBe(vertices.Length);
    }

    [Fact]
    public void AddingEdge_ShouldCreateConnectionBetweenVertices()
    {
        var graph = new TestGraph();

        graph.AddVertex(1);
        graph.AddVertex(2);

        graph.AddEdge(1, 2, new TestEdge(5.0));

        graph.Adjacency[1].ShouldContain(x => x.Vertex == 2 && x.Edge.Weight == 5.0);
        graph.Adjacency[2].ShouldContain(x => x.Vertex == 1 && x.Edge.Weight == 5.0);
    }

    [Fact]
    public void AddingEdge_ShouldCreateVerticesIfNotExist()
    {
        var graph = new TestGraph();
        graph.AddEdge(1, 2, new());
        graph.Vertices.ShouldContain(1);
        graph.Vertices.ShouldContain(2);
    }

    [Fact]
    public void AddingEdge_ShouldReplaceExistingEdge()
    {
        var graph = new TestGraph();

        graph.AddVertex(1);
        graph.AddVertex(2);

        graph.AddEdge(1, 2, new TestEdge(2.0));
        graph.AddEdge(1, 2, new TestEdge(4.0));

        graph.Adjacency[1].ShouldContain(x => x.Vertex == 2 && x.Edge.Weight == 4.0);
        graph.Adjacency[2].ShouldContain(x => x.Vertex == 1 && x.Edge.Weight == 4.0);
    }

    [Fact]
    public void RemovingVertex_ShouldRemoveAllConnections()
    {
        var graph = new TestGraph();
        graph.AddVertex(1);
        graph.AddVertex(2);
        graph.AddEdge(1, 2, new TestEdge(3.0));
        graph.RemoveVertex(1);

        graph.Vertices.ShouldNotContain(1);
        graph.Adjacency[2].ShouldNotContain(x => x.Vertex == 1);
    }

    [Fact]
    public void RemovingEdge_ShouldRemoveConnectionBetweenVertices()
    {
        var graph = new TestGraph();
        graph.AddVertex(1);
        graph.AddVertex(2);
        graph.AddEdge(1, 2, new TestEdge(4.0));
        graph.RemoveEdge(1, 2);

        graph.Adjacency[1].ShouldNotContain(x => x.Vertex == 2);
        graph.Adjacency[2].ShouldNotContain(x => x.Vertex == 1);
    }

    [Fact]
    public void RemovingVertex_NotInGraph_ShouldNotThrow()
    {
        var graph = new TestGraph();
        graph.AddVertex(1);
        Should.NotThrow(() => graph.RemoveVertex(2));
    }

    [Fact]
    public void GettingExistingEdge_ShouldReturnEdge()
    {
        var graph = new TestGraph();
        graph.AddEdge(1, 2, 6.0);

        var edge = graph.GetEdge(1, 2);

        edge.ShouldNotBeNull();
        edge.Weight.ShouldBe(6.0);
    }

    [Fact]
    public void GettingNonExistingEdge_ShouldReturnNull()
    {
        var graph = new TestGraph();
        graph.AddVertex(1);
        graph.AddVertex(2);

        var edge = graph.GetEdge(1, 2);

        edge.ShouldBeNull();
    }

    [Theory]
    [InlineData(1, 9, 17.0)]
    [InlineData(1, 8, 10.0)]
    [InlineData(7, 6, 16.0)]
    [InlineData(2, 5, 6.0)]
    public void Dijkstra_ShouldReturnShortestPath(
        int start, int end, double expectedCost)
    {
        var graph = new TestGraph();

        graph.AddEdge(1, 2, 5.0);
        graph.AddEdge(1, 3, 7.0);

        graph.AddEdge(2, 4, 3.0);

        graph.AddEdge(3, 5, 2.0);
        graph.AddEdge(3, 6, 9.0);

        graph.AddEdge(4, 5, 5.0);
        graph.AddEdge(4, 7, 9.0);
        graph.AddEdge(4, 8, 2.0);

        graph.AddEdge(5, 6, 10.0);
        graph.AddEdge(5, 8, 1.0);
        graph.AddEdge(5, 9, 8.0);

        graph.AddEdge(6, 9, 5.0);

        graph.AddEdge(7, 8, 5.0);

        var (cost, path) = graph.Dijkstra(start, end);

        cost.ShouldBe(expectedCost);
    }

    [Fact]
    public void Yens_ShouldReturnKShortestPaths()
    {
        var graph = new TestGraph();

        graph.AddEdge(1, 2, 5.0);
        graph.AddEdge(1, 3, 7.0);

        graph.AddEdge(2, 4, 3.0);

        graph.AddEdge(3, 5, 2.0);
        graph.AddEdge(3, 6, 9.0);

        graph.AddEdge(4, 5, 5.0);
        graph.AddEdge(4, 7, 9.0);
        graph.AddEdge(4, 8, 2.0);

        graph.AddEdge(5, 6, 10.0);
        graph.AddEdge(5, 8, 1.0);
        graph.AddEdge(5, 9, 8.0);

        graph.AddEdge(6, 9, 5.0);

        graph.AddEdge(7, 8, 5.0);

        var paths = graph.YensAlgorithm(3, 8)
            .Take(3)
            .ToList();

        paths.ShouldNotBeNull();

        paths[0].TotalWeight.ShouldBe(3.0);
        paths[1].TotalWeight.ShouldBe(9.0);
        paths[2].TotalWeight.ShouldBe(17.0);
    }
}
