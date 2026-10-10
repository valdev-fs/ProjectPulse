export type Role='DivisionHead'|'DepartmentHead'|'PM'|'BA'|'Admin';
export interface User { id:number; fullName:string; email?:string; role:Role; departmentId:number;reportsToPmUserId?:number|null;isActive?:boolean }
export interface Department { id:number; name:string }
export interface Project {
  id:string; name:string; description:string; departmentId:number;department:string;pmUserId:number;pm:string;
  progress:number;health:string;status:string;currentStatus:string;requesterName:string;requesterDepartment:string;timelineOwnerUserId:number|null;startDate:string;baselineEnd:string;forecastEnd:string|null;updatedAt:string;
}
export interface Task {
  id:string;projectId:string;milestoneId:number|null;milestone:string|null;title:string;description:string;
  ownerUserId:number;owner:string;status:string;priority:string;dueDate:string;version:number;updatedAt:string;contributorIds:number[];
}
export interface Milestone {id:number;name:string;weight:number;progress:number;plannedStart:string;plannedEnd:string;forecastEnd:string|null;status:string}
export interface Issue {id:string;projectId:string;taskId:string|null;title:string;severity:string;status:string;impact:string;actionPlan:string;dueDate:string|null;ownerUserId:number;owner:string}
export interface Activity {id:number;taskId:string;action:string;oldValue:string;newValue:string;note:string;actorUserId:number;createdAt:string}
export interface Detail {project:Project;members:{userId:number;fullName:string;projectRole:string}[];milestones:Milestone[];tasks:Task[];issues:Issue[];activities:Activity[]}
export interface Portfolio {total:number;onTrack:number;atRisk:number;delayed:number;blocked:number;overdueTasks:number;openIssues:number;
  departments:{name:string;total:number;onTrack:number;atRisk:number;delayed:number;blocked:number}[];projects:Project[]}

// Local demo: opaque bearer token in sessionStorage (cleared when browser tab closes).
// On hosted PostgreSQL, replace this with Microsoft Entra ID/MSAL access token retrieval.
const tokenKey = 'projectpulse-local-session';
export const getToken=()=>sessionStorage.getItem(tokenKey);
export const setToken=(token:string)=>sessionStorage.setItem(tokenKey,token);
export const clearToken=()=>sessionStorage.removeItem(tokenKey);
export class ApiError extends Error {constructor(public status:number,message:string){super(message)}}
export async function logoutLocal():Promise<void>{
  const token=getToken();
  if(!token)return;
  await fetch('/api/auth/logout',{method:'POST',headers:{'Content-Type':'application/json',Authorization:`Bearer ${token}`},body:'{}'});
}
export async function authRequest<T>(path:string,body:object):Promise<T>{
  const response=await fetch(`/api/auth/${path}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
  const data=await response.json().catch(()=>({}));
  if(!response.ok)throw new ApiError(response.status,data.error||`HTTP ${response.status}`);
  return data as T;
}
export async function api<T>(path:string, options:RequestInit={}):Promise<T> {
  const token=getToken();
  const res=await fetch(`/api${path}`,{
    ...options,
    headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{}) ,...options.headers}
  });
  if(res.status===401){clearToken();window.dispatchEvent(new Event('pp-auth-expired'))}
  if(!res.ok){const error=await res.json().catch(()=>({}));throw new ApiError(res.status,error.error||`HTTP ${res.status}`)}
  return res.json() as Promise<T>;
}
