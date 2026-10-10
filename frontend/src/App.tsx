import {useCallback,useEffect,useMemo,useState,type ReactNode,type FormEvent} from 'react';
import {api,authRequest,logoutLocal,getToken,setToken,clearToken, type User,type Department,type Project,type Portfolio,type Task,type Detail,type Issue} from './api';

type Page='executive'|'department'|'pm'|'ba'|'project'|'admin';
const menu:{key:Page;label:string;icon:string}[]=[
  {key:'executive',label:'Portfolio Overview',icon:'▦'},
  {key:'department',label:'Department Monitor',icon:'◫'},
  {key:'pm',label:'PM Workspace',icon:'▤'},
  {key:'ba',label:'My Tasks',icon:'☑'},
  {key:'project',label:'Project Detail',icon:'▥'},
  {key:'admin',label:'User Administration',icon:'⚙'}
];
const dateFmt=(date?:string|null)=>date?new Date(date+'T00:00:00').toLocaleDateString('id-ID',{day:'2-digit',month:'short',year:'numeric'}):'—';
const badgeClass=(v:string)=>({"On Track":"green","At Risk":"amber","Delayed":"red","Blocked":"red","Done":"green","In Progress":"blue","To Do":"muted","Critical":"red","High":"amber","Escalated":"red"}[v]||'muted');
const today=()=>{const d=new Date();return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;};
const overdue=(task:Task)=>task.dueDate<today()&&!['Done','Cancelled'].includes(task.status);
const healthBadge=(status:string)=><span className={`pill ${badgeClass(status)}`}><span className="dot"/>{status}</span>;
const progress=(value:number)=><div className="progress"><span style={{width:`${Math.min(value,100)}%`}}/></div>;
const count=(arr:Project[],health:string)=>arr.filter(p=>p.health===health).length;

type DemoAccount={fullName:string;email:string;role:string};
const roleTitle=(role:string)=>({DivisionHead:'Division Head IT',DepartmentHead:'Department Head',PM:'Project Management',BA:'Business Analyst',Admin:'System Administrator'} as Record<string,string>)[role]||role;
const demoRoleOrder=['Admin','DivisionHead','DepartmentHead','PM','BA'];

export default function App(){
  const [authenticated,setAuthenticated]=useState(false);
  const [initializing,setInitializing]=useState(true);
  const [activeUser,setActiveUser]=useState<User|null>(null);
  const restore=useCallback(async()=>{
    if(!getToken()){setInitializing(false);return;}
    try{setActiveUser(await api<User>('/me'));setAuthenticated(true)}
    catch{clearToken();setAuthenticated(false);setActiveUser(null)}
    finally{setInitializing(false)}
  },[]);
  useEffect(()=>{void restore()},[restore]);
  useEffect(()=>{
    const expired=()=>{setAuthenticated(false);setActiveUser(null)};
    window.addEventListener('pp-auth-expired',expired);
    return()=>window.removeEventListener('pp-auth-expired',expired);
  },[]);
  const handleLogin=async(email:string,password:string)=>{
    const response=await authRequest<{token:string;expiresAt:string}>('login',{email,password});
    setToken(response.token);
    try{
      const user=await api<User>('/me');setActiveUser(user);setAuthenticated(true);
    }catch(err){clearToken();throw err}
  };
  const logout=async()=>{
    try{await logoutLocal()}catch{/* token may already be expired */}
    clearToken();setActiveUser(null);setAuthenticated(false);
  };
  if(initializing)return <div className="auth-loading"><img src="/branding/logo.svg" alt=""/>Memuat ProjectPulse...</div>;
  if(!authenticated||!activeUser)return <LoginPage onLogin={handleLogin}/>;
  return <Workspace key={activeUser.id} onLogout={logout}/>;
}

function LoginPage({onLogin}:{onLogin:(email:string,password:string)=>Promise<void>}){
  const [email,setEmail]=useState('');
  const [password,setPassword]=useState('');
  const [confirm,setConfirm]=useState('');
  const [resetMode,setResetMode]=useState(false);
  const [visible,setVisible]=useState(false);
  const [accounts,setAccounts]=useState<DemoAccount[]>([]);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  useEffect(()=>{void fetch('/api/auth/demo-users').then(async r=>r.ok?await r.json():[]).then((rows:DemoAccount[])=>setAccounts(rows)).catch(()=>{})},[]);
  const submit=async(e:FormEvent)=>{
    e.preventDefault();setBusy(true);setError('');setNotice('');
    try{
      if(resetMode){
        if(password!==confirm)throw new Error('Konfirmasi password tidak sama.');
        const result=await authRequest<{message:string}>('reset',{email,newPassword:password});
        setNotice(result.message);setResetMode(false);setPassword('');setConfirm('');
      }else await onLogin(email,password);
    }catch(err){setError(err instanceof Error?err.message:String(err))}
    finally{setBusy(false)}
  };
  const group=demoRoleOrder.map(role=>({role,accounts:accounts.filter(u=>u.role===role)}));
  return <div className="auth-page">
    <section className="auth-visual">
      <div className="auth-visual-inner">
        <div className="login-brand"><img src="/branding/logo.svg" alt="ProjectPulse logo"/><span>Project<span>Pulse</span></span></div>
        <div className="auth-hero"><span className="auth-eyebrow">PROJECT PORTFOLIO INTELLIGENCE</span><h1>One platform.<br/><span>Every project.</span><br/>Total visibility.</h1><p>Kelola pekerjaan harian, pantau milestone, kendala, dan kesehatan project dalam satu alur pelaporan.</p></div>
        <div className="org-glass"><div className="org-head"><span>STRUKTUR ORGANISASI</span><span className="org-demo-pill">Dummy data</span></div>
          <div className="org-node org-top"><span className="org-node-icon">◈</span><div><strong>Division Head IT</strong><small>Seluruh portfolio divisi</small></div></div>
          <div className="org-line"/>
          <div className="org-node"><span className="org-node-icon green-icon">▤</span><div><strong>Department Project</strong><small>Di bawah IT Division</small></div></div>
          <div className="org-line"/>
          <div className="org-node"><span className="org-node-icon orange-icon">▣</span><div><strong>Project Management</strong><small>PM &amp; project delivery</small></div></div>
          <div className="org-line"/>
          <div className="org-children">{['BA 01','BA 02','BA 03','BA 04'].map(n=><span key={n}>♙ {n}</span>)}</div>
          <p>Departemen lain bisa ditambahkan seiring perluasan aplikasi.</p>
        </div>
        <div className="auth-footer">ProjectPulse · Internal Project Monitoring System</div>
      </div>
    </section>
    <section className="auth-form-column"><div className="auth-form-layout">
      <div className="auth-form-top"><img src="/branding/logo.svg" alt=""/> <span>ProjectPulse <em>DEMO</em></span></div>
      <div className="auth-form-header"><span className="auth-form-overline">SECURE WORKSPACE</span><h2>{resetMode?'Reset password':'Welcome back'}</h2><p>{resetMode?'Reset password khusus demo lokal, tanpa konfirmasi email.':'Masuk menggunakan salah satu akun dummy untuk melihat dashboard sesuai role.'}</p></div>
      <form className="auth-form" onSubmit={e=>void submit(e)}>
        <label>Email pengguna <input autoFocus type="email" autoComplete="username" value={email} onChange={e=>setEmail(e.target.value)} placeholder="nama@example.test" required maxLength={200}/></label>
        <label>{resetMode?'Password baru':'Password'} <div className="password-wrap"><input type={visible?'text':'password'} autoComplete={resetMode?'new-password':'current-password'} value={password} onChange={e=>setPassword(e.target.value)} minLength={resetMode?10:1} placeholder="Masukkan password" required/><button type="button" onClick={()=>setVisible(v=>!v)} aria-label={visible?'Sembunyikan password':'Lihat password'}>{visible?'Sembunyikan':'Lihat'}</button></div></label>
        {resetMode&&<label>Konfirmasi password baru <input type={visible?'text':'password'} autoComplete="new-password" value={confirm} onChange={e=>setConfirm(e.target.value)} minLength={10} placeholder="Ulangi password baru" required/></label>}
        {error&&<div className="auth-error" role="alert">{error}</div>}
        {notice&&<div className="auth-notice" role="status">{notice}</div>}
        <button className="auth-submit" disabled={busy}>{busy?'Memproses...':resetMode?'Reset Password':'Sign In →'}</button>
        <button className="auth-text-button" type="button" onClick={()=>{setResetMode(v=>!v);setError('');setNotice('');setPassword('');setConfirm('')}}>{resetMode?'← Kembali ke login':'Lupa password? Reset untuk demo'}</button>
      </form>
      {!resetMode&&<div className="demo-login-list"><div className="demo-title"><span>PILIH AKUN DEMO</span><span>Klik untuk mengisi email</span></div><div className="demo-account-grid">
        {group.map(g=><div key={g.role} className="demo-group"><div className="demo-group-title">{roleTitle(g.role)}</div>{g.accounts.map(u=><button type="button" key={u.email} className={`demo-user ${email===u.email?'selected':''}`} onClick={()=>{setEmail(u.email);setError('')}}><span className="demo-avatar">{u.fullName.split(' ').map(n=>n[0]).slice(0,2).join('')}</span><span><strong>{u.fullName}</strong><small>{u.email}</small></span></button>)}</div>)}</div>
        <div className="demo-password"><span>🔑</span> Password awal semua akun: <strong>Demo@12345</strong></div>
      </div>}
      {resetMode&&<p className="auth-warning">Hanya untuk localhost. Siapa pun yang mengetahui email dummy dapat melakukan reset tanpa verifikasi. Fitur ini otomatis tidak tersedia dalam mode PostgreSQL/JWT.</p>}
      <footer className="login-footer">© 2026 ProjectPulse · Local development demonstration</footer>
    </div></section>
  </div>;
}

