namespace WarpSimulation.Packets;

/// <summary>
/// A datagram packet that contains a WARP Link State Advertisement (LSA),
/// used for sharing network topology information between WARP nodes.
/// </summary>
public class WarpLsaDatagram : Datagram, ICloneable
{
    public WarpLsaDatagram(WarpNode source, WarpNode? destination)
        : base(source, destination)
    {
        ForwardingNode = source;
    }

    public int SequenceNumber { get; set; }

    public WarpDatabase.WarpNodeRecord NodeRecord { get; set; }

    public WarpNode ForwardingNode { get; set; }

    public override int HeaderSize
    {
        get
        {
            // link ID + node ID + effective bandwidth
            int linkRecordSize = 4 + 4 + 4;

            // seq no + node ID + links
            int size = SequenceNumber + 4 + linkRecordSize * NodeRecord.Links.Count;

            return size + base.HeaderSize;
        }
    }

    public object Clone()
    {
        return new WarpLsaDatagram(Source, Destination)
        {
            SequenceNumber = SequenceNumber,
            NodeRecord = NodeRecord,
            ForwardingNode = ForwardingNode,
            Payload = Payload
        };
    }
}
