using Microsoft.EntityFrameworkCore;

namespace ProjectPulse.Api;

public static class SeedData
{
    private static readonly Dictionary<int, string> DemoNames = new()
    {
        [1]="Arman Prasetyo",[2]="Dimas Hartono",[3]="Citra Rahmawati",[4]="Rahmat Hakim",
        [5]="Rina Anggraini",[6]="Budi Setiawan",[7]="Andi Pratama",[8]="Sari Dewi",[9]="Maya Lestari",[10]="Nadia Putri",[11]="Fajar Administrator"
    };
    public static async Task InitializeAsync(AppDbContext db)
    {
        if (await db.Divisions.AnyAsync())
        {
            // Preserve locally edited project/task data when upgrading the old demo DB.
            var div = await db.Divisions.FindAsync(1);
            if (div is not null) div.Name = "IT Division";
            var deptNames = new Dictionary<int, string> { [1] = "Project", [2] = "Infrastructure", [3] = "IT Operations" };
            foreach (var d in await db.Departments.Where(d => d.DivisionId == 1).ToListAsync())
                if (deptNames.TryGetValue(d.Id, out var name)) d.Name = name;
            foreach (var u in await db.Users.ToListAsync())
            {
                if (string.IsNullOrEmpty(u.PasswordHash)) u.PasswordHash = LocalAuth.HashPassword("Demo@12345");
                // Preserve administrator-edited display names across local restarts.
            }
            if (!await db.Users.AnyAsync(u => u.Role == "Admin"))
                db.Users.Add(new AppUser { Id=11, DepartmentId=1, Role="Admin", FullName="Fajar Administrator", Email="admin@example.test", PasswordHash=LocalAuth.HashPassword("Demo@12345") });
            foreach (var user in await db.Users.Where(x=>x.Role=="BA").ToListAsync())
                if (user.ReportsToPmUserId is null) user.ReportsToPmUserId = user.Id == 9 ? 6 : 5;
            foreach (var project in await db.Projects.ToListAsync())
            {
                if (project.TimelineOwnerUserId is null)
                    project.TimelineOwnerUserId = await db.ProjectMembers.Where(x=>x.ProjectId==project.Id && x.ProjectRole=="BA").Select(x=>(int?)x.UserId).FirstOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(project.CurrentStatus))
                    project.CurrentStatus=project.Name.Contains("AF")?"SIT":project.Name.Contains("Risk")||project.Name.Contains("TMS")?"Development":
                        project.Name.Contains("Direct")?"Design":"UAT";
                if (string.IsNullOrWhiteSpace(project.RequesterName)) project.RequesterName="User Internal HO (Demo)";
                if (string.IsNullOrWhiteSpace(project.RequesterDepartment)) project.RequesterDepartment="Head Office";
            }
            foreach (var issue in await db.Issues.ToListAsync())
            {
                if (!await db.Users.AnyAsync(u=>u.Id==issue.OwnerUserId&&u.Role=="BA"))
                    issue.OwnerUserId=await db.ProjectMembers.Where(m=>m.ProjectId==issue.ProjectId&&m.ProjectRole=="BA").Select(m=>m.UserId).FirstAsync();
            }
            if (!await db.Users.AnyAsync(u => u.Id == 10))
                db.Users.Add(new AppUser { Id=10, DepartmentId=1, Role="BA", FullName="Nadia Putri", Email="ba04@example.test", ReportsToPmUserId=5, PasswordHash=LocalAuth.HashPassword("Demo@12345") });
            await db.SaveChangesAsync();
            return;
        }
        var division = new Division { Id = 1, Name = "IT Division" };
        db.Divisions.Add(division);
        db.Departments.AddRange(
            new Department { Id = 1, DivisionId = 1, Name = "Project" },
            new Department { Id = 2, DivisionId = 1, Name = "Infrastructure" },
            new Department { Id = 3, DivisionId = 1, Name = "IT Operations" });
        db.Users.AddRange(
            new AppUser { Id = 1, DepartmentId = 1, FullName = "Arman Prasetyo", Email = "division@example.test", Role = "DivisionHead" },
            new AppUser { Id = 2, DepartmentId = 1, FullName = "Dimas Hartono", Email = "head.project@example.test", Role = "DepartmentHead" },
            new AppUser { Id = 3, DepartmentId = 2, FullName = "Citra Rahmawati", Email = "head.infrastructure@example.test", Role = "DepartmentHead" },
            new AppUser { Id = 4, DepartmentId = 3, FullName = "Rahmat Hakim", Email = "head.operations@example.test", Role = "DepartmentHead" },
            new AppUser { Id = 5, DepartmentId = 1, FullName = "Rina Anggraini", Email = "pm01@example.test", Role = "PM" },
            new AppUser { Id = 6, DepartmentId = 2, FullName = "Budi Setiawan", Email = "pm02@example.test", Role = "PM" },
            new AppUser { Id = 7, DepartmentId = 1, FullName = "Andi Pratama", Email = "ba01@example.test", Role = "BA", ReportsToPmUserId=5 },
            new AppUser { Id = 8, DepartmentId = 1, FullName = "Sari Dewi", Email = "ba02@example.test", Role = "BA", ReportsToPmUserId=5 },
            new AppUser { Id = 9, DepartmentId = 2, FullName = "Maya Lestari", Email = "ba03@example.test", Role = "BA", ReportsToPmUserId=6 },
            new AppUser { Id = 10, DepartmentId = 1, FullName = "Nadia Putri", Email = "ba04@example.test", Role = "BA", ReportsToPmUserId=5 },
            new AppUser { Id = 11, DepartmentId = 1, FullName = "Fajar Administrator", Email = "admin@example.test", Role = "Admin" });
        foreach(var user in db.Users.Local) user.PasswordHash = LocalAuth.HashPassword("Demo@12345");
        var data = new [] {
            new { Id="REQ20261001001", Name="AF Dashboard", Dept=1, PM=5, Health="On Track", Progress=82, End="2026-10-29", Members=new[]{7,8} },
            new { Id="REQ20261001002", Name="MCS & MCS HO", Dept=1, PM=5, Health="Delayed", Progress=61, End="2026-10-08", Members=new[]{7,9} },
            new { Id="REQ20261001003", Name="Risk Premium", Dept=3, PM=6, Health="Blocked", Progress=46, End="2026-11-05", Members=new[]{8,9} },
            new { Id="REQ20261001004", Name="ATC Dashboard", Dept=2, PM=6, Health="On Track", Progress=65, End="2026-11-16", Members=new[]{9} },
            new { Id="REQ20261001005", Name="Direct Income", Dept=1, PM=5, Health="At Risk", Progress=38, End="2026-11-23", Members=new[]{8} },
            new { Id="REQ20261001006", Name="TMS Integration", Dept=2, PM=6, Health="Blocked", Progress=78, End="2026-10-21", Members=new[]{7,9} }
        };
        var phases = new[] { ("Requirement",15,0,4), ("Design",15,5,10), ("Development",30,11,21), ("SIT",15,22,26), ("UAT",15,27,31), ("Implementation",5,32,33), ("PIR",5,34,36) };
        foreach (var p in data)
        {
            db.Projects.Add(new Project { Id=p.Id, Name=p.Name, DepartmentId=p.Dept, PmUserId=p.PM, StartDate=new DateOnly(2026,10,1), BaselineEnd=DateOnly.Parse(p.End), ForecastEnd=DateOnly.Parse(p.End).AddDays(p.Health=="Delayed"?9:0), Health=p.Health, Progress=p.Progress, Description=$"Monitoring pelaksanaan {p.Name}.", Status="Active",
                TimelineOwnerUserId=p.Members[0], CurrentStatus=p.Name.Contains("AF")?"SIT":p.Name.Contains("Risk")||p.Name.Contains("TMS")?"Development":p.Name.Contains("Direct")?"Design":"UAT",
                RequesterName=p.Name.Contains("AF")?"Nina Kusuma":"Rudi Firmansyah", RequesterDepartment=p.Name.Contains("AF")?"HRD":"Finance" });
            db.ProjectMembers.Add(new ProjectMember { ProjectId=p.Id, UserId=p.PM, ProjectRole="PM" });
            foreach (var u in p.Members) db.ProjectMembers.Add(new ProjectMember { ProjectId=p.Id, UserId=u, ProjectRole="BA" });
            foreach (var ph in phases)
            {
                db.Milestones.Add(new Milestone { ProjectId=p.Id, Name=ph.Item1, Weight=ph.Item2, PlannedStart=new DateOnly(2026,10,1).AddDays(ph.Item3), PlannedEnd=new DateOnly(2026,10,1).AddDays(ph.Item4), ForecastEnd=new DateOnly(2026,10,1).AddDays(ph.Item4), Status=ph.Item3<10?"In Progress":"Not Started", Progress=ph.Item3<5?100:ph.Item3<11?p.Progress:0 });
            }
        }
        var tasks = new [] {
            ("TSK-101","REQ20261001001",7,"Review angka AF terhadap source","In Progress","High", "2026-10-12"),
            ("TSK-102","REQ20261001001",8,"Susun scenario UAT AF","To Do","Medium", "2026-10-16"),
            ("TSK-103","REQ20261001002",7,"Reconcile dashboard MCS","Blocked","Critical", "2026-10-08"),
            ("TSK-104","REQ20261001002",9,"Update dokumentasi MCS HO","In Progress","Medium", "2026-10-15"),
            ("TSK-105","REQ20261001003",8,"Finalisasi definisi risk premium","Blocked","High", "2026-10-09"),
            ("TSK-106","REQ20261001003",9,"Review mockup premium","To Do","Medium", "2026-10-21"),
            ("TSK-107","REQ20261001004",9,"Testing integrasi ATC","In Progress","High", "2026-10-18"),
            ("TSK-108","REQ20261001005",8,"Profiling source direct income","In Progress","High", "2026-10-19"),
            ("TSK-109","REQ20261001006",7,"Investigasi socket closed TMS","Blocked","Critical", "2026-10-10"),
            ("TSK-110","REQ20261001006",9,"Dokumentasi retry incremental","To Do","Medium", "2026-10-14")
        };
        foreach (var t in tasks) db.Tasks.Add(new ProjectTask { Id=t.Item1, ProjectId=t.Item2, OwnerUserId=t.Item3, Title=t.Item4, Status=t.Item5, Priority=t.Item6, DueDate=DateOnly.Parse(t.Item7) });
        db.TaskContributors.AddRange(
            new TaskContributor { TaskId="TSK-101", UserId=8 },
            new TaskContributor { TaskId="TSK-103", UserId=9 },
            new TaskContributor { TaskId="TSK-109", UserId=9 });
        db.Issues.AddRange(
            new ProjectIssue { Id="ISS-001", ProjectId="REQ20261001002", TaskId="TSK-103", OwnerUserId=7, Title="Scope berubah saat UAT", Severity="High", Status="Open", Impact="Go-live mundur", ActionPlan="Review change request dan jadwalkan ulang", DueDate=new DateOnly(2026,10,13) },
            new ProjectIssue { Id="ISS-002", ProjectId="REQ20261001003", TaskId="TSK-105", OwnerUserId=8, Title="Business rule belum disetujui", Severity="Critical", Status="Escalated", Impact="UAT belum bisa dimulai", ActionPlan="Eskalasi ke business owner", DueDate=new DateOnly(2026,10,12) },
            new ProjectIssue { Id="ISS-003", ProjectId="REQ20261001006", TaskId="TSK-109", OwnerUserId=7, Title="Socket closed saat extraction", Severity="Critical", Status="Escalated", Impact="Integrasi tertahan", ActionPlan="Review jaringan dan pecah batch tarik", DueDate=new DateOnly(2026,10,11) });
        await db.SaveChangesAsync();
    }
}
