namespace Parcs.Core
{
    public interface IChannel
    {
        void WriteSignal(Signal signal);
        void WriteData(bool data);
        void WriteData(byte data);
        void WriteData(int data);
        void WriteData(long data);
        void WriteData(double data);
        void WriteData(string data);
        void WriteObject<T>(T @object);
        Signal ReadSignal();
        bool ReadBoolean();
        byte ReadByte();
        int ReadInt();
        long ReadLong();
        double ReadDouble();
        string ReadString();
        T ReadObject<T>();
    }
}