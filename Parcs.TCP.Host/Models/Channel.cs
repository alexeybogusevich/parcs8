using NetCoreServer;
using Parcs.Core;
using System.Text.Json;

namespace Parcs.TCP.Host.Models
{
    internal class Channel : IChannel
    {
        private readonly TcpSession _hostSession;

        public Channel(TcpSession hostSession)
        {
            _hostSession = hostSession;
        }

        public bool ReadBoolean()
        {
            var size = sizeof(bool);
            var buffer = TryReceive(size);
            return BitConverter.ToBoolean(buffer);
        }

        public byte ReadByte()
        {
            var size = sizeof(byte);
            var buffer = TryReceive(size);
            return buffer[0];
        }

        public double ReadDouble()
        {
            var size = sizeof(double);
            var buffer = TryReceive(size);
            return BitConverter.ToDouble(buffer);
        }

        public int ReadInt()
        {
            var size = sizeof(int);
            var buffer = TryReceive(size);
            return BitConverter.ToInt32(buffer);
        }

        public long ReadLong()
        {
            var size = sizeof(long);
            var buffer = TryReceive(size);
            return BitConverter.ToInt64(buffer);
        }

        public T ReadObject<T>()
        {
            var size = ReadInt();
            var buffer = TryReceive(size);
            using MemoryStream ms = new(buffer);
            return JsonSerializer.Deserialize<T>(ms);
        }

        public string ReadString()
        {
            var size = ReadInt();
            return _hostSession.Receive(size);
        }

        public void WriteData(bool data)
        {
            var bytes = BitConverter.GetBytes(data);
            _hostSession.SendAsync(bytes);
        }

        public void WriteData(byte data)
        {
            var bytes = BitConverter.GetBytes(data);
            _hostSession.SendAsync(bytes);
        }

        public void WriteData(int data)
        {
            var bytes = BitConverter.GetBytes(data);
            _hostSession.SendAsync(bytes);
        }

        public void WriteData(long data)
        {
            var bytes = BitConverter.GetBytes(data);
            _hostSession.SendAsync(bytes);
        }

        public void WriteData(double data)
        {
            var bytes = BitConverter.GetBytes(data);
            _hostSession.SendAsync(bytes);
        }

        public void WriteData(string data)
        {
            _hostSession.SendAsync(data);
        }

        public void WriteObject<T>(T @object)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(@object);
            WriteData(bytes.Length);
            _hostSession.SendAsync(bytes);
        }

        private byte[] TryReceive(int size)
        {
            var buffer = new byte[size];
            var length = _hostSession.Receive(buffer);

            if (size != length)
            {
                throw new ArgumentException($"Expected to receive {size} bytes, but got {length}.");
            }

            return buffer;
        }
    }
}