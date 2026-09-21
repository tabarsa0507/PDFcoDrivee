using PDFcoDrive.Models;

namespace PDFcoDrive.Services
{
    public interface IGroupService
    {
        Task<List<Group>> GetAllAsync();
        Task<Group?> GetByIdAsync(int id);
        Task<List<Group>> GetByUserAsync(string userId);
        Task<(bool success, string message, int? id)> CreateAsync(Group group);
        Task<(bool success, string message)> UpdateAsync(Group group);
        Task<(bool success, string message)> DeleteAsync(int id);
        bool IsUserInGroup(Group group, string userId);
    }

    public class GroupService : IGroupService
    {
        private readonly IJsonStorageService _storage;

        public GroupService(IJsonStorageService storage)
        {
            _storage = storage;
        }

        public async Task<List<Group>> GetAllAsync()
        {
            return await _storage.GetGroupsAsync();
        }

        public async Task<Group?> GetByIdAsync(int id)
        {
            var groups = await _storage.GetGroupsAsync();
            return groups.FirstOrDefault(g => g.Id == id);
        }

        public async Task<List<Group>> GetByUserAsync(string userId)
        {
            var groups = await _storage.GetGroupsAsync();
            return groups.Where(g => g.MemberIds.Contains(userId)).ToList();
        }

        public async Task<(bool success, string message, int? id)> CreateAsync(Group group)
        {
            if (string.IsNullOrWhiteSpace(group.Name))
                return (false, "نام گروه الزامیست", null);

            var groups = await _storage.GetGroupsAsync();

            if (groups.Any(g => g.Name == group.Name))
                return (false, "گروهی با این نام وجود دارد", null);

            group.Id = groups.Count > 0 ? groups.Max(g => g.Id) + 1 : 1;
            group.CreatedAt = DateTime.Now;

            groups.Add(group);
            await _storage.SaveGroupsAsync(groups);

            return (true, "گروه ساخته شد", group.Id);
        }

        public async Task<(bool success, string message)> UpdateAsync(Group group)
        {
            var groups = await _storage.GetGroupsAsync();
            var existing = groups.FirstOrDefault(g => g.Id == group.Id);
            if (existing == null) return (false, "گروه یافت نشد");

            // چک نام تکراری
            if (groups.Any(g => g.Id != group.Id && g.Name == group.Name))
                return (false, "گروهی با این نام وجود دارد");

            existing.Name = group.Name;
            existing.Description = group.Description;
            existing.Icon = group.Icon;
            existing.Color = group.Color;
            existing.MemberIds = group.MemberIds;

            await _storage.SaveGroupsAsync(groups);
            return (true, "گروه ویرایش شد");
        }

        public async Task<(bool success, string message)> DeleteAsync(int id)
        {
            var groups = await _storage.GetGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Id == id);
            if (group == null) return (false, "گروه یافت نشد");

            groups.Remove(group);
            await _storage.SaveGroupsAsync(groups);

            return (true, "گروه حذف شد");
        }

        public bool IsUserInGroup(Group group, string userId)
        {
            return group.MemberIds.Contains(userId);
        }
    }
}