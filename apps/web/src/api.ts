export type School = { id: string; name: string; address: string; active: boolean };
export type Student = { id: string; fullName: string; schoolId: string; schoolName: string; shift: string; pickupAddress: string; dropoffAddress: string; guardianName: string; guardianPhone: string; active: boolean };
export type Contract = { id: string; studentId: string; studentName: string; monthlyAmount: number; dueDay: number; startsOn: string; installments: number; status: string };
export type Charge = { id: string; studentId: string; studentName: string; competence: string; dueDate: string; amount: number; paidAmount: number; status: string };
export type Expense = { id: string; category: string; description: string; amount: number; date: string; status: string };
export type Absence = { id: string; studentName: string; studentId: string; date: string; period: string; reason: string; acknowledged: boolean };
export type Summary = { activeStudents: number; activeSchools: number; expected: number; received: number; outstanding: number; overdue: number; expenses: number; balance: number; absencesToday: number };
export type Session = { email: string; operationName: string };
export class ApiError extends Error { constructor(public status: number, message: string) { super(message); } }
let csrf: string | null = null;
async function getCsrf(): Promise<string> {
  if (csrf) return csrf;
  const resp = await fetch('/api/v1/auth/antiforgery', { credentials: 'same-origin', cache: 'no-store' });
  if (!resp.ok) throw new ApiError(resp.status, 'Não foi possível preparar sua sessão segura.');
  const data = await resp.json() as { token: string };
  csrf = data.token;
  return csrf;
}
export async function request<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const mutating = !['GET', 'HEAD'].includes(method);
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (mutating) { headers['Content-Type'] = 'application/json'; headers['X-CSRF-TOKEN'] = await getCsrf(); }
  const resp = await fetch('/api/v1' + path, { method, credentials: 'same-origin', cache: 'no-store', headers, body: body === undefined ? undefined : JSON.stringify(body) });
  if (!resp.ok) {
    const data: { detail?: string; title?: string; error?: string } = await resp.json().catch(() => ({}));
    if (resp.status === 403 && mutating) csrf = null;
    throw new ApiError(resp.status, data.detail || data.error || data.title || `Ocorreu um erro (${resp.status}).`);
  }
  if (resp.status === 204) return undefined as T;
  return resp.json() as Promise<T>;
}
export const api = {
  me: () => request<Session>('/auth/me'),
  login: (email: string, password: string) => request<Session>('/auth/login', 'POST', { email, password }),
  logout: async () => { await request<void>('/auth/logout', 'POST', {}); csrf = null; },
  schools: () => request<School[]>('/schools'),
  addSchool: (name: string, address: string) => request<School>('/schools', 'POST', { name, address }),
  students: () => request<Student[]>('/students'),
  addStudent: (input: unknown) => request<Student>('/students', 'POST', input),
  contracts: () => request<Contract[]>('/contracts'),
  addContract: (input: unknown) => request<Contract>('/contracts', 'POST', input),
  charges: (month: string) => request<Charge[]>(`/charges?month=${encodeURIComponent(month)}`),
  generateCharges: (month: string) => request<{ created: number }>('/billing/generate', 'POST', { month }),
  pay: (id: string, amount: number, method: string) => request<{ id: string }>(`/charges/${id}/payments`, 'POST', { amount, method, idempotencyKey: crypto.randomUUID() }),
  expenses: (month: string) => request<Expense[]>(`/expenses?month=${encodeURIComponent(month)}`),
  addExpense: (input: unknown) => request<Expense>('/expenses', 'POST', input),
  absences: (date: string) => request<Absence[]>(`/absences?date=${encodeURIComponent(date)}`),
  addAbsence: (input: unknown) => request<Absence>('/absences', 'POST', input),
  acknowledge: (id: string) => request<void>(`/absences/${id}/acknowledge`, 'POST', {}),
  summary: (month: string) => request<Summary>(`/dashboard/summary?month=${encodeURIComponent(month)}`),
};
export const money = (v: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v || 0);
export const today = () => { const now = new Date(); return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`; };
export const thisMonth = () => today().slice(0, 7);
export const dateBr = (value: string) => value?.slice(0, 10).split('-').reverse().join('/') || '—';
