namespace WarpSimulation.Tests;

public class SimulationTest
{
    [Fact]
    public void Simulation_ShouldParseJsonVertices()
    {
        string jsonString = @"
        {
            ""nodes"": {
                ""A"": { ""x"": 200, ""y"": 200 },
                ""B"": { ""x"": 170, ""y"": 300 }
            },
            ""links"": [
                { ""vertices"": [""A"", ""B""], ""bandwidth"": 4096 }
            ]
        }";

        var simulation = new Simulation();
        simulation.LoadFromJsonFile(jsonString);

        var graph = simulation.NetworkGraph;

        graph.Vertices.Count.ShouldBe(2);
    }

    [Fact]
    public void Simulation_ShouldParseJsonEdges()
    {
        string jsonString = @"
        {
            ""nodes"": {
                ""A"": { ""x"": 200, ""y"": 200 },
                ""B"": { ""x"": 170, ""y"": 300 }
            },
            ""links"": [
                { ""vertices"": [""A"", ""B""], ""bandwidth"": 4096 }
            ]
        }";

        var simulation = new Simulation();
        simulation.LoadFromJsonFile(jsonString);

        var graph = simulation.NetworkGraph;

        graph.Edges.Count.ShouldBe(1);
        graph.Edges[0].Vertices.Count().ShouldBe(2);
    }
}
