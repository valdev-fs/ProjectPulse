using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace ProjectPulse.Api;

// SQLite-only compatibility upgrade for local v0.2/v0.3 demos. Never run against PostgreSQL.
public static class LocalUpgrade
{
    public static async Task RunAsync(AppDbContext db)
    {
        var conn=db.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            async Task Exec(string sql)
            {
                await using var cmd=conn.CreateCommand(); cmd.CommandText=sql; await cmd.ExecuteNonQueryAsync();
            }
            async Task<bool> Has(string table,string column)
            {
                await using var cmd=conn.CreateCommand(); cmd.CommandText=$"PRAGMA table_info({table})";
                await using var reader=await cmd.ExecuteReaderAsync();
                while(await reader.ReadAsync())if(reader.GetString(1)==column)return true;
                return false;
            }
            if(!await Has("users","reports_to_pm_user_id"))await Exec("ALTER TABLE users ADD COLUMN reports_to_pm_user_id INTEGER NULL");
            if(!await Has("users","is_active"))await Exec("ALTER TABLE users ADD COLUMN is_active INTEGER NOT NULL DEFAULT 1");
            if(!await Has("users","password_hash"))await Exec("ALTER TABLE users ADD COLUMN password_hash TEXT NOT NULL DEFAULT ''");
            if(!await Has("projects","timeline_owner_user_id"))await Exec("ALTER TABLE projects ADD COLUMN timeline_owner_user_id INTEGER NULL");
            if(!await Has("projects","requester_name"))await Exec("ALTER TABLE projects ADD COLUMN requester_name TEXT NOT NULL DEFAULT ''");
            if(!await Has("projects","requester_department"))await Exec("ALTER TABLE projects ADD COLUMN requester_department TEXT NOT NULL DEFAULT ''");
            if(!await Has("projects","current_status"))await Exec("ALTER TABLE projects ADD COLUMN current_status TEXT NOT NULL DEFAULT ''");
            await Exec("CREATE TABLE IF NOT EXISTS req_counters (date_key TEXT PRIMARY KEY, last_number INTEGER NOT NULL)");
            await Exec("CREATE TABLE IF NOT EXISTS local_sessions (id INTEGER PRIMARY KEY AUTOINCREMENT,user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,token_hash TEXT NOT NULL UNIQUE,expires_at TEXT NOT NULL,created_at TEXT NOT NULL)");
            await Exec("CREATE INDEX IF NOT EXISTS ix_local_sessions_expires_at ON local_sessions(expires_at)");
            // Migrate public IDs, including dependent FKs. FK enforcement is disabled only inside this controlled local bootstrap.
            var old=new List<(string Id,string Date)>();
            await using(var cmd=conn.CreateCommand())
            {
                cmd.CommandText="SELECT id, start_date FROM projects WHERE id LIKE 'PRJ-%' ORDER BY id";
                await using var reader=await cmd.ExecuteReaderAsync();
                while(await reader.ReadAsync())old.Add((reader.GetString(0),reader.GetString(1)));
            }
            if(old.Count>0)
            {
                await Exec("PRAGMA foreign_keys=OFF");
                await using var transaction=await conn.BeginTransactionAsync();
                try
                {
                    foreach(var item in old)
                    {
                        var day=item.Date.Replace("-","")[..8];
                        await using var maxCmd=conn.CreateCommand();maxCmd.Transaction=transaction;
                        maxCmd.CommandText="SELECT COALESCE(MAX(CAST(SUBSTR(id,12,3) AS INTEGER)),0) FROM projects WHERE id LIKE @prefix";
                        Add(maxCmd,"@prefix","REQ"+day+"%");
                        var no=Convert.ToInt32(await maxCmd.ExecuteScalarAsync())+1;
                        if(no>999)throw new InvalidOperationException("Daily REQ ID limit exceeded during upgrade.");
                        var next=$"REQ{day}{no:000}";
                        foreach(var table in new[]{"project_members","milestones","tasks","issues","task_activities","project_baselines","projects"})
                        {
                            await using var cmd=conn.CreateCommand();cmd.Transaction=transaction;
                            cmd.CommandText=$"UPDATE {table} SET {(table=="projects"?"id":"project_id")}=@next WHERE {(table=="projects"?"id":"project_id")}=@old";
                            Add(cmd,"@next",next);Add(cmd,"@old",item.Id);await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    await transaction.CommitAsync();
                }
                catch { await transaction.RollbackAsync();throw; }
                finally {await Exec("PRAGMA foreign_keys=ON");}
                await using var check=conn.CreateCommand();check.CommandText="PRAGMA foreign_key_check";
                await using var bad=await check.ExecuteReaderAsync();
                if(await bad.ReadAsync())throw new InvalidOperationException("SQLite foreign_key_check failed; restore backup before continuing.");
            }
            // Sync counters even if the schema already contained REQ records.
            await Exec("INSERT INTO req_counters(date_key,last_number) SELECT SUBSTR(id,4,8), MAX(CAST(SUBSTR(id,12,3) AS INTEGER)) FROM projects WHERE id LIKE 'REQ%' GROUP BY SUBSTR(id,4,8) ON CONFLICT(date_key) DO UPDATE SET last_number = MAX(req_counters.last_number,excluded.last_number)");
        }
        finally {await conn.CloseAsync();}
    }
    private static void Add(DbCommand cmd,string name,object value) {var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);}
}
