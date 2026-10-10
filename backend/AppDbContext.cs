using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ProjectPulse.Api;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<LocalSession> Sessions => Set<LocalSession>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
    public DbSet<TaskContributor> TaskContributors => Set<TaskContributor>();
    public DbSet<ProjectIssue> Issues => Set<ProjectIssue>();
    public DbSet<TaskActivity> Activities => Set<TaskActivity>();
    public DbSet<ReqCounter> ReqCounters => Set<ReqCounter>();
    public DbSet<ProjectBaseline> Baselines => Set<ProjectBaseline>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Division>().ToTable("divisions");
        b.Entity<Department>().ToTable("departments").HasOne(x => x.Division).WithMany().HasForeignKey(x => x.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AppUser>().ToTable("users").HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        b.Entity<AppUser>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.ReportsToPmUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReqCounter>().ToTable("req_counters").HasKey(x => x.DateKey);
        b.Entity<LocalSession>().ToTable("local_sessions").HasKey(x => x.Id);
        b.Entity<LocalSession>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<LocalSession>().HasIndex(x => x.ExpiresAt);
        b.Entity<LocalSession>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Project>().ToTable("projects").HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Project>().HasOne(x => x.PmUser).WithMany().HasForeignKey(x => x.PmUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Project>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.TimelineOwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProjectMember>().ToTable("project_members").HasKey(x => new { x.ProjectId, x.UserId });
        b.Entity<ProjectMember>().HasOne(x => x.Project).WithMany(x => x.Members).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProjectMember>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Milestone>().ToTable("milestones").HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProjectTask>().ToTable("tasks").HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProjectTask>().HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProjectTask>().HasOne(x => x.Milestone).WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProjectTask>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<TaskContributor>().ToTable("task_contributors").HasKey(x => new { x.TaskId, x.UserId });
        b.Entity<TaskContributor>().HasOne(x => x.Task).WithMany(x => x.Contributors).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TaskContributor>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProjectIssue>().ToTable("issues").HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProjectIssue>().HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProjectIssue>().HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TaskActivity>().ToTable("task_activities").HasKey(x => x.Id);
        b.Entity<ProjectBaseline>().ToTable("project_baselines").HasKey(x => x.Id);
        b.Entity<Project>().HasIndex(x => x.DepartmentId);
        b.Entity<ProjectTask>().HasIndex(x => new { x.ProjectId, x.DueDate });
        b.Entity<ProjectIssue>().HasIndex(x => new { x.ProjectId, x.Status });
        b.Entity<TaskActivity>().HasIndex(x => new { x.ProjectId, x.CreatedAt });

        // Names match SQL schema PostgreSQL: snake_case, tanpa plugin tambahan.
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(SnakeCase(property.Name));
    }

    private static string SnakeCase(string name)
    {
        var s = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]))) s.Append('_');
            s.Append(char.ToLowerInvariant(name[i]));
        }
        return s.ToString();
    }
}
