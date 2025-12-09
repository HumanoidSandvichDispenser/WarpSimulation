namespace WarpSimulation;

/// <summary>
/// An extension of an undirected weighted graph, made specifically for WARP
/// networks and the simulation.
/// </summary>
public class WarpNetworkGraph : UndirectedWeightedGraph<WarpNode, Link>
{
    public void Update(float delta)
    {
        foreach (var link in Edges)
        {
            link.Update(delta);
        }

        foreach (var node in Vertices)
        {
            node.Update(delta);
        }
    }

    public void Draw()
    {
        foreach (var link in Edges)
        {
            link.Draw();
        }

        foreach (var node in Vertices)
        {
            node.Draw();
        }
    }
}
