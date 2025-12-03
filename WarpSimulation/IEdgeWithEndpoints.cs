namespace WarpSimulation;

/// <summary>
/// An edge that has defined endpoints.
/// </summary>
public interface IEdgeWithEndpoints<TVertex> : IEdge
{
    public TVertex[] Vertices { get; }
}
