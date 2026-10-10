namespace ProjectPulse.Api;

public sealed class Division
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
public sealed class Department
{
    public int Id { get; set; }
    public int DivisionId { get; set; }
    public Division Division { get; set; } = null!;
    public string Name { get; set; } = "";
}
public sealed class AppUser
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "BA";
    public string PasswordHash { get; set; } = "";
    public int? ReportsToPmUserId { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class LocalSession
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Project
{
    public string Id { get; set; } = "";
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int PmUserId { get; set; }
    public int? TimelineOwnerUserId { get; set; }
    public string RequesterName { get; set; } = "";
    public string RequesterDepartment { get; set; } = "";
    public string CurrentStatus { get; set; } = "Requirement";
    public AppUser PmUser { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Active";
    public string Health { get; set; } = "On Track";
    // Progress project berasal dari milestone berbobot yang di-update BA timeline owner, bukan task count
    public int Progress { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly BaselineEnd { get; set; }
    public DateOnly? ForecastEnd { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
}
public sealed class ProjectMember
{
    public string ProjectId { get; set; } = "";
    public Project Project { get; set; } = null!;
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string ProjectRole { get; set; } = "BA";
}
public sealed class Milestone
{
    public int Id { get; set; }
    public string ProjectId { get; set; } = "";
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Weight { get; set; }
    public int Progress { get; set; }
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ForecastEnd { get; set; }
    public string Status { get; set; } = "Not Started";
}
public sealed class ProjectTask
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public Project Project { get; set; } = null!;
    public int? MilestoneId { get; set; }
    public Milestone? Milestone { get; set; }
    public int OwnerUserId { get; set; }
    public AppUser Owner { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "To Do";
    public string Priority { get; set; } = "Medium";
    public DateOnly DueDate { get; set; }
    public int Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public ICollection<TaskContributor> Contributors { get; set; } = new List<TaskContributor>();
}
public sealed class TaskContributor
{
    public string TaskId { get; set; } = "";
    public ProjectTask Task { get; set; } = null!;
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
}
public sealed class ProjectIssue
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public Project Project { get; set; } = null!;
    public string? TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    public int OwnerUserId { get; set; }
    public AppUser Owner { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public string Impact { get; set; } = "";
    public string ActionPlan { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class TaskActivity
{
    public long Id { get; set; }
    public string ProjectId { get; set; } = "";
    public string TaskId { get; set; } = "";
    public int ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string OldValue { get; set; } = "";
    public string NewValue { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class ProjectBaseline
{
    public long Id { get; set; }
    public string ProjectId { get; set; } = "";
    public int Revision { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string Reason { get; set; } = "";
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public sealed class ReqCounter
{
    public string DateKey { get; set; } = "";
    public int LastNumber { get; set; }
}
