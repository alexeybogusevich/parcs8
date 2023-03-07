using Parcs.Core.Internal;
using System.Text;
using System.Text.Json;

namespace Parcs.Core
{
    public sealed class Channel : IChannel
    {
        private readonly ITransmissonManager _transmissonManager;

        public Channel(ITransmissonManager transmissonManager)
        {
            _transmissonManager = transmissonManager;
        }

        public Signal ReadSignal()
        {
            var @byte = TryReceiveSignal();
            return (Signal)@byte;
        }

        public bool ReadBoolean()
        {
            var size = sizeof(bool);
            var buffer = TryReceiveData(size);
            return BitConverter.ToBoolean(buffer);
        }

        public byte ReadByte()
        {
            var size = sizeof(byte);
            var buffer = TryReceiveData(size);
            return buffer[0];
        }

        public double ReadDouble()
        {
            var size = sizeof(double);
            var buffer = TryReceiveData(size);
            return BitConverter.ToDouble(buffer);
        }

        public int ReadInt()
        {
            var size = sizeof(int);
            var buffer = TryReceiveData(size);
            return BitConverter.ToInt32(buffer);
        }

        public long ReadLong()
        {
            var size = sizeof(long);
            var buffer = TryReceiveData(size);
            return BitConverter.ToInt64(buffer);
        }

        public T ReadObject<T>()
        {
            var size = ReadInt();
            var buffer = TryReceiveData(size);
            using MemoryStream ms = new(buffer.ToArray());
            return JsonSerializer.Deserialize<T>(ms);
        }

        public string ReadString()
        {
            var size = ReadInt();
            var buffer = TryReceiveData(size);
            return Encoding.UTF8.GetString(buffer);
        }

        public void WriteSignal(Signal signal)
        {
            var bytes = new byte[] { (byte)signal };
            _transmissonManager.Send(bytes);
        }

        public void WriteData(bool data)
        {
            var bytes = BitConverter.GetBytes(data);
            var bytesWithSignal = bytes.Prepend((byte)Signal.TransmitData);
            _transmissonManager.Send(bytesWithSignal);
        }

        public void WriteData(byte data)
        {
            var bytes = new byte[] { data };
            var bytesWithSignal = bytes.Prepend((byte)Signal.TransmitData);
            _transmissonManager.Send(bytesWithSignal);
        }

        public void WriteData(int data)
        {
            var bytes = BitConverter.GetBytes(data);
            var bytesWithSignal = bytes.Prepend((byte)Signal.TransmitData);
            _transmissonManager.Send(bytesWithSignal);
        }

        public void WriteData(long data)
        {
            var bytes = BitConverter.GetBytes(data);
            var bytesWithSignal = bytes.Prepend((byte)Signal.TransmitData);
            _transmissonManager.Send(bytesWithSignal);
        }

        public void WriteData(double data)
        {
            var bytes = BitConverter.GetBytes(data);
            var bytesWithSignal = bytes.Prepend((byte)Signal.TransmitData);
            _transmissonManager.Send(bytesWithSignal);
        }

        public void WriteData(string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            WriteData(bytes.Length);
            var bytesWithSignal = bytes.Prepend((byte)Signal.TransmitData);
            _transmissonManager.Send(bytesWithSignal);
        }

        public void WriteObject<T>(T @object)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(@object);
            WriteData(bytes.Length);
            var bytesWithSignal = bytes.Prepend((byte)Signal.TransmitData);
            _transmissonManager.Send(bytesWithSignal);
        }

        private Span<byte> TryReceiveData(int size)
        {
            var sizeAfterSignal = sizeof(Signal) + size;

            var buffer = new byte[sizeAfterSignal];
            var length = _transmissonManager.Receive(buffer);

            if (length != sizeAfterSignal)
            {
                throw new ArgumentException($"Expected to receive {size} bytes, but got {length}.");
            }

            return buffer.AsSpan()[1..];
        }

        private byte TryReceiveSignal()
        {
            var buffer = new byte[sizeof(Signal)];
            var length = _transmissonManager.Receive(buffer);

            if (length != sizeof(Signal))
            {
                throw new ArgumentException($"Expected to receive {sizeof(Signal)} bytes, but got {length}.");
            }

            return buffer[0];
        }
    }
}