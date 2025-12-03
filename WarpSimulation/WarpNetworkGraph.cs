namespace WarpSimulation;

public class WarpNetworkGraph : UndirectedWeightedGraph<WarpNode, Link>
{
    public void Update(float delta)
    {

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
