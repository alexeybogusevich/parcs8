namespace Parcs.Net
{
    public enum Signal : byte
    {
        Unknown = 0,
        InitializeJob = 1,
        ExecuteClass = 2,
        CancelJob = 3,
        CloseConnection = 4,
        InternalChannelSwitch = 5,
    }
}