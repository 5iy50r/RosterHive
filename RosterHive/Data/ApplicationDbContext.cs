using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RosterHive.Models;

namespace RosterHive.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();

    public DbSet<TimeOffRequest> TimeOffRequests => Set<TimeOffRequest>();
    public DbSet<TimeOffRequestEvent> TimeOffRequestEvents => Set<TimeOffRequestEvent>();

    public DbSet<ShiftSwapRequest> ShiftSwapRequests => Set<ShiftSwapRequest>();
    public DbSet<ShiftSwapRequestEvent> ShiftSwapRequestEvents => Set<ShiftSwapRequestEvent>();

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ShiftAssignment>()
            .HasOne(sa => sa.Shift)
            .WithMany(s => s.Assignments)
            .HasForeignKey(sa => sa.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TimeOffRequestEvent>()
            .HasOne(e => e.TimeOffRequest)
            .WithMany(r => r.Events)
            .HasForeignKey(e => e.TimeOffRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ShiftSwapRequestEvent>()
            .HasOne(e => e.ShiftSwapRequest)
            .WithMany(r => r.Events)
            .HasForeignKey(e => e.ShiftSwapRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TaskComment>()
            .HasOne(c => c.TaskItem)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TaskItem>()
            .HasOne(t => t.Shift)
            .WithMany()
            .HasForeignKey(t => t.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
