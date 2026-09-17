using System;
using System.Security.Cryptography;
using System.Text;

namespace TFTAnalyzer.Serialization
{
    public static class ContentHasher
    {
        public static string Sha256Hex(string utf8Text)
        {
            if (utf8Text == null)
            {
                throw new ArgumentNullException(nameof(utf8Text));
            }

            return Sha256Hex(Encoding.UTF8.GetBytes(utf8Text));
        }

        public static string Sha256Hex(ReadOnlySpan<byte> bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes.ToArray());
                var result = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    result.Append(value.ToString("x2"));
                }

                return result.ToString();
            }
        }
    }
}
