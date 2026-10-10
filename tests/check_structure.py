"""Dependency-free sanity check (not a substitute for .NET integration tests)."""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parents[1]
required = [
    'backend/ProjectPulse.Api.csproj', 'backend/Program.cs', 'backend/AppDbContext.cs',
    'backend/Models.cs', 'backend/SeedData.cs','backend/LocalAuth.cs', 'frontend/package.json',
    'frontend/src/App.tsx', 'frontend/src/api.ts', 'frontend/public/branding/logo.svg', 'frontend/public/branding/favicon.ico', 'sql/001_postgresql_init.sql',
    'docs/ERD.md', 'docs/API.md', 'prototype/index.html'
]
for item in required:
    assert (root / item).exists(), f'Missing {item}'

settings = json.loads((root / 'backend/appsettings.json').read_text())
assert settings['Database']['Provider'] == 'Sqlite'
assert settings['Security']['Mode'] == 'Local'
package = json.loads((root / 'frontend/package.json').read_text())
assert 'react' in package['dependencies']
assert 'vite' in package['devDependencies']

pg = (root / 'sql/001_postgresql_init.sql').read_text()
expected = ('divisions','departments','users','projects','project_members', 'milestones',
            'tasks','task_contributors','issues','task_activities','project_baselines')
for table in expected:
    assert re.search(r'CREATE TABLE IF NOT EXISTS\s+' + table + r'\b', pg), f'No table {table}'
assert 'BEGIN;' in pg and 'COMMIT;' in pg
assert 'REFERENCES projects(id)' in pg and 'REFERENCES users(id)' in pg
code = (root / 'backend/Program.cs').read_text()
assert 'provider != "Sqlite"' in code and 'mode == "Local"' in code
assert 'localAuth.MapPost("/login"' in code and 'localAuth.MapPost("/reset"' in code
assert 'X-Demo-User' not in code, 'Insecure legacy demo identity bypass remains'
assert 'Results.Ok(Current(http))' not in code, 'Password hash must not leak through /me'
assert 'api.MapPatch("/tasks/{taskId}"' in code
assert 'Version' in code and 'DbUpdateConcurrencyException' in code
print('PASS: file manifest, JSON configs, PG schema names, local-only authentication guard, task concurrency surface.')
