using PDFcoDrive.Models;

namespace PDFcoDrive.Services
{
    public interface IAuthService
    {
        Task<User?> ValidateUserAsync(string nationalCode, string password);
        Task<User?> GetUserByIdAsync(string userId);
        Task<User?> GetUserByNationalCodeAsync(string nationalCode);
        Task<List<User>> GetAllUsersAsync();
        Task<(bool success, string message)> CreateUserAsync(string nationalCode, string fullName, string password, string role = "User");
        Task<(bool success, string message)> UpdateUserAsync(string userId, string fullName, string role);
        Task<(bool success, string message)> ResetPasswordAsync(string userId, string newPassword);
        Task<bool> DeleteUserAsync(string userId);
        Task<bool> ChangePasswordAsync(string userId, string newPassword);
        Task UpdateLastLoginAsync(string userId);
    }

    public class AuthService : IAuthService
    {
        private readonly IJsonStorageService _storage;
        private readonly IHashService _hash;

        public AuthService(IJsonStorageService storage, IHashService hash)
        {
            _storage = storage;
            _hash = hash;
        }

        public async Task<User?> ValidateUserAsync(string nationalCode, string password)
        {
            var users = await _storage.GetUsersAsync();
            var user = users.FirstOrDefault(u => u.NationalCode == nationalCode && u.IsActive);
            if (user == null) return null;
            if (!_hash.VerifyPassword(password, user.PasswordHash, user.PasswordSalt)) return null;
            return user;
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            var users = await _storage.GetUsersAsync();
            return users.FirstOrDefault(u => u.Id == userId);
        }

        public async Task<User?> GetUserByNationalCodeAsync(string nationalCode)
        {
            var users = await _storage.GetUsersAsync();
            return users.FirstOrDefault(u => u.NationalCode == nationalCode);
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _storage.GetUsersAsync();
        }

        public async Task<(bool success, string message)> CreateUserAsync(
            string nationalCode, string fullName, string password, string role = "User")
        {
            if (string.IsNullOrWhiteSpace(nationalCode) || nationalCode.Length != 10 || !nationalCode.All(char.IsDigit))
                return (false, "Ú©Ø¯ Ù…Ù„ÛŒ Ø¨Ø§ÛŒØ¯ 10 Ø±Ù‚Ù… Ø¹Ø¯Ø¯ÛŒ Ø¨Ø§Ø´Ø¯");

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return (false, "Ø±Ù…Ø² Ø¹Ø¨ÙˆØ± Ø¨Ø§ÛŒØ¯ Ø­Ø¯Ø§Ù‚Ù„ 6 Ú©Ø§Ø±Ø§Ú©ØªØ± Ø¨Ø§Ø´Ø¯");

            var users = await _storage.GetUsersAsync();
            if (users.Any(u => u.NationalCode == nationalCode))
                return (false, "Ø§ÛŒÙ† Ú©Ø¯ Ù…Ù„ÛŒ Ù‚Ø¨Ù„Ø§Ù‹ Ø«Ø¨Øª Ø´Ø¯Ù‡ Ø§Ø³Øª");

            var (hash, salt) = _hash.HashPassword(password);

            var newUser = new User
            {
                NationalCode = nationalCode,
                FullName = fullName,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = role,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            users.Add(newUser);
            await _storage.SaveUsersAsync(users);
            return (true, "Ú©Ø§Ø±Ø¨Ø± Ø¨Ø§ Ù…ÙˆÙÙ‚ÛŒØª Ø³Ø§Ø®ØªÙ‡ Ø´Ø¯");
        }

        public async Task<(bool success, string message)> UpdateUserAsync(string userId, string fullName, string role)
        {
            var users = await _storage.GetUsersAsync();
            var user = users.FirstOrDefault(u => u.Id == userId);

            if (user == null) return (false, "Ú©Ø§Ø±Ø¨Ø± ÛŒØ§ÙØª Ù†Ø´Ø¯");
            if (string.IsNullOrWhiteSpace(fullName)) return (false, "Ù†Ø§Ù… Ø§Ù„Ø²Ø§Ù…ÛŒØ³Øª");
            if (user.NationalCode == "2122767111" && role != "Admin")
                return (false, "Ù†Ù‚Ø´ Ø§Ø¯Ù…ÛŒÙ† Ø§ØµÙ„ÛŒ Ù‚Ø§Ø¨Ù„ ØªØºÛŒÛŒØ± Ù†ÛŒØ³Øª");

            user.FullName = fullName;
            user.Role = role;
            await _storage.SaveUsersAsync(users);
            return (true, "Ø§Ø·Ù„Ø§Ø¹Ø§Øª Ú©Ø§Ø±Ø¨Ø± Ø¨Ù‡â€ŒØ±ÙˆØ²Ø±Ø³Ø§Ù†ÛŒ Ø´Ø¯");
        }

        public async Task<(bool success, string message)> ResetPasswordAsync(string userId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return (false, "Ø±Ù…Ø² Ø¹Ø¨ÙˆØ± Ø¨Ø§ÛŒØ¯ Ø­Ø¯Ø§Ù‚Ù„ 6 Ú©Ø§Ø±Ø§Ú©ØªØ± Ø¨Ø§Ø´Ø¯");

            var users = await _storage.GetUsersAsync();
            var user = users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return (false, "Ú©Ø§Ø±Ø¨Ø± ÛŒØ§ÙØª Ù†Ø´Ø¯");

            var (hash, salt) = _hash.HashPassword(newPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            await _storage.SaveUsersAsync(users);
            return (true, "Ø±Ù…Ø² Ø¹Ø¨ÙˆØ± ØªØºÛŒÛŒØ± Ú©Ø±Ø¯");
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            var users = await _storage.GetUsersAsync();
            var user = users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;
            if (user.NationalCode == "2122767111") return false;
            users.Remove(user);
            await _storage.SaveUsersAsync(users);
            return true;
        }

        public async Task<bool> ChangePasswordAsync(string userId, string newPassword)
        {
            var users = await _storage.GetUsersAsync();
            var user = users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            var (hash, salt) = _hash.HashPassword(newPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            await _storage.SaveUsersAsync(users);
            return true;
        }

        public async Task UpdateLastLoginAsync(string userId)
        {
            var users = await _storage.GetUsersAsync();
            var user = users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.LastLoginAt = DateTime.Now;
                await _storage.SaveUsersAsync(users);
            }
        }
    }
}