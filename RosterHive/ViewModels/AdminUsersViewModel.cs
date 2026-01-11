namespace RosterHive.ViewModels;

public class AdminUsersViewModel
{
    public bool IsCurrentRoot { get; set; }
    public string RootEmail { get; set; } = "admin@rosterhive.local";

    public List<UserRow> Users { get; set; } = new();

    public class UserRow
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool IsRoot { get; set; }

        public bool IsLocked { get; set; }
        public string LockInfo { get; set; } = string.Empty;

        public bool IsSelf { get; set; }
    }
}
