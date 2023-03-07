namespace Parcs.Core.Internal
{
    internal static class ByteArrayExtensions
    {
        internal static byte[] Prepend(this byte[] bArray, byte newByte)
        {
            var newArray = new byte[bArray.Length + 1];
            bArray.CopyTo(newArray, 1);
            newArray[0] = newByte;
            return newArray;
        }
    }
}