using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using ProjectPulse.Api;

var builder = WebApplication.CreateBuilder(args);
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var mode = builder.Configuration["Security:Mode"] ?? "Local";
if (provider is not ("Sqlite" or "Postgres")) throw new InvalidOperationException("Database:Provider must be Sqlite or Postgres.");
if (mode is not ("Local" or "Jwt")) throw new InvalidOperationException("Security:Mode must be Local or Jwt.");
if (mode == "Local" && (provider != "Sqlite" || !builder.Environment.IsDevelopment()))
    throw new InvalidOperationException("Local login is allowed ONLY with SQLite in local Development. PostgreSQL must use JWT.");
if (mode == "Jwt")
{
    if (string.IsNullOrWhiteSpace(builder.Configuration["Security:JwtAuthority"]) || string.IsNullOrWhiteSpace(builder.Configuration["Security:JwtAudience"]))
        throw new InvalidOperationException("JWT authority and audience are required.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Security:JwtAuthority"];
        o.Audience = builder.Configuration["Security:JwtAudience"];
        o.RequireHttpsMetadata = true;
    });
    builder.Services.AddAuthorization();
}
builder.Services.AddDbContext<AppDbContext>(opts =>
{
    if (provider == "Sqlite") opts.UseSqlite(builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=Data/projectpulse.db");
    else opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Missing PostgreSQL connection string"));
});

// Rate-limit local password login and unauthenticated demo reset.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("local-auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));
});

var app = builder.Build();
if (provider == "Sqlite")
{
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data"));
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync(); // bootstrap lokal saja, tidak dipakai di PostgreSQL
    if (mode == "Local") await LocalUpgrade.RunAsync(db);
    await SeedData.InitializeAsync(db);
}
app.UseRouting();
app.UseRateLimiter();
if (mode == "Jwt") { app.UseAuthentication(); app.UseAuthorization(); }

app.MapGet("/api/health", () => Results.Ok(new { service="ProjectPulse API", provider, utc=DateTime.UtcNow }));
// These endpoints are intentionally unavailable on PostgreSQL / hosted deployments.
if (mode == "Local")
{
    var localAuth = app.MapGroup("/api/auth").RequireRateLimiting("local-auth");
    localAuth.AddEndpointFilter(async (invocation, next) =>
        invocation.HttpContext.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip)
            ? await next(invocation) : Results.StatusCode(403));
    localAuth.MapGet("/demo-users", async (AppDbContext db) => Results.Ok(
        await db.Users.AsNoTracking().OrderBy(u => u.Id)
            .Select(u => new { u.FullName, u.Email, u.Role }).ToListAsync()));
    localAuth.MapPost("/login", async Task<IResult> (LoginRequest req, AppDbContext db) =>
    {
        var email = req.Email?.Trim().ToLowerInvariant() ?? "";
        if (email.Length > 200 || req.Password is null || req.Password.Length > 256) return Results.BadRequest(new { error = "Invalid input" });
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        if (user is null || !LocalAuth.Verify(req.Password, user.PasswordHash))
            return Results.Json(new { error = "Email atau password salah." }, statusCode: 401);
        var token = LocalAuth.NewToken();
        var expires = DateTime.UtcNow.AddHours(8);
        db.Sessions.Add(new LocalSession { UserId = user.Id, TokenHash = LocalAuth.HashToken(token), ExpiresAt = expires });
        await db.SaveChangesAsync();
        return Results.Ok(new { token, expiresAt = expires });
    });
    localAuth.MapPost("/reset", async Task<IResult> (ResetPasswordRequest req, AppDbContext db) =>
    {
        var email = req.Email?.Trim().ToLowerInvariant() ?? "";
        if (email.Length is < 3 or > 200 || req.NewPassword is null || req.NewPassword.Length < 10 || req.NewPassword.Length > 128)
            return Results.BadRequest(new { error = "Password minimal 10 karakter (maksimum 128)." });
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return Results.Ok(new { message = "Jika akun ditemukan, password telah direset." });
        user.PasswordHash = LocalAuth.HashPassword(req.NewPassword);
        var sessions = await db.Sessions.Where(x => x.UserId == user.Id).ToListAsync();
        db.Sessions.RemoveRange(sessions);
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "Password demo direset. Silakan login dengan password baru." });
    });
    localAuth.MapPost("/logout", async Task<IResult> (HttpContext http, AppDbContext db) =>
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var hash = LocalAuth.HashToken(authorization[7..].Trim());
            var session = await db.Sessions.FirstOrDefaultAsync(s => s.TokenHash == hash);
            if (session is not null) { db.Sessions.Remove(session); await db.SaveChangesAsync(); }
        }
        return Results.Ok(new { ok = true });
    });
}

