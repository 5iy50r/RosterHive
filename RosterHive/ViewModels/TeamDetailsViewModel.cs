namespace RosterHive.ViewModels;

public class TeamDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JoinCode { get; set; } = string.Empty;

    public bool ShowJoinCode { get; set; }
    public bool CanManageMembers { get; set; }
    public bool OwnerMissing { get; set; }
    public bool CanManageTeamRoles { get; set; }
    public string? OwnerUserId { get; set; }

    public List<MemberRow> Members { get; set; } = new();
    public List<UserOption> AvailableUsers { get; set; } = new();

    public class MemberRow
    {
        public int TeamMemberId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public bool IsOwner { get; set; }
    }

    public class UserOption
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
