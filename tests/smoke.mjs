// Staging-only destructive smoke test. NEVER run against a real client database.
// Usage: ALLOW_MUTATING_SMOKE=1 BASE_URL=https://staging.example.com SMOKE_EMAIL=... SMOKE_PASSWORD=... node tests/smoke.mjs
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
const base = (process.env.BASE_URL || '').replace(/\/$/, '');
if (process.env.ALLOW_MUTATING_SMOKE !== '1' || !base || !process.env.SMOKE_EMAIL || !process.env.SMOKE_PASSWORD) {
  throw new Error('Teste cria registros. Requer ALLOW_MUTATING_SMOKE=1, BASE_URL, SMOKE_EMAIL, SMOKE_PASSWORD. Use só staging.');
}
const cookies = new Map(); let csrf = '';
const request = async (path, method='GET', payload) => {
  const headers = {accept:'application/json'};
  if (cookies.size) headers.cookie = [...cookies.entries()].map(([k,v])=>`${k}=${v}`).join('; ');
  if (method !== 'GET') {headers['content-type']='application/json';headers['x-csrf-token']=csrf;}
  const r = await fetch(`${base}/api/v1${path}`, {method,headers,body:payload===undefined?undefined:JSON.stringify(payload),redirect:'manual'});
  for (const sc of r.headers.getSetCookie?.() || []) {const cookie = sc.split(';')[0];const p=cookie.indexOf('=');if(p>0) cookies.set(cookie.slice(0,p),cookie.slice(p+1));}
  const out = await r.text();let data;try{data=JSON.parse(out)}catch{data=out}
  return {status:r.status,data};
};
const expect = (r, code) => {assert.equal(r.status,code,JSON.stringify(r.data));return r.data};
csrf = expect(await request('/auth/antiforgery'),200).token;
expect(await request('/auth/login','POST',{email:process.env.SMOKE_EMAIL,password:process.env.SMOKE_PASSWORD}),200);
expect(await request('/auth/me'),200);
const stamp=randomUUID().slice(0,8);
const school=expect(await request('/schools','POST',{name:`Smoke Test ${stamp}`,address:'Rua de teste 101, São Paulo'}),201);
const student=expect(await request('/students','POST',{fullName:`Aluno Teste ${stamp}`,schoolId:school.id,shift:'Manhã',pickupAddress:'Rua A, 1',dropoffAddress:'Rua B, 2',guardianName:'Responsável Exemplo',guardianPhone:'11999999999'}),201);
const now=new Date();const month=`${now.getUTCFullYear()}-${String(now.getUTCMonth()+1).padStart(2,'0')}`;
expect(await request('/contracts','POST',{studentId:student.id,monthlyAmount:123.45,dueDay:28,startsOn:`${month}-01`,installments:12}),201);
const run1=expect(await request('/billing/generate','POST',{month}),200);assert.ok(run1.created>=1);
const run2=expect(await request('/billing/generate','POST',{month}),200);assert.equal(run2.created,0);
const rows=expect(await request(`/charges?month=${month}`),200);const charge=rows.find(x=>x.studentId===student.id);assert.ok(charge,'Cobrança de teste não encontrada');
const key=randomUUID();const pay={amount:123.45,method:'PIX',idempotencyKey:key};
const once=expect(await request(`/charges/${charge.id}/payments`,'POST',pay),200);
const twice=expect(await request(`/charges/${charge.id}/payments`,'POST',pay),200);assert.equal(once.id,twice.id);
const after=expect(await request(`/charges?month=${month}`),200).find(x=>x.id===charge.id);assert.equal(after.status,'Paid');
expect(await request('/auth/logout','POST',{}),204);
const unauth=await request('/auth/me');assert.equal(unauth.status,401);
console.log('SMOKE OK (staging): login, CRUD mínimo, billing idempotente, pagamento idempotente, logout.');
