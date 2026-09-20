using System.Security.Cryptography;
using System.Text;

namespace PDFcoDrive.Services
{
    public interface ICaptchaService
    {
        string GenerateCode(int length = 5);
        string HashCode(string code);
    }

    public class CaptchaService : ICaptchaService
    {
        private const string Chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        public string GenerateCode(int length = 5)
        {
            var result = new StringBuilder(length);
            var buffer = new byte[length];
            RandomNumberGenerator.Fill(buffer);
            for (int i = 0; i < length; i++)
                result.Append(Chars[buffer[i] % Chars.Length]);
            return result.ToString();
        }

        public string HashCode(string code)
        {
            var bytes = Encoding.UTF8.GetBytes(code.ToUpperInvariant());
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}