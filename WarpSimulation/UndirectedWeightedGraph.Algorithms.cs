namespace WarpSimulation;

// this file contains useful graph algorithms for pathfinding

public partial class UndirectedWeightedGraph<TVertex, TEdge>
{
    /// <summary>
    /// Finds the shortest path from source to target using Dijkstra's
    /// algorithm.
    /// </summary>
    /// <returns>
    /// A DijkstraResult containing the total weight and the path as a
    /// sequence of vertices.
    /// /returns>
    /// <param name="forbidden">
    /// An optional set of vertices to avoid during the search. Used for Yen's
    /// algorithm to avoid loops.
    /// </param>
    public DijkstraResult Dijkstra(
        TVertex source,
        TVertex target,
        HashSet<TVertex>? forbidden = null)
    {
        forbidden ??= new HashSet<TVertex>();
        Dictionary<TVertex, double> distances = new();
        Dictionary<TVertex, TVertex?> previous = new();
        List<TVertex> unvisited = new();

        foreach (var vertex in Vertices)
        {
            distances[vertex] = double.PositiveInfinity;
            unvisited.Add(vertex);
        }

        distances[source] = 0;

        while (unvisited.Count > 0)
        {
            var current = unvisited.MinBy(v => distances[v]);

            if (current is null)
            {
                break;
            }

            if (current.Equals(target))
            {
                Stack<TVertex> path = new();

                if (previous.ContainsKey(current) || current.Equals(source))
                {
                    while (current is not null)
                    {
                        path.Push(current);

                        if (previous.ContainsKey(current))
                        {
                            current = previous[current];
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                return new(distances[target], path);
            }

            unvisited.Remove(current);

            foreach (var (neighbor, edge) in GetNeighbors(current))
            {
                if (forbidden.Contains(neighbor))
                {
                    continue;
                }

                double alt = distances[current] + edge.Weight;
                if (alt < distances[neighbor])
                {
                    distances[neighbor] = alt;
                    previous[neighbor] = current;
                }
            }
        }

        return new(double.PositiveInfinity, Enumerable.Empty<TVertex>());
    }

    /// <summary>
    /// Enumerates all shortest paths from source to target using Yen's algorithm.
    /// </summary>
    /// <remarks>
    /// There is no k parameter to limit the number of paths returned;
    /// the caller can simply stop enumerating when they have enough paths.
    /// </remarks>
    /// <returns>
    /// An enumerable of DijkstraResult, each representing a shortest path.
    /// </returns>
    /// <param name="source">The starting vertex.</param>
    /// <param name="target">The target vertex.</param>
    public IEnumerable<DijkstraResult> YensAlgorithm(TVertex source, TVertex target)
    {
        var shortestPaths = new List<DijkstraResult>();
        var candidatePaths = new PriorityQueue<DijkstraResult, double>();

        var first = Dijkstra(source, target);
        if (!first.Path.Any())
        {
            yield break;
        }

        shortestPaths.Add(first);
        yield return first;

        while (true)
        {
            var lastPath = shortestPaths.Last();
            var lastVertices = lastPath.Path.ToList();

            for (int i = 0; i < lastVertices.Count - 1; i++)
            {
                var spurNode = lastVertices[i];
                var rootPath = lastVertices.Take(i + 1).ToList();

                // store edges removed to restore later
                var removedEdges = new HashSet<(TVertex from, TVertex to, TEdge edge)>();

                // remove edges that would create loops
                foreach (var p in shortestPaths)
                {
                    var pList = p.Path.ToList();
                    if (pList.Count > i && rootPath.SequenceEqual(pList.Take(i + 1)))
                    {
                        var from = pList[i];
                        var to = pList[i + 1];
                        var edge = GetEdge(from, to);
                        if (edge != null)
                        {
                            RemoveEdge(from, to);
                            removedEdges.Add((from, to, edge));
                        }
                    }
                }

                // compute spur path from spurNode to target
                var forbidden = new HashSet<TVertex>(rootPath);
                forbidden.Remove(spurNode);
                var spurPathResult = Dijkstra(spurNode, target, forbidden);
                if (spurPathResult.Path.Any())
                {
                    var spurPath = spurPathResult.Path.ToList();

                    // combine root and spur paths, avoiding duplicate spurNode
                    var totalPath = rootPath
                        .Take(rootPath.Count - 1)
                        .Concat(spurPath)
                        .ToList();

                    var totalWeight = 0.0;

                    for (int j = 0; j < totalPath.Count - 1; j++)
                    {
                        var edge = GetEdge(totalPath[j], totalPath[j + 1]);
                        totalWeight += edge?.Weight ?? 0;
                    }

                    candidatePaths.Enqueue(new(totalWeight, totalPath), totalWeight);
                }

                // restore removed edges
                foreach (var (from, to, edge) in removedEdges)
                {
                    AddEdge(from, to, edge);
                }
            }

            if (candidatePaths.Count == 0)
            {
                yield break;
            }

            var next = candidatePaths.Dequeue();
            if (shortestPaths.Any(p => p.Path.SequenceEqual(next.Path)))
            {
                continue;
            }

            shortestPaths.Add(next);
            yield return next;
        }
    }
}
