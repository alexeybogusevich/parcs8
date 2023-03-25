namespace Parcs.Net
{
    public enum Signal : byte
    {
        TransmitData = 0,
        InitializeJob = 1,
        ExecuteClass = 2,
        AcknowledgeConnection = 3,
    }
}