function Workspace({onLogout}:{onLogout:()=>Promise<void>}){

  const [me,setMe]=useState<User|null>(null);
  const [users,setUsers]=useState<User[]>([]);
  const [departments,setDepartments]=useState<Department[]>([]);
  const [portfolio,setPortfolio]=useState<Portfolio|null>(null);
  const [projects,setProjects]=useState<Project[]>([]);
  const [myTasks,setMyTasks]=useState<Task[]>([]);
  const [page,setPage]=useState<Page>('ba');
  const [selected,setSelected]=useState<string>('');
  const [detail,setDetail]=useState<Detail|null>(null);
  const [tab,setTab]=useState<'timeline'|'tasks'|'issues'|'activity'>('timeline');
  const [deptId,setDeptId]=useState<number>(0);
  const [search,setSearch]=useState('');
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [modal,setModal]=useState<'task'|'issue'|'project'|'member'|'milestone'|'health'|'currentStatus'|'dates'|'adminUser'|'timelineOwner'|null>(null);
  const [editing,setEditing]=useState<Task|null>(null);
  const [editingIssue,setEditingIssue]=useState<Issue|null>(null);
  const [message,setMessage]=useState('');
  const [adminUsers,setAdminUsers]=useState<User[]>([]);
  const [editingAdmin,setEditingAdmin]=useState<User|null>(null);
  const [editingMilestone,setEditingMilestone]=useState<Detail['milestones'][number]|null>(null);

  const load=useCallback(async()=>{
    try{
      const [u,ul,d,p,pro,t]=await Promise.all([
        api<User>('/me'),api<User[]>('/users'),api<Department[]>('/departments'),
        api<Portfolio>('/portfolio'),api<Project[]>('/projects'),api<Task[]>('/tasks/my')
      ]);
      if(u.role==='Admin')setAdminUsers(await api<User[]>('/admin/users'));
      setMe(u);setUsers(ul);setDepartments(d);setPortfolio(p);setProjects(pro);setMyTasks(t);
      setSelected(prev=>pro.some(x=>x.id===prev)?prev:pro[0]?.id||'');
      setError('');
    }catch(e){setError(e instanceof Error?e.message:String(e));}
  },[]);
  const loadDetail=useCallback(async()=>{
    if(!selected){setDetail(null);return;}
    try{setDetail(await api<Detail>(`/projects/${selected}`))}catch(e){setDetail(null);setError(e instanceof Error?e.message:String(e))}
  },[selected]);
  useEffect(()=>{void load()},[load]);
  useEffect(()=>{void loadDetail()},[loadDetail]);
  useEffect(()=>{if(!me)return;setPage(me.role==='DivisionHead'?'executive':me.role==='DepartmentHead'?'department':me.role==='PM'?'pm':me.role==='Admin'?'admin':'ba')},[me?.id]);
  const filtered=useMemo(()=>projects.filter(p=>(!deptId||p.departmentId===deptId)&&`${p.name} ${p.department} ${p.pm}`.toLowerCase().includes(search.toLowerCase())),[projects,deptId,search]);
  const viewProject=(id:string)=>{setSelected(id);setTab('timeline');setPage('project');};
  const refresh=async()=>{await load();await loadDetail();};
  const mutate=async(action:()=>Promise<unknown>)=>{
    setBusy(true);setMessage('');try{await action();setEditing(null);setEditingIssue(null);setModal(null);await refresh();setMessage('Saved successfully.');}
    catch(e){setError(e instanceof Error?e.message:String(e))}finally{setBusy(false)}
  };
  const isEditor=me?.role==='BA' && !!detail?.members.some(m=>m.userId===me?.id&&m.projectRole==='BA');
  const isTimelineBA=me?.role==='BA' && detail?.project.timelineOwnerUserId===me?.id;
  const isPM=me?.role==='PM';
  const canEditTask=(t:Task)=>!!me && me.role==='BA'&&(t.ownerUserId===me.id||t.contributorIds.includes(me.id));
  const canEditIssue=(i:Issue)=>!!me && me.role==='BA'&&i.ownerUserId===me.id;
  const pageTitle=({executive:'Portfolio Overview',department:'Department Monitoring',pm:'Project Manager Workspace',ba:'My Tasks',project:detail?.project.name||'Project Detail',admin:'User Administration'} as Record<Page,string>)[page];
  const navigate=(key:Page)=>{setPage(key);setError('');};

  return <div className="shell">
    <aside className="sidebar">
      <div className="brand"><img className="brand-logo" src="/branding/logo.svg" alt=""/><div><strong>ProjectPulse</strong><small>ENTERPRISE MONITORING</small></div></div>
      <div className="nav-label">WORKSPACE</div>
      <nav>{menu.filter(m=>(me?.role==='BA'?['ba','project']:me?.role==='PM'?['pm','project']:me?.role==='DepartmentHead'?['department','project']:me?.role==='Admin'?['admin','executive','project']:['executive','project']).includes(m.key)).map(m=><button key={m.key} className={`nav ${page===m.key?'active':''}`} onClick={()=>navigate(m.key)}><span className="nav-icon">{m.icon}</span>{m.label}</button>)}</nav>
      <div className="side-bottom user-side"><div className="mini-title">SIGNED IN AS</div><div className="side-user"><strong>{me?.fullName||'Loading...'}</strong><small>{me?roleTitle(me.role):''}</small></div><button className="logout-btn" onClick={()=>void onLogout()}>↪ Sign Out</button></div>
    </aside>
    <main className="main"><header className="topbar"><div className="crumb">Workspace <span> / </span> {pageTitle}</div><div className="top-right"><span className="live"><span/> Signed in</span><div className="avatar">{me?.fullName.split(' ').map(s=>s[0]).join('').slice(0,2)||'U'}</div><span className="who"><strong>{me?.fullName||'Loading'}</strong><small>{me?roleTitle(me.role):''}</small></span></div></header>
      <div className="main-body"><div className="heading"><div><p className="overline">PROJECT DELIVERY • FY 2026</p><h1>{pageTitle}</h1><p className="subtitle">{page==='executive'?'Consolidated project performance across the division':page==='department'?'Monitor your department portfolio and delivery risks':page==='pm'?'Own the delivery. Track milestones, tasks and blockers':page==='ba'?'Your assignments, deadlines and activity updates':page==='admin'?'Manage employees, reporting lines and resource borrowing':'Timeline, task ownership, issue management and activity history'}</p></div>
      <button className="btn secondary" onClick={()=>void refresh()}>↻ Refresh</button></div>
      {error&&<div className="notice error">{error}<button onClick={()=>setError('')}>×</button></div>}
      {message&&<div className="notice success">{message}<button onClick={()=>setMessage('')}>×</button></div>}
      {page==='executive'&&portfolio&&<>
        <div className="metrics">{[['Total Projects',portfolio.total,'◫'],['On Track',portfolio.onTrack,'✓'],['At Risk / Delayed',portfolio.atRisk+portfolio.delayed,'!'],['Blocked',portfolio.blocked,'⊘'],['Open Issues',portfolio.openIssues,'⚑']].map(([label,value,icon])=><div className="card metric" key={label}><div className="metric-top"><span>{label}</span><i>{icon}</i></div><strong>{value}</strong><small>Across accessible projects</small></div>)}</div>
        <div className="two-col"><section className="card section"><div className="section-head"><div><h2>Project Health by Department</h2><p>Health distribution for all projects</p></div></div>
          {portfolio.departments.map(d=><div className="stack-wrap" key={d.name}><div className="stack-title"><strong>{d.name}</strong><span>{d.total} projects</span></div><div className="stack">
            {d.onTrack>0&&<div className="stack-green" style={{flex:d.onTrack}}/>}{d.atRisk>0&&<div className="stack-amber" style={{flex:d.atRisk}}/>}{d.delayed>0&&<div className="stack-orange" style={{flex:d.delayed}}/>}{d.blocked>0&&<div className="stack-red" style={{flex:d.blocked}}/>}</div></div>)}
          <div className="legend"><span>● On Track</span><span>● At Risk</span><span>● Delayed</span><span>● Blocked</span></div>
        </section><section className="card section"><div className="section-head"><div><h2>Delivery Signals</h2><p>Immediate attention required</p></div></div><div className="signal"><span>Overdue Tasks</span><strong>{portfolio.overdueTasks}</strong></div><div className="signal"><span>Open Issues</span><strong>{portfolio.openIssues}</strong></div><div className="signal"><span>Critical Project Health</span><strong>{portfolio.blocked+portfolio.delayed}</strong></div><p className="helper">Project health reflects BA timeline-owner updates with supporting rationale. No automatic health inference from task completion.</p></section></div>
        <PortfolioCharts projects={portfolio.projects}/>
        <ProjectTable projects={portfolio.projects} onOpen={viewProject} heading="All Projects"/>
      </>}
      {page==='department'&&<>
        <div className="toolbar card"><label>Department <select value={deptId} onChange={e=>setDeptId(Number(e.target.value))}><option value={0}>All visible departments</option>{departments.map(d=><option key={d.id} value={d.id}>{d.name}</option>)}</select></label><label>Search <input value={search} onChange={e=>setSearch(e.target.value)} placeholder="Search projects"/></label></div>
        <div className="metrics four">{[['Projects',filtered.length],['On Track',count(filtered,'On Track')],['At Risk / Delayed',count(filtered,'At Risk')+count(filtered,'Delayed')],['Blocked',count(filtered,'Blocked')]].map(([label,value])=><div className="card metric" key={label}><div className="metric-top">{label}</div><strong>{value}</strong><small>Current selection</small></div>)}</div>
        <PortfolioCharts projects={filtered}/>
        <ProjectTable projects={filtered} onOpen={viewProject} heading="Department Project Register"/>
      </>}
      {page==='pm'&&<>
        <div className="metrics four">{[['My Projects',projects.length],['On Track',count(projects,'On Track')],['Delayed',count(projects,'Delayed')],['Blocked',count(projects,'Blocked')]].map(([label,value])=><div className="card metric" key={label}><div className="metric-top">{label}</div><strong>{value}</strong><small>Assigned portfolio</small></div>)}</div>
        <div className="actions-row"><button className="btn primary" onClick={()=>setModal('project')}>+ New Project</button></div><PortfolioCharts projects={projects}/><ProjectTable projects={projects} onOpen={viewProject} heading="Projects Managed"/>
        <section className="card section"><div className="section-head"><div><h2>Project task activity</h2><p>Choose a project to monitor BA-owned tasks, timeline and issues</p></div></div><div className="quick-projects">{projects.map(p=><button key={p.id} onClick={()=>viewProject(p.id)}><strong>{p.name}</strong>{healthBadge(p.health)}<span>Open →</span></button>)}</div></section>
      </>}
      {page==='admin'&&me?.role==='Admin'&&<>
        <div className="metrics four"><div className="card metric"><div className="metric-top">All members</div><strong>{adminUsers.length}</strong></div><div className="card metric"><div className="metric-top">Business Analysts</div><strong>{adminUsers.filter(u=>u.role==='BA').length}</strong></div><div className="card metric"><div className="metric-top">Project Managers</div><strong>{adminUsers.filter(u=>u.role==='PM').length}</strong></div><div className="card metric"><div className="metric-top">Inactive</div><strong>{adminUsers.filter(u=>!u.isActive).length}</strong></div></div>
        <div className="actions-row"><button className="btn primary" onClick={()=>{setEditingAdmin(null);setModal('adminUser')}}>+ Add Member</button></div>
        <section className="card section"><div className="section-head"><div><h2>Organization & Reporting Lines</h2><p>BA home team is independent of project assignments. Moving reporting PM will not remove borrowed resources.</p></div></div>
          <div className="table-scroll"><table><thead><tr><th>Employee</th><th>Role</th><th>Department</th><th>Reporting PM (home team)</th><th>Active</th><th></th></tr></thead><tbody>{adminUsers.map(u=><tr key={u.id}><td><strong>{u.fullName}</strong><small>{u.email}</small></td><td>{roleTitle(u.role)}</td><td>{departments.find(d=>d.id===u.departmentId)?.name||'-'}</td><td>{u.role==='BA'?adminUsers.find(x=>x.id===u.reportsToPmUserId)?.fullName||'-':'—'}</td><td>{u.isActive?'Active':'Disabled'}</td><td><button className="text-button" onClick={()=>{setEditingAdmin(u);setModal('adminUser')}}>Edit →</button></td></tr>)}</tbody></table></div>
        </section>
        <PortfolioCharts projects={projects}/>
      </>}
      {page==='ba'&&<>
        <div className="metrics four">{[['Assigned Tasks',myTasks.length],['In Progress',myTasks.filter(t=>t.status==='In Progress').length],['Overdue',myTasks.filter(overdue).length],['Completed',myTasks.filter(t=>t.status==='Done').length]].map(([label,value])=><div className="card metric" key={label}><div className="metric-top">{label}</div><strong>{value}</strong><small>Across my projects</small></div>)}</div>
        <div className="actions-row"><button className="btn primary" onClick={()=>setModal('task')}>+ Add Task</button></div>
        <div className="kanban">{['To Do','In Progress','Blocked','Done'].map(status=><div className="kanban-col" key={status}><div className="kanban-head"><strong>{status}</strong><span>{myTasks.filter(t=>t.status===status).length}</span></div>{myTasks.filter(t=>t.status===status).map(t=><div className="kanban-card" key={t.id}><small>{t.projectId} • {t.id}</small><strong>{t.title}</strong><div className="kanban-footer"><span>Due {dateFmt(t.dueDate)}</span>{healthBadge(t.priority)}</div><div className="card-actions"><button onClick={()=>viewProject(t.projectId)}>View project</button>{t.ownerUserId===me?.id||t.contributorIds.includes(me?.id||-1)?<button onClick={()=>{setSelected(t.projectId);setEditing(t)}}>Update task</button>:null}</div></div>)}</div>)}</div>
      </>}
      {page==='project'&&<>
        <div className="toolbar card"><label>Project <select value={selected} onChange={e=>setSelected(e.target.value)}>{projects.map(p=><option key={p.id} value={p.id}>{p.name}</option>)}</select></label><span className="muted">{detail?.project.department} · PM {detail?.project.pm||'—'}</span></div>
        {detail&&<>
          <div className="project-banner card"><div><span className="overline">{detail.project.id} • {detail.project.status}</span><h2>{detail.project.name}</h2><p>{detail.project.description}</p></div><div className="banner-right">{healthBadge(detail.project.health)}<strong>{detail.project.progress}%</strong><span>Milestone-weighted progress</span>{isTimelineBA&&<button className="text-button" onClick={()=>setModal('health')}>Update health</button>}</div></div>
          <div className="project-meta card"><div><span>Current Status</span><strong>{detail.project.currentStatus}</strong>{isTimelineBA&&<button className="text-button" onClick={()=>setModal('currentStatus')}>Update phase</button>}</div><div><span>Requester • Head Office</span><strong>{detail.project.requesterName}</strong><small>{detail.project.requesterDepartment}</small></div><div><span>Timeline Owner (BA)</span><strong>{detail.members.find(m=>m.userId===detail.project.timelineOwnerUserId)?.fullName||'Not assigned'}</strong>{me?.role==='Admin'&&<button className="text-button" onClick={()=>setModal('timelineOwner')}>Reassign</button>}</div></div>
          <div className="mini-metrics"><div className="card"><span>Baseline End</span><strong>{dateFmt(detail.project.baselineEnd)}</strong></div><div className="card"><span>Forecast End</span><strong>{dateFmt(detail.project.forecastEnd)}</strong></div><div className="card"><span>Members</span><strong>{detail.members.length}</strong></div><div className="card"><span>Open Issues</span><strong>{detail.issues.filter(i=>i.status!=='Resolved').length}</strong></div></div>
          <div className="tabs">{([['timeline','Timeline & Gantt'],['tasks',`Task Register (${detail.tasks.length})`],['issues',`Issues (${detail.issues.length})`],['activity','Activity Log']] as const).map(([key,label])=><button key={key} onClick={()=>setTab(key)} className={tab===key?'active':''}>{label}</button>)}</div>
          {tab==='timeline'&&<><div className="actions-row">{(isPM&&detail.project.pmUserId===me?.id||me?.role==='Admin')&&<button className="btn secondary" onClick={()=>setModal('member')}>+ Assign BA</button>}{isTimelineBA&&<button className="btn primary" onClick={()=>setModal('milestone')}>+ Add Milestone</button>}</div><Gantt milestones={detail.milestones} editable={Boolean(isTimelineBA)} onDates={m=>setEditingMilestone(m)} onProgress={(id,p)=>void mutate(()=>api(`/milestones/${id}/progress`,{method:'PATCH',body:JSON.stringify({progress:p})}))}/><section className="card section"><div className="section-head"><div><h2>Project Members</h2><p>Multi-BA assignment</p></div></div><div className="member-chips">{detail.members.map(m=><span key={m.userId} className="member-chip">{m.fullName} <small>{m.projectRole}</small></span>)}</div></section></>}
          {tab==='tasks'&&<section className="card section"><div className="section-head"><div><h2>Task Register</h2><p>Updates persist in SQLite local</p></div>{isEditor&&<button className="btn primary" onClick={()=>setModal('task')}>+ New Task</button>}</div><div className="table-scroll"><table><thead><tr><th>Task</th><th>Owner</th><th>Priority</th><th>Due Date</th><th>Status</th><th>Action</th></tr></thead><tbody>{detail.tasks.map(t=><tr key={t.id}><td><strong>{t.title}</strong><small>{t.id}</small></td><td>{t.owner}</td><td>{healthBadge(t.priority)}</td><td className={overdue(t)?'danger-text':''}>{dateFmt(t.dueDate)}</td><td>{healthBadge(t.status)}</td><td>{canEditTask(t)?<button className="text-button" onClick={()=>setEditing(t)}>Update</button>:<span className="muted">View</span>}</td></tr>)}</tbody></table></div></section>}
          {tab==='issues'&&<section className="card section"><div className="section-head"><div><h2>Issue & Escalation Tracker</h2><p>Action plan, severity, owner, and resolution status</p></div>{isEditor&&<button className="btn primary" onClick={()=>setModal('issue')}>+ Report Issue</button>}</div><div className="table-scroll"><table><thead><tr><th>Issue</th><th>Severity</th><th>Owner</th><th>Status</th><th>Action plan</th><th></th></tr></thead><tbody>{detail.issues.map(i=><tr key={i.id}><td><strong>{i.title}</strong><small>{i.id} · {i.impact}</small></td><td>{healthBadge(i.severity)}</td><td>{i.owner}</td><td>{healthBadge(i.status)}</td><td>{i.actionPlan}</td><td>{canEditIssue(i)&&<button className="text-button" onClick={()=>setEditingIssue(i)}>Update</button>}</td></tr>)}</tbody></table></div></section>}
          {tab==='activity'&&<section className="card section"><div className="section-head"><div><h2>Audit activity</h2><p>Most recent task changes in this project</p></div></div>{detail.activities.length?detail.activities.map(a=><div className="activity" key={a.id}><div className="activity-icon">↻</div><div><strong>{a.action} — {a.taskId}</strong><p>{a.oldValue||'New'} → {a.newValue} {a.note?` • ${a.note}`:''}</p><small>{new Date(a.createdAt).toLocaleString('id-ID')}</small></div></div>):<p className="empty">No task activity yet.</p>}</section>}
        </>}
      </>}
      <footer className="footer">ProjectPulse v0.4 • API-backed MVP • SQLite local → PostgreSQL development</footer>
      </div>
    </main>
    {editing&&<TaskEdit task={editing} isPM={true} busy={busy} onCancel={()=>setEditing(null)} onSave={(body)=>void mutate(()=>api(`/tasks/${editing.id}`,{method:'PATCH',body:JSON.stringify(body)}))}/>}
    {editingIssue&&<IssueEdit issue={editingIssue} busy={busy} onCancel={()=>setEditingIssue(null)} onSave={(body)=>void mutate(()=>api(`/issues/${editingIssue.id}`,{method:'PATCH',body:JSON.stringify(body)}))}/>}
    {modal==='task'&&me&&<TaskCreate projects={projects} users={users} detail={detail} initialId={page==='project'?selected:projects[0]?.id||''} current={me} busy={busy} onClose={()=>setModal(null)} onSave={(id,body)=>void mutate(()=>api(`/projects/${id}/tasks`,{method:'POST',body:JSON.stringify(body)}))}/>}
    {modal==='issue'&&detail&&<IssueCreate project={detail.project} busy={busy} onClose={()=>setModal(null)} onSave={body=>void mutate(()=>api(`/projects/${selected}/issues`,{method:'POST',body:JSON.stringify(body)}))}/>}
    {modal==='health'&&isTimelineBA&&detail&&<HealthEdit project={detail.project} busy={busy} onClose={()=>setModal(null)} onSave={body=>void mutate(()=>api(`/projects/${selected}/health`,{method:'PATCH',body:JSON.stringify(body)}))}/>}
    {modal==='member'&&(isPM||me?.role==='Admin')&&detail&&<MemberCreate users={users} detail={detail} busy={busy} onClose={()=>setModal(null)} onSave={body=>void mutate(()=>api(me?.role==='Admin'?`/admin/projects/${selected}/members`:`/projects/${selected}/members`,{method:'POST',body:JSON.stringify(body)}))}/>}
    {modal==='milestone'&&isTimelineBA&&detail&&<MilestoneCreate detail={detail} busy={busy} onClose={()=>setModal(null)} onSave={body=>void mutate(()=>api(`/projects/${selected}/milestones`,{method:'POST',body:JSON.stringify(body)}))}/>}
    {modal==='project'&&isPM&&<ProjectCreate departments={departments} users={users} busy={busy} onClose={()=>setModal(null)} onSave={body=>void mutate(()=>api('/projects',{method:'POST',body:JSON.stringify(body)}))}/>}
    {modal==='currentStatus'&&detail&&<CurrentStatusEdit project={detail.project} busy={busy} onClose={()=>setModal(null)} onSave={body=>void mutate(()=>api(`/projects/${selected}/current-status`,{method:'PATCH',body:JSON.stringify(body)}))}/>}
    {editingMilestone&&<MilestoneDatesEdit milestone={editingMilestone} busy={busy} onClose={()=>setEditingMilestone(null)} onSave={body=>void mutate(async()=>{await api(`/milestones/${editingMilestone.id}/dates`,{method:'PATCH',body:JSON.stringify(body)});setEditingMilestone(null)})}/>}
    {modal==='adminUser'&&me?.role==='Admin'&&<AdminUserEdit current={editingAdmin} users={adminUsers} departments={departments} busy={busy} onClose={()=>setModal(null)} onSave={body=>void mutate(()=>api(editingAdmin?`/admin/users/${editingAdmin.id}`:'/admin/users',{method:editingAdmin?'PATCH':'POST',body:JSON.stringify(body)}))}/>}
    {modal==='timelineOwner'&&me?.role==='Admin'&&detail&&<TimelineOwnerEdit detail={detail} users={users} busy={busy} onClose={()=>setModal(null)} onSave={body=>void mutate(()=>api(`/admin/projects/${selected}/timeline-owner`,{method:'PATCH',body:JSON.stringify(body)}))}/>}
  </div>;
}
function ProjectTable({projects,onOpen,heading}:{projects:Project[];onOpen:(id:string)=>void;heading:string}){
  return <section className="card section"><div className="section-head"><div><h2>{heading}</h2><p>{projects.length} projects in selection</p></div></div><div className="table-scroll"><table><thead><tr><th>Project</th><th>Department</th><th>PM</th><th>Progress</th><th>Baseline End</th><th>Health</th><th>Current Status</th><th></th></tr></thead><tbody>{projects.map(p=><tr key={p.id}><td><button className="project-link" onClick={()=>onOpen(p.id)}>{p.name}</button><small>{p.id}</small></td><td>{p.department}</td><td>{p.pm}</td><td className="progress-cell"><strong>{p.progress}%</strong>{progress(p.progress)}</td><td>{dateFmt(p.baselineEnd)}</td><td>{healthBadge(p.health)}</td><td><span className="phase-label">{p.currentStatus}</span></td><td><button className="text-button" onClick={()=>onOpen(p.id)}>Details →</button></td></tr>)}</tbody></table>{!projects.length&&<p className="empty">No projects found.</p>}</div></section>;
}
function Gantt({milestones,editable,onProgress,onDates}:{milestones:Detail['milestones'];editable:boolean;onProgress:(id:number,progress:number)=>void;onDates:(m:Detail['milestones'][number])=>void}){
  const times=milestones.flatMap(m=>[new Date(m.plannedStart+'T00:00:00').getTime(),new Date((m.forecastEnd||m.plannedEnd)+'T00:00:00').getTime()]);
  const min=Math.min(...times);const max=Math.max(...times);const range=Math.max(86400000,max-min+86400000);
  return <section className="card section"><div className="section-head"><div><h2>Milestone Timeline</h2><p>Baseline and forecast, illustrative milestones until official approval workflow</p></div></div>
    <div className="gantt-list"><div className="gantt-header"><span>Milestone</span><span>Timeline <small>{dateFmt(milestones[0]?.plannedStart)} – {dateFmt(milestones[milestones.length-1]?.forecastEnd||milestones[milestones.length-1]?.plannedEnd)}</small></span><span>Progress</span></div>
      {milestones.map(m=>{const start=(new Date(m.plannedStart+'T00:00:00').getTime()-min)/range*100;
        const width=(new Date((m.forecastEnd||m.plannedEnd)+'T00:00:00').getTime()-new Date(m.plannedStart+'T00:00:00').getTime()+86400000)/range*100;
        return <div className="gantt-row" key={m.id}><strong>{m.name}<small>Weight {m.weight}%</small></strong><div className="gantt-track"><div className="gantt-bar" style={{left:`${start}%`,width:`${width}%`}}><span style={{width:`${m.progress}%`}}/></div></div><b>{m.progress}% {editable&&<button className="text-button edit-milestone" onClick={()=>{const v=prompt(`Approved milestone progress (0-100): ${m.name}`,String(m.progress));if(v===null)return;const parsed=Number(v);if(Number.isInteger(parsed)&&parsed>=0&&parsed<=100)onProgress(m.id,parsed);else alert('Enter a whole number between 0 and 100')}}>Progress</button>}{editable&&<button className="text-button" onClick={()=>onDates(m)}>Dates</button>}</b></div>})}</div>
    <p className="helper">Progress milestones di seed masih data simulasi. Persentase project tidak diperbarui otomatis ketika BA menandai task selesai.</p>
  </section>;
}
function Dialog({title,children,onClose}:{title:string;children:ReactNode;onClose:()=>void}){
  return <div className="modal-backdrop" role="presentation" onMouseDown={e=>{if(e.target===e.currentTarget)onClose()}}><section className="modal" role="dialog" aria-modal="true" aria-label={title}><div className="modal-header"><h2>{title}</h2><button onClick={onClose} aria-label="Close dialog">×</button></div>{children}</section></div>;
}
function TaskEdit({task,isPM,busy,onCancel,onSave}:{task:Task;isPM:boolean;busy:boolean;onCancel:()=>void;onSave:(body:object)=>void}){
  const [status,setStatus]=useState(task.status);const [note,setNote]=useState('');const [due,setDue]=useState(task.dueDate);
  return <Dialog title={`Update Task • ${task.id}`} onClose={onCancel}><form onSubmit={e=>{e.preventDefault();onSave({status,note,version:task.version,...(isPM&&due!==task.dueDate?{dueDate:due}:{})})}}>
    <p className="modal-detail">{task.title}</p><label>Status<select value={status} onChange={e=>setStatus(e.target.value)}>{['To Do','In Progress','Blocked','Done','Cancelled'].map(s=><option key={s}>{s}</option>)}</select></label>
    {isPM&&<label>Due Date<input type="date" value={due} onChange={e=>setDue(e.target.value)} required/></label>}
    <label>Update Note<textarea value={note} onChange={e=>setNote(e.target.value)} placeholder="What changed?" rows={3}/></label><div className="modal-actions"><button className="btn secondary" type="button" onClick={onCancel}>Cancel</button><button className="btn primary" disabled={busy}>Save Changes</button></div></form></Dialog>;
}
function IssueEdit({issue,busy,onCancel,onSave}:{issue:Issue;busy:boolean;onCancel:()=>void;onSave:(body:object)=>void}){
  const [status,setStatus]=useState(issue.status);const [actionPlan,setPlan]=useState(issue.actionPlan);
  return <Dialog title={`Update Issue • ${issue.id}`} onClose={onCancel}><form onSubmit={e=>{e.preventDefault();onSave({status,actionPlan})}}><p className="modal-detail">{issue.title}</p><label>Status<select value={status} onChange={e=>setStatus(e.target.value)}>{['Open','Escalated','Resolved'].map(s=><option key={s}>{s}</option>)}</select></label><label>Action Plan<textarea value={actionPlan} onChange={e=>setPlan(e.target.value)} rows={3}/></label><div className="modal-actions"><button type="button" className="btn secondary" onClick={onCancel}>Cancel</button><button className="btn primary" disabled={busy}>Save Issue</button></div></form></Dialog>;
}
function TaskCreate({projects,users,detail,initialId,current,busy,onClose,onSave}:{projects:Project[];users:User[];detail:Detail|null;initialId:string;current:User;busy:boolean;onClose:()=>void;onSave:(projectId:string,body:object)=>void}){
  const [projectId,setProject]=useState(initialId);const [title,setTitle]=useState('');const [description,setDescription]=useState('');const [priority,setPriority]=useState('Medium');const [owner,setOwner]=useState(current.id);
  const [dueDate,setDue]=useState(today());const [milestone,setMilestone]=useState('');
  const memberIds=projectId===detail?.project.id?detail.members.map(m=>m.userId):users.map(u=>u.id);
  const choices=users.filter(u=>memberIds.includes(u.id) && u.role==='BA');
  return <Dialog title="Create Task" onClose={onClose}><form onSubmit={e=>{e.preventDefault();onSave(projectId,{title,description,priority,ownerUserId:current.role==='BA'?current.id:owner,dueDate,milestoneId:projectId===detail?.project.id&&milestone?Number(milestone):null})}}>
    <label>Project<select value={projectId} onChange={e=>{setProject(e.target.value);setMilestone('')}}>{projects.map(p=><option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
    <label>Task Title<input value={title} onChange={e=>setTitle(e.target.value)} required minLength={3}/></label><label>Description<textarea rows={2} value={description} onChange={e=>setDescription(e.target.value)}/></label>
    <div className="form-cols"><label>Priority<select value={priority} onChange={e=>setPriority(e.target.value)}>{['Low','Medium','High','Critical'].map(s=><option key={s}>{s}</option>)}</select></label><label>Due Date<input type="date" value={dueDate} onChange={e=>setDue(e.target.value)} required/></label></div>
    {false&&<label>Owner<select value={owner} onChange={e=>setOwner(Number(e.target.value))}>{choices.map(u=><option key={u.id} value={u.id}>{u.fullName}</option>)}</select></label>}
    {projectId===detail?.project.id&&<label>Milestone (optional)<select value={milestone} onChange={e=>setMilestone(e.target.value)}><option value="">Unassigned</option>{detail.milestones.map(m=><option key={m.id} value={m.id}>{m.name}</option>)}</select></label>}
    <div className="modal-actions"><button type="button" className="btn secondary" onClick={onClose}>Cancel</button><button disabled={busy||!projectId} className="btn primary">Create Task</button></div></form></Dialog>;
}
function IssueCreate({project,busy,onClose,onSave}:{project:Project;busy:boolean;onClose:()=>void;onSave:(body:object)=>void}){
  const [title,setTitle]=useState('');const [severity,setSeverity]=useState('Medium');const [impact,setImpact]=useState('');const [actionPlan,setPlan]=useState('');const [dueDate,setDue]=useState(today());
  return <Dialog title={`Report Issue • ${project.id}`} onClose={onClose}><form onSubmit={e=>{e.preventDefault();onSave({title,severity,impact,actionPlan,dueDate,taskId:null})}}><label>Issue Title<input value={title} onChange={e=>setTitle(e.target.value)} required/></label><label>Severity<select value={severity} onChange={e=>setSeverity(e.target.value)}>{['Low','Medium','High','Critical'].map(s=><option key={s}>{s}</option>)}</select></label><label>Impact<textarea value={impact} onChange={e=>setImpact(e.target.value)} rows={2}/></label><label>Action Plan<textarea value={actionPlan} onChange={e=>setPlan(e.target.value)} rows={2}/></label><label>Target Resolution<input type="date" value={dueDate} onChange={e=>setDue(e.target.value)} /></label><div className="modal-actions"><button type="button" className="btn secondary" onClick={onClose}>Cancel</button><button className="btn primary" disabled={busy}>Save Issue</button></div></form></Dialog>;
}
function ProjectCreate({departments,users,busy,onClose,onSave}:{departments:Department[];users:User[];busy:boolean;onClose:()=>void;onSave:(body:object)=>void}){
  const ba=users.filter(u=>u.role==='BA' && u.isActive!==false);
  const [name,setName]=useState('');const [description,setDescription]=useState('');const [departmentId,setDepartmentId]=useState(departments[0]?.id||1);
  const [startDate,setStart]=useState(today());const [baselineEnd,setEnd]=useState(today());
  const [timelineOwnerUserId,setTimelineOwner]=useState(ba[0]?.id||0);const [requesterName,setRequesterName]=useState('');const [requesterDepartment,setRequesterDepartment]=useState('');
  return <Dialog title="Create Project with REQ ID" onClose={onClose}><form onSubmit={e=>{e.preventDefault();onSave({name,description,departmentId,startDate,baselineEnd,timelineOwnerUserId,requesterName,requesterDepartment})}}>
    <p className="helper">REQ ID is generated automatically and uniquely when saved, e.g. REQ20261010001.</p>
    <label>Project Name<input value={name} minLength={3} onChange={e=>setName(e.target.value)} required/></label>
    <label>IT Department<select value={departmentId} onChange={e=>setDepartmentId(Number(e.target.value))}>{departments.map(d=><option key={d.id} value={d.id}>{d.name}</option>)}</select></label>
    <label>BA Timeline Owner<select value={timelineOwnerUserId} onChange={e=>setTimelineOwner(Number(e.target.value))} required>{ba.map(u=><option value={u.id} key={u.id}>{u.fullName} • home PM {users.find(pm=>pm.id===u.reportsToPmUserId)?.fullName||'-'}</option>)}</select></label>
    <div className="form-cols"><label>HO Requester Name<input value={requesterName} onChange={e=>setRequesterName(e.target.value)} required placeholder="User Head Office"/></label><label>HO Department<input value={requesterDepartment} onChange={e=>setRequesterDepartment(e.target.value)} required placeholder="HRD / Finance / Risk"/></label></div>
    <label>Description<textarea value={description} onChange={e=>setDescription(e.target.value)} rows={2}/></label>
    <div className="form-cols"><label>Start Date<input type="date" required value={startDate} onChange={e=>setStart(e.target.value)}/></label><label>Baseline End<input type="date" required min={startDate} value={baselineEnd} onChange={e=>setEnd(e.target.value)}/></label></div>
    <div className="modal-actions"><button type="button" className="btn secondary" onClick={onClose}>Cancel</button><button className="btn primary" disabled={busy||!timelineOwnerUserId}>Create Project</button></div>
  </form></Dialog>;
}

function MemberCreate({users,detail,busy,onClose,onSave}:{users:User[];detail:Detail;busy:boolean;onClose:()=>void;onSave:(body:object)=>void}){
  const options=users.filter(u=>u.role==='BA' && u.isActive!==false && !detail.members.some(m=>m.userId===u.id));
  const [userId,setUserId]=useState(options[0]?.id||0);
  return <Dialog title="Add Project Member" onClose={onClose}><form onSubmit={e=>{e.preventDefault();onSave({userId})}}>
    <p className="helper">Borrowed BA tetap berada di home team PM asal. Assigned BA dapat bekerja lintas project tanpa pindah atasan.</p>
    <label>Employee<select value={userId} onChange={e=>setUserId(Number(e.target.value))}>{options.map(u=><option key={u.id} value={u.id}>{u.fullName} ({u.role})</option>)}</select></label>
    <div className="modal-actions"><button type="button" className="btn secondary" onClick={onClose}>Cancel</button><button className="btn primary" disabled={!options.length||busy}>Add Member</button></div>
  </form></Dialog>;
}
function MilestoneCreate({detail,busy,onClose,onSave}:{detail:Detail;busy:boolean;onClose:()=>void;onSave:(body:object)=>void}){
  const used=detail.milestones.reduce((s,m)=>s+m.weight,0);
  const remaining=Math.max(0,100-used);
  const [name,setName]=useState('');const [weight,setWeight]=useState(remaining);
  const [plannedStart,setStart]=useState(detail.project.startDate);
  const [plannedEnd,setEnd]=useState(detail.project.baselineEnd);
  return <Dialog title="Add Milestone" onClose={onClose}><form onSubmit={e=>{e.preventDefault();onSave({name,weight,plannedStart,plannedEnd})}}>
    <p className="helper">Total weight harus tepat 100% agar progress project dihitung dari milestone yang di-update BA. Available: {remaining}%.</p>
    <label>Milestone Name<input required value={name} onChange={e=>setName(e.target.value)} placeholder="Requirement / Design / SIT"/></label>
    <label>Weight (%)<input type="number" min={1} max={remaining} value={weight} onChange={e=>setWeight(Number(e.target.value))} required/></label>
    <div className="form-cols"><label>Baseline Start<input type="date" required value={plannedStart} onChange={e=>setStart(e.target.value)}/></label><label>Baseline End<input type="date" min={plannedStart} required value={plannedEnd} onChange={e=>setEnd(e.target.value)}/></label></div>
    <div className="modal-actions"><button type="button" className="btn secondary" onClick={onClose}>Cancel</button><button className="btn primary" disabled={busy||remaining===0}>Create Milestone</button></div>
  </form></Dialog>;
}

function HealthEdit({project,busy,onClose,onSave}:{project:Project;busy:boolean;onClose:()=>void;onSave:(body:object)=>void}){
  const [health,setHealth]=useState(project.health);const [reason,setReason]=useState('');
  return <Dialog title="Update Project Health" onClose={onClose}><form onSubmit={e=>{e.preventDefault();onSave({health,reason})}}>
    <label>Health Status<select value={health} onChange={e=>setHealth(e.target.value)}>{['On Track','At Risk','Delayed','Blocked'].map(s=><option key={s}>{s}</option>)}</select></label>
    <label>Reason / Evidence<textarea required value={reason} onChange={e=>setReason(e.target.value)} rows={3} placeholder="Explain why status changed"/></label>
    <p className="helper">Status ini harus didasarkan pada milestone, risiko, hambatan dan forecast, bukan pada persentase task Done saja.</p>
    <div className="modal-actions"><button type="button" className="btn secondary" onClick={onClose}>Cancel</button><button className="btn primary" disabled={busy||!reason.trim()}>Update Health</button></div>
  </form></Dialog>;
}

const projectStages=['Requirement','Design','Development','SIT','UAT','Implementation','PIR','On Hold','Completed'];
function PortfolioCharts({projects}:{projects:Project[]}){
  const total=projects.length;
  const green=projects.filter(p=>p.health==='On Track').length;
  const amber=projects.filter(p=>p.health==='At Risk').length;
  const red=projects.filter(p=>['Delayed','Blocked'].includes(p.health)).length;
  const gradient=total?`conic-gradient(#12ae89 0 ${green/total*100}%, #f1ae45 ${green/total*100}% ${(green+amber)/total*100}%, #ee6672 ${(green+amber)/total*100}% 100%)`:'conic-gradient(#dce3ec 0 100%)';
  const groups=projectStages.map(stage=>({stage,count:projects.filter(p=>p.currentStatus===stage).length})).filter(x=>x.count);
  const months=Array.from(new Set(projects.map(p=>p.baselineEnd.slice(0,7)))).sort().slice(0,7);
  return <div className="insight-grid">
    <section className="card insight-card"><div className="section-head"><div><h2>Portfolio Health</h2><p>Project count by health category</p></div></div>
      <div className="donut-layout"><div className="donut" style={{background:gradient}}><div><b>{total}</b><small>projects</small></div></div><div className="donut-legend"><span><i style={{background:'#12ae89'}}/>On Track <b>{green}</b></span><span><i style={{background:'#f1ae45'}}/>At Risk <b>{amber}</b></span><span><i style={{background:'#ee6672'}}/>Delayed / Blocked <b>{red}</b></span></div></div>
    </section>
    <section className="card insight-card"><div className="section-head"><div><h2>Delivery Pipeline</h2><p>Current status distribution • count of projects</p></div></div>
      <div className="stage-bars">{groups.length?groups.map(g=><div className="stage-row" key={g.stage}><span>{g.stage}</span><div className="stage-track"><i style={{width:`${total?g.count/total*100:0}%`}}/></div><b>{g.count}</b></div>):<div className="muted">No projects</div>}</div>
    </section>
    <section className="card insight-card deadline-card"><div className="section-head"><div><h2>Delivery Calendar</h2><p>Number of projects due by baseline month</p></div></div>
      <div className="month-chart">{months.length?months.map(m=>{const v=projects.filter(p=>p.baselineEnd.startsWith(m)).length;return <div key={m} className="month-item"><strong>{v}</strong><div className="month-track"><div style={{height:`${v/Math.max(1,...months.map(k=>projects.filter(p=>p.baselineEnd.startsWith(k)).length))*100}%`}}/></div><span>{m.slice(5)}/{m.slice(2,4)}</span></div>}):<div className="muted">No planned deliveries</div>}</div>
    </section>
  </div>;
}
function CurrentStatusEdit({project,busy,onClose,onSave}:{project:Project;busy:boolean;onClose:()=>void;onSave:(body:object)=>void}){
  const [currentStatus,setStatus]=useState(project.currentStatus);const [note,setNote]=useState('');
  return <Dialog title={`Current Status • ${project.id}`} onClose={onClose}><form onSubmit={e=>{e.preventDefault();onSave({currentStatus,note})}}>
    <label>Project Phase<select value={currentStatus} onChange={e=>setStatus(e.target.value)}>{projectStages.map(x=><option key={x}>{x}</option>)}</select></label>
    <label>Update Note<textarea required minLength={3} value={note} onChange={e=>setNote(e.target.value)} rows={3} placeholder="e.g. SIT underway; awaiting HO sign-off"/></label>
    <p className="helper">Current Status is a phase. Health is a separate risk indicator; for example, Development + Blocked.</p>
    <div className="modal-actions"><button className="btn secondary" type="button" onClick={onClose}>Cancel</button><button className="btn primary" disabled={busy||!note.trim()}>Save Status</button></div></form></Dialog>;
}
function MilestoneDatesEdit({milestone,busy,onClose,onSave}:{milestone:Detail['milestones'][number];busy:boolean;onClose:()=>void;onSave:(body:object)=>void}){
  const [plannedStart,setStart]=useState(milestone.plannedStart);const [plannedEnd,setEnd]=useState(milestone.plannedEnd);const [forecastEnd,setForecast]=useState(milestone.forecastEnd||milestone.plannedEnd);const [reason,setReason]=useState('');
  return <Dialog title={`Timeline • ${milestone.name}`} onClose={onClose}><form onSubmit={e=>{e.preventDefault();onSave({plannedStart,plannedEnd,forecastEnd,reason})}}>
    <div className="form-cols"><label>Baseline Start<input type="date" value={plannedStart} onChange={e=>setStart(e.target.value)} required/></label><label>Baseline End<input type="date" min={plannedStart} value={plannedEnd} onChange={e=>setEnd(e.target.value)} required/></label></div>
    <label>Forecast End<input type="date" min={plannedStart} value={forecastEnd} onChange={e=>setForecast(e.target.value)} required/></label>
    <label>Reason for Change<textarea value={reason} onChange={e=>setReason(e.target.value)} rows={3} minLength={3} required/></label>
    <div className="modal-actions"><button type="button" className="btn secondary" onClick={onClose}>Cancel</button><button className="btn primary" disabled={busy}>Update Dates</button></div></form></Dialog>;
}
function AdminUserEdit({current,users,departments,busy,onClose,onSave}:{current:User|null;users:User[];departments:Department[];busy:boolean;onClose:()=>void;onSave:(body:object)=>void}){
  const [fullName,setName]=useState(current?.fullName||'');const [email,setEmail]=useState(current?.email||'');const [role,setRole]=useState<string>(current?.role||'BA');const [departmentId,setDept]=useState(current?.departmentId||departments[0]?.id||1);
  const [reportsToPmUserId,setPM]=useState<number>(current?.reportsToPmUserId||users.find(x=>x.role==='PM')?.id||0);
  const [isActive,setActive]=useState(current?.isActive!==false);
  return <Dialog title={current?`Edit Member • ${current.fullName}`:'Add Member'} onClose={onClose}><form onSubmit={e=>{e.preventDefault();onSave({fullName,email,role,departmentId,reportsToPmUserId:role==='BA'?reportsToPmUserId:null,isActive})}}>
    <label>Full Name<input required value={fullName} onChange={e=>setName(e.target.value)}/></label><label>Email<input type="email" required value={email} onChange={e=>setEmail(e.target.value)}/></label>
    <div className="form-cols"><label>Role<select value={role} onChange={e=>setRole(e.target.value)}>{['Admin','DivisionHead','DepartmentHead','PM','BA'].map(x=><option value={x} key={x}>{roleTitle(x)}</option>)}</select></label><label>Department<select value={departmentId} onChange={e=>setDept(Number(e.target.value))}>{departments.map(x=><option value={x.id} key={x.id}>{x.name}</option>)}</select></label></div>
    {role==='BA'&&<label>Home Team • Reporting PM<select value={reportsToPmUserId} onChange={e=>setPM(Number(e.target.value))}>{users.filter(x=>x.role==='PM'&&x.isActive!==false).map(x=><option value={x.id} key={x.id}>{x.fullName}</option>)}</select></label>}
    {current&&<label className="check-label"><input type="checkbox" checked={isActive} onChange={e=>setActive(e.target.checked)}/> Active account</label>}
    <p className="helper">Changing the reporting PM transfers the BA home team only. Existing project assignments remain intact. New demo users get initial password Demo@12345, stored as a salted hash.</p>
    <div className="modal-actions"><button className="btn secondary" type="button" onClick={onClose}>Cancel</button><button className="btn primary" disabled={busy||role==='BA'&&!reportsToPmUserId}>{current?'Save Member':'Create Member'}</button></div></form></Dialog>;
}
function TimelineOwnerEdit({detail,users,busy,onClose,onSave}:{detail:Detail;users:User[];busy:boolean;onClose:()=>void;onSave:(body:object)=>void}){
  const assigned=detail.members.filter(m=>users.some(u=>u.id===m.userId&&u.role==='BA'&&u.isActive!==false));
  const [userId,setUserId]=useState(detail.project.timelineOwnerUserId||assigned[0]?.userId||0);
  return <Dialog title="Reassign Timeline Owner" onClose={onClose}><form onSubmit={e=>{e.preventDefault();onSave({userId})}}>
    <label>Assigned BA<select value={userId} onChange={e=>setUserId(Number(e.target.value))}>{assigned.map(x=><option value={x.userId} key={x.userId}>{x.fullName}</option>)}</select></label>
    <p className="helper">Only an assigned BA can be the timeline owner. Borrowed BAs are eligible. To assign someone new, add the BA as a member first.</p>
    <div className="modal-actions"><button className="btn secondary" type="button" onClick={onClose}>Cancel</button><button className="btn primary" disabled={busy||!userId}>Reassign</button></div></form></Dialog>;
}
