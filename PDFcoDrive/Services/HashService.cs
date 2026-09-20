using System.Security.Cryptography;
using System.Text;

namespace PDFcoDrive.Services
{
    public interface IHashService
    {
        (string hash, string salt) HashPassword(string password);
        bool VerifyPassword(string password, string hash, string salt);
        string ComputeSha256(Stream stream);
        string ComputeSha256(string text);
        string GetPepperKey();
    }

    public class HashService : IHashService
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        private readonly string _pepper;

        public HashService(IConfiguration config)
        {
            _pepper = config["Auth:PepperKey"]
                ?? "PDFcoDrive-Default-Pepper-Key-2026-Change-Me";
        }

        public (string hash, string salt) HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            string pepperedPassword = password + _pepper;

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password: pepperedPassword,
                salt: salt,
                iterations: Iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: HashSize);

            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        public bool VerifyPassword(string password, string hash, string salt)
        {
            try
            {
                byte[] saltBytes = Convert.FromBase64String(salt);
                byte[] expectedHash = Convert.FromBase64String(hash);
                string pepperedPassword = password + _pepper;

                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    password: pepperedPassword,
                    salt: saltBytes,
                    iterations: Iterations,
                    hashAlgorithm: HashAlgorithmName.SHA256,
                    outputLength: HashSize);

                return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
            }
            catch { return false; }
        }

        public string ComputeSha256(Stream stream)
        {
            long originalPosition = stream.Position;
            stream.Position = 0;
            byte[] hashBytes = SHA256.HashData(stream);
            stream.Position = originalPosition;
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public string ComputeSha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public string GetPepperKey()
        {
            if (_pepper.Length <= 16) return _pepper;
            return _pepper.Substring(0, 8) + "..." + _pepper.Substring(_pepper.Length - 8);
        }
    }
}