var api = app.MapGroup("/api");
if (mode == "Jwt") api.RequireAuthorization();
api.AddEndpointFilter(async (invocation, next) =>
{
    var http = invocation.HttpContext;
    var db = http.RequestServices.GetRequiredService<AppDbContext>();
    AppUser? current;
    if (mode == "Local")
    {
        if (http.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip)) return Results.StatusCode(403);
        var authHeader = http.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return Results.Unauthorized();
        var token = authHeader[7..].Trim();
        if (token.Length is < 32 or > 256) return Results.Unauthorized();
        var hash = LocalAuth.HashToken(token);
        var session = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.TokenHash == hash && s.ExpiresAt > DateTime.UtcNow);
        current = session is null ? null : await db.Users.AsNoTracking().Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == session.UserId && x.IsActive);
    }
    else
    {
        var email = http.User.FindFirst("preferred_username")?.Value
                    ?? http.User.FindFirst("upn")?.Value
                    ?? http.User.FindFirst(ClaimTypes.Email)?.Value;
        current = string.IsNullOrEmpty(email) ? null : await db.Users.AsNoTracking().Include(x => x.Department).FirstOrDefaultAsync(x => x.Email == email && x.IsActive);
    }
    if (current == null) return Results.Unauthorized();
    http.Items["CurrentUser"] = current;
    return await next(invocation);
});

