namespace WarpSimulation;

public interface IEdge : IComparable
{
    double Weight { get; }

    /// <summary>
    /// Indicates whether the edge has been marked for algorithms that require
    /// marking edges (e.g., during traversal).
    /// </summary>
    public bool IsMarked { get; set; }
}
