namespace WarpSimulation;

/// <summary>
/// A generic undirected weighted graph implementation.
/// </summary>
public partial class UndirectedWeightedGraph<TVertex, TEdge>
    where TVertex : notnull
    where TEdge : IEdge
{
    public Dictionary<TVertex, List<(TVertex Vertex, TEdge Edge)>> Adjacency { get; private set; }

    public List<TVertex>? _cachedVertices = null;

    public List<TVertex> Vertices
    {
        get
        {
            if (_cachedVertices is null)
            {
                _cachedVertices = Adjacency.Keys.ToList();
            }

            return _cachedVertices;
        }
    }

    public int VertexCount => Adjacency.Count;

    private List<TEdge>? _cachedEdges = null;

    public List<TEdge> Edges
    {
        get
        {
            if (_cachedEdges is not null)
            {
                return _cachedEdges;
            }

            HashSet<TEdge> seenEdges = new();
            seenEdges.Clear();

            foreach (var neighbors in Adjacency.Values)
            {
                foreach (var (_, edge) in neighbors)
                {
                    seenEdges.Add(edge);
                }
            }

            _cachedEdges = seenEdges.ToList();
            return _cachedEdges;
        }
    }

    public record struct DijkstraResult(double TotalWeight, IEnumerable<TVertex> Path);

    public UndirectedWeightedGraph()
    {
        Adjacency = new();
    }

    public void AddVertex(TVertex vertex)
    {
        if (vertex is null || vertex.Equals(default))
        {
            throw new ArgumentNullException(
                nameof(vertex), "Vertex cannot be null or default value.");
        }

        if (!Adjacency.ContainsKey(vertex))
        {
            Adjacency[vertex] = new List<(TVertex, TEdge)>();
        }

        _cachedVertices = null;
    }

    public void RemoveVertex(TVertex vertex)
    {
        if (vertex is null || vertex.Equals(default))
        {
            throw new ArgumentNullException(
                nameof(vertex), "Vertex cannot be null or default value.");
        }

        if (Adjacency.ContainsKey(vertex))
        {
            Adjacency.Remove(vertex);
        }
        foreach (var key in Adjacency.Keys)
        {
            Adjacency[key].RemoveAll(x => x.Vertex.Equals(vertex));
        }

        _cachedVertices = null;
    }

    /// <summary>
    /// Adds an undirected edge between two vertices with the specified edge
    /// data. If the vertices do not exist, they are added to the graph.
    /// If the edge already exists, it is replaced.
    /// </summary>
    public void AddEdge(TVertex from, TVertex to, TEdge edge)
    {
        AddVertex(from);
        AddVertex(to);

        if (!Adjacency.ContainsKey(from))
        {
            Adjacency[from] = new List<(TVertex, TEdge)>();
        }

        if (!Adjacency.ContainsKey(to))
        {
            Adjacency[to] = new List<(TVertex, TEdge)>();
        }

        RemoveEdge(from, to);

        if (edge is IEdgeWithEndpoints<TVertex> edgeWithEndpoints)
        {
            edgeWithEndpoints.Vertices[0] = from;
            edgeWithEndpoints.Vertices[1] = to;
        }

        Adjacency[from].Add((to, edge));
        Adjacency[to].Add((from, edge));

        _cachedEdges = null;
    }

    public void RemoveEdge(TVertex from, TVertex to)
    {
        if (Adjacency.ContainsKey(from))
        {
            Adjacency[from].RemoveAll(x => x.Vertex.Equals(to));
        }

        if (Adjacency.ContainsKey(to))
        {
            Adjacency[to].RemoveAll(x => x.Vertex.Equals(from));
        }

        _cachedEdges = null;
    }

    public void Clear()
    {
        Adjacency.Clear();

        _cachedVertices = null;
        _cachedEdges = null;
    }

    public bool ContainsVertex(TVertex vertex)
    {
        return Adjacency.ContainsKey(vertex);
    }

    public TEdge? GetEdge(TVertex from, TVertex to)
    {
        if (Adjacency.ContainsKey(from))
        {
            return Adjacency[from]
                .FirstOrDefault(x => x.Vertex.Equals(to))
                .Edge;
        }

        return default;
    }

    public IEnumerable<(TVertex Vertex, TEdge Edge)> GetNeighbors(TVertex vertex)
    {
        if (Adjacency.ContainsKey(vertex))
        {
            return Adjacency[vertex];
        }

        return Enumerable.Empty<(TVertex, TEdge)>();
    }

    public IEnumerable<TEdge> GetEdges(IEnumerable<TVertex> vertices)
    {
        TVertex prev = default!;
        foreach (var vertex in vertices)
        {
            if (prev is null || prev.Equals(default))
            {
                prev = vertex;
                continue;
            }

            if (!Adjacency.ContainsKey(prev))
            {
                yield break;
            }

            var neighbor = Adjacency[prev].FirstOrDefault((ve) => ve.Vertex.Equals(vertex));

            if (neighbor.Edge is null)
            {
                yield break;
            }
            else
            {
                prev = vertex;
                yield return neighbor.Edge;
            }
        }
    }
}