api.MapGet("/me", (HttpContext http) =>
{
    var u = Current(http);
    return Results.Ok(new { u.Id, u.FullName, u.Email, u.Role, u.DepartmentId, u.ReportsToPmUserId });
});
api.MapGet("/users", async (HttpContext http, AppDbContext db) =>
{
    var u = Current(http);
    return Results.Ok(await db.Users.Include(x => x.Department)
        .Where(x => x.Department.DivisionId == u.Department.DivisionId)
        .OrderBy(x => x.FullName).Select(x => new { x.Id, x.FullName, x.Email, x.Role, x.DepartmentId, x.ReportsToPmUserId, x.IsActive }).ToListAsync());
});
api.MapGet("/departments", async (HttpContext http, AppDbContext db) =>
{
    var u = Current(http);
    return Results.Ok(await db.Departments.Where(x => x.DivisionId == u.Department.DivisionId)
        .Select(x => new { x.Id, x.Name }).ToListAsync());
});
api.MapGet("/projects", async (HttpContext http, AppDbContext db) =>
{
    var p = await VisibleProjects(db, Current(http)).Include(x => x.Department).Include(x => x.PmUser).OrderBy(x => x.Name).ToListAsync();
    return Results.Ok(p.Select(ProjectSummary).ToList());
});
api.MapGet("/portfolio", async (HttpContext http, AppDbContext db) =>
{
    var projects = await VisibleProjects(db, Current(http)).Include(x => x.Department).Include(x => x.PmUser).ToListAsync();
    var ids = projects.Select(x => x.Id).ToArray();
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var overdue = await db.Tasks.CountAsync(t => ids.Contains(t.ProjectId) && t.DueDate < today && t.Status != "Done" && t.Status != "Cancelled");
    var openIssues = await db.Issues.CountAsync(x => ids.Contains(x.ProjectId) && x.Status != "Resolved");
    return Results.Ok(new
    {
        total = projects.Count,
        onTrack = projects.Count(p => p.Health == "On Track"),
        atRisk = projects.Count(p => p.Health == "At Risk"),
        delayed = projects.Count(p => p.Health == "Delayed"),
        blocked = projects.Count(p => p.Health == "Blocked"),
        overdueTasks = overdue,
        openIssues,
        departments = projects.GroupBy(p => p.Department.Name).Select(g => new
        {
            name=g.Key, total=g.Count(), onTrack=g.Count(p=>p.Health=="On Track"),
            atRisk=g.Count(p=>p.Health=="At Risk"), delayed=g.Count(p=>p.Health=="Delayed"),
            blocked=g.Count(p=>p.Health=="Blocked")
        }).OrderBy(g => g.name).ToArray(),
        projects = projects.Select(ProjectSummary).ToArray()
    });
});
api.MapGet("/tasks/my", async (HttpContext http, AppDbContext db) =>
{
    var user=Current(http);
    var ids=await VisibleProjects(db,user).Select(p=>p.Id).ToArrayAsync();
    var tasks=await db.Tasks.Include(t=>t.Owner).Include(t=>t.Contributors).Where(t=>ids.Contains(t.ProjectId) &&
        (t.OwnerUserId==user.Id || t.Contributors.Any(c=>c.UserId==user.Id)))
        .OrderBy(t=>t.DueDate).ToListAsync();
    return Results.Ok(tasks.Select(TaskDto));
});
api.MapGet("/projects/{projectId}", async Task<IResult> (string projectId, HttpContext http, AppDbContext db) =>
{
    var project=await VisibleProjects(db,Current(http)).Include(p=>p.Department).Include(p=>p.PmUser)
        .Include(p=>p.Members).ThenInclude(m=>m.User).FirstOrDefaultAsync(p=>p.Id==projectId);
    if(project==null) return Results.NotFound();
    var milestones=await db.Milestones.Where(m=>m.ProjectId==projectId).OrderBy(m=>m.PlannedStart).ToListAsync();
    var tasks=await db.Tasks.Include(t=>t.Owner).Include(t=>t.Milestone).Include(t=>t.Contributors)
        .Where(t=>t.ProjectId==projectId).OrderBy(t=>t.DueDate).ToListAsync();
    var issues=await db.Issues.Include(i=>i.Owner).Where(i=>i.ProjectId==projectId).OrderBy(i=>i.DueDate).ToListAsync();
    var activities=await db.Activities.Where(a=>a.ProjectId==projectId).OrderByDescending(a=>a.CreatedAt)
        .Take(30).ToListAsync();
    return Results.Ok(new {project=ProjectSummary(project),
        members=project.Members.Select(m=>new { m.UserId, m.User.FullName, m.ProjectRole }),
        milestones=milestones.Select(m=>new {m.Id,m.Name,m.Weight,m.Progress,m.PlannedStart,m.PlannedEnd,m.ForecastEnd,m.Status}),
        tasks=tasks.Select(TaskDto),
        issues=issues.Select(i=>new {i.Id,i.ProjectId,i.TaskId,i.Title,i.Severity,i.Status,i.Impact,i.ActionPlan,i.DueDate,i.OwnerUserId,owner=i.Owner.FullName}),
        activities=activities.Select(a=>new {a.Id,a.TaskId,a.Action,a.OldValue,a.NewValue,a.Note,a.ActorUserId,a.CreatedAt})});
});
api.MapPost("/projects", async Task<IResult> (NewProject req, HttpContext http, AppDbContext db) =>
{
    var user=Current(http);
    if(user.Role!="PM") return Results.StatusCode(403);
    if(string.IsNullOrWhiteSpace(req.Name) || req.Name.Trim().Length > 180 || req.BaselineEnd<req.StartDate ||
       string.IsNullOrWhiteSpace(req.RequesterName) || req.RequesterName.Length>150 ||
       string.IsNullOrWhiteSpace(req.RequesterDepartment) || req.RequesterDepartment.Length>150 ||
       !await db.Departments.AnyAsync(x=>x.Id==req.DepartmentId && x.DivisionId==user.Department.DivisionId))
        return Results.BadRequest(new {error="Invalid project details or HO requester"});
    var ba=await db.Users.Include(x=>x.Department).FirstOrDefaultAsync(x=>x.Id==req.TimelineOwnerUserId);
    if(ba==null||ba.Role!="BA"||!ba.IsActive||ba.Department.DivisionId!=user.Department.DivisionId)
        return Results.BadRequest(new {error="A valid BA must be assigned as timeline owner"});
    // Sequence is stored in database and part of the same transaction as the project.
    await using var tx=await db.Database.BeginTransactionAsync();
    var dateKey=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMdd");
    var counter=await db.ReqCounters.FirstOrDefaultAsync(x=>x.DateKey==dateKey);
    if(counter==null)
    {
        var max=await db.Projects.Where(p=>p.Id.StartsWith("REQ"+dateKey)).Select(p=>p.Id).ToListAsync();
        var last=max.Select(x=>int.TryParse(x[11..],out var n)?n:0).DefaultIfEmpty(0).Max();
        counter=new ReqCounter{DateKey=dateKey,LastNumber=last};db.ReqCounters.Add(counter);
    }
    if(counter.LastNumber>=999)return Results.Conflict(new {error="Daily REQ ID capacity (999) reached"});
    counter.LastNumber++;
    var id=$"REQ{dateKey}{counter.LastNumber:000}";
    var p=new Project {Id=id,Name=req.Name.Trim(),Description=req.Description?.Trim()??"",
        DepartmentId=req.DepartmentId,PmUserId=user.Id,TimelineOwnerUserId=ba.Id,
        RequesterName=req.RequesterName.Trim(),RequesterDepartment=req.RequesterDepartment.Trim(),
        CurrentStatus="Requirement",StartDate=req.StartDate,BaselineEnd=req.BaselineEnd,ForecastEnd=req.BaselineEnd};
    db.Projects.Add(p);
    db.ProjectMembers.AddRange(new ProjectMember{ProjectId=id,UserId=user.Id,ProjectRole="PM"},
        new ProjectMember{ProjectId=id,UserId=ba.Id,ProjectRole="BA"});
    try {await db.SaveChangesAsync();await tx.CommitAsync();}
    catch(DbUpdateException){return Results.Conflict(new{error="REQ ID allocation conflict; retry"});}
    return Results.Created($"/api/projects/{id}",new {p.Id});
});
api.MapPost("/projects/{projectId}/tasks", async Task<IResult> (string projectId, NewTask req, HttpContext http, AppDbContext db) =>
{
    var user=Current(http);
    var project=await VisibleProjects(db,user).FirstOrDefaultAsync(p=>p.Id==projectId);
    if(project==null)return Results.NotFound();
    if(user.Role!="BA" || !IsProjectBA(db,user,projectId))return Results.StatusCode(403);
    if(string.IsNullOrWhiteSpace(req.Title)||req.Title.Length>200||!Validation.Priorities.Contains(req.Priority))return Results.BadRequest(new{error="Invalid task"});
    if(user.Role=="BA" && req.OwnerUserId!=user.Id)return Results.StatusCode(403);
    if(!await db.ProjectMembers.AnyAsync(x=>x.ProjectId==projectId&&x.UserId==req.OwnerUserId))return Results.BadRequest(new{error="Owner must be a project member"});
    if(req.MilestoneId is { } mid && !await db.Milestones.AnyAsync(m=>m.ProjectId==projectId&&m.Id==mid))return Results.BadRequest(new{error="Milestone outside project"});
    var task=new ProjectTask{Id="TSK-"+Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),ProjectId=projectId,Title=req.Title.Trim(),Description=req.Description??"",OwnerUserId=req.OwnerUserId,
        Priority=req.Priority,DueDate=req.DueDate,MilestoneId=req.MilestoneId};
    db.Tasks.Add(task);
    db.Activities.Add(new TaskActivity{TaskId=task.Id,ProjectId=projectId,ActorUserId=user.Id,Action="Created",NewValue=task.Title});
    await db.SaveChangesAsync();return Results.Created($"/api/projects/{projectId}",new{id=task.Id});
});
api.MapPatch("/tasks/{taskId}", async Task<IResult> (string taskId, UpdateTask req, HttpContext http, AppDbContext db) =>
{
    var user=Current(http);
    var task=await db.Tasks.Include(t=>t.Owner).Include(t=>t.Contributors).FirstOrDefaultAsync(t=>t.Id==taskId);
    if(task==null||!await VisibleProjects(db,user).AnyAsync(p=>p.Id==task.ProjectId))return Results.NotFound();
    var isContributor=task.Contributors.Any(x=>x.UserId==user.Id);
    if(!(user.Role=="BA" && (task.OwnerUserId==user.Id||isContributor)))return Results.StatusCode(403);
    if(!Validation.TaskStatuses.Contains(req.Status))return Results.BadRequest(new{error="Invalid status"});
    if(req.Note?.Length>1000)return Results.BadRequest(new{error="Note too long"});
    if(req.Version!=task.Version)return Results.Conflict(new{error="Task has changed. Refresh first.",currentVersion=task.Version});
    // Assigned BA owns task dates and progress.
    var old=task.Status;
    task.Status=req.Status;task.Version++;task.UpdatedAt=DateTime.UtcNow;
    task.CompletedAt=req.Status=="Done"?DateTime.UtcNow:null;
    if(req.DueDate is { } due)task.DueDate=due;
    db.Activities.Add(new TaskActivity{TaskId=task.Id,ProjectId=task.ProjectId,ActorUserId=user.Id,Action="Updated status",OldValue=old,NewValue=req.Status,Note=(req.Note??"").Trim()});
    try {await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return Results.Conflict(new{error="Concurrent update detected"});}
    return Results.Ok(TaskDto(task));
});
api.MapPost("/projects/{projectId}/issues", async Task<IResult> (string projectId, NewIssue req, HttpContext http, AppDbContext db) =>
{
    var user=Current(http);
    var project=await VisibleProjects(db,user).FirstOrDefaultAsync(p=>p.Id==projectId);
    if(project==null)return Results.NotFound();
    if(user.Role!="BA" || !IsProjectBA(db,user,projectId))return Results.StatusCode(403);
    if(string.IsNullOrWhiteSpace(req.Title)||req.Title.Length>200||!Validation.Severities.Contains(req.Severity))return Results.BadRequest(new{error="Invalid issue"});
    if(req.TaskId!=null&&!await db.Tasks.AnyAsync(t=>t.Id==req.TaskId&&t.ProjectId==projectId))return Results.BadRequest(new{error="Task outside project"});
    var issue=new ProjectIssue{Id="ISS-"+Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),ProjectId=projectId,Title=req.Title.Trim(),Severity=req.Severity,
        OwnerUserId=user.Id,Status="Open",TaskId=req.TaskId,Impact=req.Impact??"",ActionPlan=req.ActionPlan??"",DueDate=req.DueDate};
    db.Issues.Add(issue);await db.SaveChangesAsync();return Results.Created($"/api/projects/{projectId}",new{id=issue.Id});
});
api.MapPatch("/issues/{issueId}", async Task<IResult> (string issueId, UpdateIssue req, HttpContext http, AppDbContext db) =>
{
    var user=Current(http);
    var issue=await db.Issues.FirstOrDefaultAsync(i=>i.Id==issueId);
    if(issue==null||!await VisibleProjects(db,user).AnyAsync(p=>p.Id==issue.ProjectId))return Results.NotFound();
    if(user.Role!="BA" || issue.OwnerUserId!=user.Id)return Results.StatusCode(403);
    if(!Validation.IssueStatuses.Contains(req.Status))return Results.BadRequest(new{error="Invalid issue status"});
    issue.Status=req.Status;issue.ActionPlan=req.ActionPlan??issue.ActionPlan;issue.UpdatedAt=DateTime.UtcNow;
    await db.SaveChangesAsync();return Results.Ok(new{issue.Id,issue.Status,issue.ActionPlan});
});


