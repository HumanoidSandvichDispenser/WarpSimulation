namespace WarpSimulation;

public class WarpNetworkGraph : UndirectedWeightedGraph<WarpNode, Link>
{
    public void Update(float delta)
    {
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

    /// <summary>
    /// Draws the shortest paths between two nodes for debugging purposes.
    /// </summary>
    /// <remarks>
    /// This uses the selected node's local database rather than this graph's
    /// data. Ensure that the node's database is up to date before calling
    /// this method.
    /// </remarks>
    public void DebugDrawShortestPath(WarpNode from, WarpNode to)
    {
        var paths = from.KPathSelection(to, 8);

        foreach (var (path, i) in paths.Select((p, index) => (p, index)))
        {
            Console.WriteLine($"Adding rank {i} to path: " +
                string.Join(" -> ", path.Path.Select(v => v.Name)));
            var vertexList = path.Path.ToList();
            for (int j = 0; j < vertexList.Count - 1; j++)
            {
                var edge = GetEdge(vertexList[j], vertexList[j + 1]);
                if (edge != null)
                {
                    edge.DrawInfo.Rank.Add(i);
                }
            }
        }
    }
}
