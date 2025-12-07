namespace WarpSimulation.Packets;

public class WarpLsaDatagram : Datagram
{
    public WarpLsaDatagram(WarpNode source, WarpNode destination)
        : base(source, destination)
    {

    }

    public int SequenceNumber { get; set; }

    public WarpDatabase.WarpNodeRecord NodeRecord { get; set; }

    public override int HeaderSize
    {
        get
        {
            // link ID + node ID + effective bandwidth
            int linkRecordSize = 4 + 4 + 4;

            // seq no + node ID + links
            int size = SequenceNumber + 4 + linkRecordSize * NodeRecord.Links.Count;

            return size + base.Size;
        }
    }
}