// Timeline-owner BA records health with justification; executive health is separate from phase.
api.MapPatch("/projects/{projectId}/health", async Task<IResult> (string projectId, UpdateProjectHealth req, HttpContext http, AppDbContext db) =>
{
    var u=Current(http);
    var project=await VisibleProjects(db,u).FirstOrDefaultAsync(x=>x.Id==projectId);
    if(project==null)return Results.NotFound();
    if(u.Role!="BA"||project.TimelineOwnerUserId!=u.Id)return Results.StatusCode(403);
    if(!Validation.ProjectHealth.Contains(req.Health)||string.IsNullOrWhiteSpace(req.Reason)||req.Reason.Length>1000)
        return Results.BadRequest(new{error="Valid health and reason are required"});
    var previous=project.Health;
    project.Health=req.Health;project.UpdatedAt=DateTime.UtcNow;
    db.Activities.Add(new TaskActivity {ProjectId=projectId,TaskId="",ActorUserId=u.Id,
        Action="Project health",OldValue=previous,NewValue=req.Health,Note=req.Reason.Trim()});
    await db.SaveChangesAsync();return Results.Ok(new{project.Id,project.Health});
});
// PM assigns staff to a project. Multi-BA / multi-project relationships use project_members.
api.MapPost("/projects/{projectId}/members", async Task<IResult> (string projectId, MemberRequest req, HttpContext http, AppDbContext db) =>
{
    var u=Current(http);
    var project=await VisibleProjects(db,u).FirstOrDefaultAsync(x=>x.Id==projectId);
    if(project==null) return Results.NotFound();
    if(u.Role!="PM" || project.PmUserId!=u.Id) return Results.StatusCode(403);
    var target=await db.Users.FirstOrDefaultAsync(x=>x.Id==req.UserId);
    if(target==null || target.Role!="BA" || !target.IsActive || !await db.Departments.AnyAsync(d=>d.Id==target.DepartmentId&&d.DivisionId==u.Department.DivisionId)) return Results.BadRequest(new {error="Invalid user"});
    if(await db.ProjectMembers.AnyAsync(x=>x.ProjectId==projectId&&x.UserId==target.Id))return Results.Conflict(new {error="Already a project member"});
    db.ProjectMembers.Add(new ProjectMember{ProjectId=projectId,UserId=target.Id,ProjectRole=target.Role});
    await db.SaveChangesAsync();return Results.Ok(new{projectId,req.UserId});
});
api.MapPost("/projects/{projectId}/milestones", async Task<IResult> (string projectId, MilestoneRequest req, HttpContext http, AppDbContext db) =>
{
    var u=Current(http);
    var project=await VisibleProjects(db,u).FirstOrDefaultAsync(x=>x.Id==projectId);
    if(project==null)return Results.NotFound();
    if(u.Role!="BA"||project.TimelineOwnerUserId!=u.Id)return Results.StatusCode(403);
    var total=await db.Milestones.Where(x=>x.ProjectId==projectId).SumAsync(x=>x.Weight);
    if(string.IsNullOrWhiteSpace(req.Name)||req.Weight<=0||total+req.Weight>100||req.PlannedEnd<req.PlannedStart)
        return Results.BadRequest(new{error="Invalid milestone or total weight exceeds 100"});
    var m=new Milestone{ProjectId=projectId,Name=req.Name.Trim(),Weight=req.Weight,PlannedStart=req.PlannedStart,PlannedEnd=req.PlannedEnd,ForecastEnd=req.PlannedEnd};
    db.Milestones.Add(m);await db.SaveChangesAsync();return Results.Ok(new{m.Id,m.Name,m.Weight});
});
api.MapPatch("/milestones/{milestoneId:int}/progress", async Task<IResult> (int milestoneId, MilestoneProgress req, HttpContext http, AppDbContext db) =>
{
    var u=Current(http);
    var m=await db.Milestones.FirstOrDefaultAsync(x=>x.Id==milestoneId);
    if(m==null||!await VisibleProjects(db,u).AnyAsync(x=>x.Id==m.ProjectId))return Results.NotFound();
    var p=await db.Projects.FirstAsync(x=>x.Id==m.ProjectId);
    if(u.Role!="BA"||p.TimelineOwnerUserId!=u.Id)return Results.StatusCode(403);
    if(req.Progress<0||req.Progress>100)return Results.BadRequest(new{error="Progress must be 0..100"});
    var oldProgress=m.Progress;
    m.Progress=req.Progress;m.Status=req.Progress==100?"Done":req.Progress>0?"In Progress":"Not Started";
    db.Activities.Add(new TaskActivity {ProjectId=m.ProjectId,TaskId="",ActorUserId=u.Id,
        Action="Milestone progress",OldValue=$"{m.Name}: {oldProgress}%",NewValue=$"{m.Name}: {m.Progress}%"});
    await db.SaveChangesAsync();
    var all=await db.Milestones.Where(x=>x.ProjectId==p.Id).ToListAsync();
    if(all.Sum(x=>x.Weight)==100)
    {
        p.Progress=all.Sum(x=>x.Weight*x.Progress)/100;
        p.UpdatedAt=DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
    return Results.Ok(new{m.Id,m.Progress,projectProgress=p.Progress,weightTotal=all.Sum(x=>x.Weight)});
});

// Assigned timeline-owner BA can revise forecast dates, status, and milestone progress.
api.MapPatch("/milestones/{milestoneId:int}/dates",async Task<IResult>(int milestoneId,UpdateMilestoneDates req,HttpContext http,AppDbContext db)=>
{
    var u=Current(http);var m=await db.Milestones.FirstOrDefaultAsync(x=>x.Id==milestoneId);
    if(m==null||!await VisibleProjects(db,u).AnyAsync(p=>p.Id==m.ProjectId))return Results.NotFound();
    if(u.Role!="BA"||!await db.Projects.AnyAsync(p=>p.Id==m.ProjectId&&p.TimelineOwnerUserId==u.Id))return Results.StatusCode(403);
    if(req.PlannedEnd<req.PlannedStart || req.ForecastEnd<req.PlannedStart || req.Reason?.Trim().Length is null or < 3 or > 1000)
        return Results.BadRequest(new{error="Invalid dates / reason"});
    var previous=$"{m.PlannedStart}..{m.PlannedEnd} -> {m.ForecastEnd}";
    m.PlannedStart=req.PlannedStart;m.PlannedEnd=req.PlannedEnd;m.ForecastEnd=req.ForecastEnd;
    var project=await db.Projects.FirstAsync(x=>x.Id==m.ProjectId);
    project.ForecastEnd=(await db.Milestones.Where(x=>x.ProjectId==m.ProjectId).ToListAsync())
        .Select(x=>x.Id==m.Id?req.ForecastEnd:(x.ForecastEnd??x.PlannedEnd)).Max();
    project.UpdatedAt=DateTime.UtcNow;
    db.Activities.Add(new TaskActivity {ProjectId=m.ProjectId,TaskId="",ActorUserId=u.Id,Action="Milestone dates",OldValue=previous,
        NewValue=$"{m.PlannedStart}..{m.PlannedEnd} -> {m.ForecastEnd}",Note=req.Reason.Trim()});
    await db.SaveChangesAsync();return Results.Ok(new{m.Id,m.PlannedStart,m.PlannedEnd,m.ForecastEnd});
});
api.MapPatch("/projects/{projectId}/current-status",async Task<IResult>(string projectId,UpdateCurrentStatus req,HttpContext http,AppDbContext db)=>
{
    var u=Current(http);var p=await VisibleProjects(db,u).FirstOrDefaultAsync(x=>x.Id==projectId);
    if(p==null)return Results.NotFound();
    if(u.Role!="BA"||p.TimelineOwnerUserId!=u.Id)return Results.StatusCode(403);
    if(!Validation.CurrentStatuses.Contains(req.CurrentStatus)||string.IsNullOrWhiteSpace(req.Note)||req.Note.Length>1000)
        return Results.BadRequest(new{error="Valid phase and note required"});
    var old=p.CurrentStatus;p.CurrentStatus=req.CurrentStatus;p.UpdatedAt=DateTime.UtcNow;
    db.Activities.Add(new TaskActivity {ProjectId=p.Id,TaskId="",ActorUserId=u.Id,Action="Current status",OldValue=old,NewValue=p.CurrentStatus,Note=req.Note.Trim()});
    await db.SaveChangesAsync();return Results.Ok(new{p.Id,p.CurrentStatus});
});
// Admin owns reporting lines independently from project memberships. Moving BA teams does not alter project assignments.
api.MapGet("/admin/users",async Task<IResult>(HttpContext http,AppDbContext db)=>
{
    if(Current(http).Role!="Admin")return Results.StatusCode(403);
    return Results.Ok(await db.Users.OrderBy(x=>x.FullName).Select(x=>new{x.Id,x.FullName,x.Email,x.Role,x.DepartmentId,x.ReportsToPmUserId,x.IsActive}).ToListAsync());
});
api.MapPost("/admin/users",async Task<IResult>(NewAdminUser req,HttpContext http,AppDbContext db)=>
{
    var actor=Current(http);if(actor.Role!="Admin")return Results.StatusCode(403);
    var issue=await ValidateUserInput(db,req.FullName,req.Email,req.Role,req.DepartmentId,req.ReportsToPmUserId);
    if(issue!=null)return Results.BadRequest(new{error=issue});
    if(await db.Users.AnyAsync(x=>x.Email==req.Email.Trim().ToLower()))return Results.Conflict(new{error="Email already registered"});
    var u=new AppUser {FullName=req.FullName.Trim(),Email=req.Email.Trim().ToLowerInvariant(),Role=req.Role,
        DepartmentId=req.DepartmentId,ReportsToPmUserId=req.Role=="BA"?req.ReportsToPmUserId:null,
        PasswordHash=LocalAuth.HashPassword("Demo@12345"),IsActive=true};
    db.Users.Add(u);await db.SaveChangesAsync();return Results.Created($"/api/admin/users/{u.Id}",new{u.Id,u.FullName,u.Email});
});
api.MapPatch("/admin/users/{userId:int}",async Task<IResult>(int userId,UpdateAdminUser req,HttpContext http,AppDbContext db)=>
{
    var actor=Current(http);if(actor.Role!="Admin")return Results.StatusCode(403);
    var target=await db.Users.FirstOrDefaultAsync(x=>x.Id==userId);if(target==null)return Results.NotFound();
    if(actor.Id==target.Id && (req.Role!="Admin" || !req.IsActive))return Results.BadRequest(new{error="Cannot remove your own admin access"});
    var issue=await ValidateUserInput(db,req.FullName,req.Email,req.Role,req.DepartmentId,req.ReportsToPmUserId);
    if(issue!=null)return Results.BadRequest(new{error=issue});
    if(await db.Users.AnyAsync(x=>x.Id!=userId&&x.Email==req.Email.Trim().ToLower()))return Results.Conflict(new{error="Email already registered"});
    if(target.Role=="PM" && req.Role!="PM" && (await db.Projects.AnyAsync(x=>x.PmUserId==userId)||await db.Users.AnyAsync(x=>x.ReportsToPmUserId==userId)))
        return Results.Conflict(new{error="Reassign PM projects and direct reports first"});
    if(target.Role=="BA" && (req.Role!="BA" || !req.IsActive) && await db.Projects.AnyAsync(x=>x.TimelineOwnerUserId==userId))
        return Results.Conflict(new{error="Reassign timeline ownership before changing role or disabling BA"});
    target.FullName=req.FullName.Trim();target.Email=req.Email.Trim().ToLowerInvariant();target.Role=req.Role;
    target.DepartmentId=req.DepartmentId;target.ReportsToPmUserId=req.Role=="BA"?req.ReportsToPmUserId:null;target.IsActive=req.IsActive;
    await db.SaveChangesAsync();return Results.Ok(new{target.Id,target.FullName,target.Role,target.ReportsToPmUserId,target.IsActive});
});
api.MapPost("/admin/projects/{projectId}/members",async Task<IResult>(string projectId,MemberRequest req,HttpContext http,AppDbContext db)=>
{
    if(Current(http).Role!="Admin")return Results.StatusCode(403);
    if(!await db.Projects.AnyAsync(x=>x.Id==projectId))return Results.NotFound();
    if(!await db.Users.AnyAsync(x=>x.Id==req.UserId&&x.IsActive&&x.Role=="BA"))return Results.BadRequest(new{error="Valid BA required"});
    if(await db.ProjectMembers.AnyAsync(x=>x.ProjectId==projectId&&x.UserId==req.UserId))return Results.Conflict(new{error="Already assigned"});
    db.ProjectMembers.Add(new ProjectMember{ProjectId=projectId,UserId=req.UserId,ProjectRole="BA"});
    await db.SaveChangesAsync();return Results.Ok(new{projectId,req.UserId});
});
api.MapPatch("/admin/projects/{projectId}/timeline-owner",async Task<IResult>(string projectId,TimelineOwnerRequest req,HttpContext http,AppDbContext db)=>
{
    if(Current(http).Role!="Admin")return Results.StatusCode(403);
    var p=await db.Projects.FirstOrDefaultAsync(x=>x.Id==projectId);if(p==null)return Results.NotFound();
    if(!await db.ProjectMembers.AnyAsync(x=>x.ProjectId==projectId&&x.UserId==req.UserId&&x.User.Role=="BA"&&x.User.IsActive))
        return Results.BadRequest(new{error="Timeline owner must be an active assigned BA"});
    p.TimelineOwnerUserId=req.UserId;await db.SaveChangesAsync();return Results.Ok(new{p.Id,p.TimelineOwnerUserId});
});

app.Run();

static AppUser Current(HttpContext http) => (AppUser)http.Items["CurrentUser"]!;
static IQueryable<Project> VisibleProjects(AppDbContext db, AppUser u) => u.Role switch
{
    "Admin" => db.Projects,
    "DivisionHead" => db.Projects.Where(p => p.Department.DivisionId == u.Department.DivisionId),
    "DepartmentHead" => db.Projects.Where(p => p.DepartmentId == u.DepartmentId),
    "PM" or "BA" => db.Projects.Where(p => p.Members.Any(m => m.UserId == u.Id)),
    _ => db.Projects.Where(p => false)
};
static bool IsProjectBA(AppDbContext db,AppUser u,string projectId)=>u.Role=="BA" && db.ProjectMembers.Any(m=>m.ProjectId==projectId&&m.UserId==u.Id&&m.ProjectRole=="BA");
static async Task<string?> ValidateUserInput(AppDbContext db,string fullName,string email,string role,int departmentId,int? reportsToPmUserId)
{
    if(string.IsNullOrWhiteSpace(fullName)||fullName.Trim().Length>150||string.IsNullOrWhiteSpace(email)||email.Length>200||!email.Contains('@'))return "Invalid name/email";
    if(!new[]{"Admin","DivisionHead","DepartmentHead","PM","BA"}.Contains(role))return "Invalid role";
    var dept=await db.Departments.FindAsync(departmentId);if(dept==null)return "Invalid department";
    if(role=="BA" && (!reportsToPmUserId.HasValue || !await db.Users.AnyAsync(x=>x.Id==reportsToPmUserId.Value&&x.Role=="PM"&&x.IsActive&&x.Department.DivisionId==dept.DivisionId)))
        return "BA must have an active reporting PM within the division";
    return null;
}
static object ProjectSummary(Project p)=>new {p.Id,p.Name,p.Description,p.DepartmentId,
    department=p.Department?.Name??"",p.PmUserId,pm=p.PmUser?.FullName??"",p.Progress,p.Health,p.Status,p.CurrentStatus,p.RequesterName,p.RequesterDepartment,p.TimelineOwnerUserId,p.StartDate,p.BaselineEnd,p.ForecastEnd,p.UpdatedAt};
static object TaskDto(ProjectTask t)=>new {t.Id,t.ProjectId,t.MilestoneId,milestone=t.Milestone?.Name,
    t.Title,t.Description,t.OwnerUserId,owner=t.Owner?.FullName??"",t.Status,t.Priority,t.DueDate,t.Version,t.UpdatedAt,
    contributorIds=t.Contributors?.Select(c=>c.UserId).ToArray()??Array.Empty<int>()};

public sealed record NewProject(string Name,string? Description,int DepartmentId,DateOnly StartDate,DateOnly BaselineEnd,int TimelineOwnerUserId,string RequesterName,string RequesterDepartment);
public sealed record NewTask(string Title,string? Description,int OwnerUserId,string Priority,DateOnly DueDate,int? MilestoneId);
public sealed record UpdateTask(string Status,int Version,string? Note,DateOnly? DueDate);
public sealed record NewIssue(string Title,string Severity,string? TaskId,string? Impact,string? ActionPlan,DateOnly? DueDate);
public sealed record UpdateIssue(string Status,string? ActionPlan);
public static class Validation
{
    public static readonly string[] CurrentStatuses=["Requirement","Design","Development","SIT","UAT","Implementation","PIR","On Hold","Completed"];
    public static readonly string[] ProjectHealth=["On Track","At Risk","Delayed","Blocked"];
    public static readonly string[] TaskStatuses=["To Do","In Progress","Blocked","Done","Cancelled"];
    public static readonly string[] IssueStatuses=["Open","Escalated","Resolved"];
    public static readonly string[] Priorities=["Low","Medium","High","Critical"];
    public static readonly string[] Severities=["Low","Medium","High","Critical"];
}

public sealed record MemberRequest(int UserId);
public sealed record MilestoneRequest(string Name,int Weight,DateOnly PlannedStart,DateOnly PlannedEnd);
public sealed record MilestoneProgress(int Progress);
public sealed record UpdateProjectHealth(string Health,string Reason);

public sealed record LoginRequest(string Email, string Password);
public sealed record ResetPasswordRequest(string Email, string NewPassword);

public sealed record UpdateMilestoneDates(DateOnly PlannedStart,DateOnly PlannedEnd,DateOnly ForecastEnd,string Reason);
public sealed record UpdateCurrentStatus(string CurrentStatus,string Note);
public sealed record NewAdminUser(string FullName,string Email,string Role,int DepartmentId,int? ReportsToPmUserId);
public sealed record UpdateAdminUser(string FullName,string Email,string Role,int DepartmentId,int? ReportsToPmUserId,bool IsActive);
public sealed record TimelineOwnerRequest(int UserId);
