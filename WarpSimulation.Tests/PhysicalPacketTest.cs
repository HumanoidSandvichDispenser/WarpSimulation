namespace WarpSimulation.Tests;

public class PhysicalPacketTest
{
    [Fact]
    public void PhysicalPacket_ShouldInvokeOnTransmissionCompleteEvent()
    {
        var startNode = new WarpNode("Start");
        var endNode = new WarpNode("End");

        var datagram = new Packets.Datagram(startNode, endNode, new byte[128]);

        var packet = new Packets.PhysicalPacket(startNode, endNode, datagram)
        {
            TransmissionTime = 1.0f,
        };

        bool eventInvoked = false;

        packet.OnTransmissionComplete += (p) => eventInvoked = true;

        packet.Update(1.0f);

        eventInvoked.ShouldBeTrue();
    }
}
