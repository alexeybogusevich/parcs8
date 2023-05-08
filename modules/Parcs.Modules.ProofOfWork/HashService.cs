using System.Security.Cryptography;
using System.Text;

namespace Parcs.Modules.ProofOfWork
{
    public static class HashService
    {
        public static string GetHashValue(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLower();
        }
    }
}