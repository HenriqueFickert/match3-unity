public class Package
{
    public string protocolId;
    public int sequence;
    public int ack;
    public string type;
    public GameCommand packageData;

    public Package()
    { }

    public Package(int sequence, int ack, GameCommand data, RequestType type)
    {
        protocolId = "MRQST";
        this.sequence = sequence;
        this.ack = ack;
        this.type = type.ToString();
        packageData = data;
    }
}