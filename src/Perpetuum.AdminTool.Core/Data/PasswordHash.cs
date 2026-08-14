using System.Security.Cryptography;
using System.Text;

namespace Perpetuum.AdminTool.Data
{
    public static class PasswordHash
    {
        public static string Compute(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            byte[] bytes = Encoding.ASCII.GetBytes(input);
            return Convert.ToHexString(SHA1.HashData(bytes));
        }
    }
}
