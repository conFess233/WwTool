// 后端请求封装：改动数据的请求都带 X-WwTool 头（后端的 CSRF 防护，见 src/Web/Program.cs）。
export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function parseError(res: Response): Promise<ApiError> {
  let detail = '';
  try {
    const body = await res.json();
    detail = body?.detail ?? body?.title ?? '';
  } catch {
    // 非 JSON 错误体
  }
  if (!detail) {
    detail = res.status === 401 ? '请先登录。' : res.status === 429 ? '操作太频繁，请稍后再试。' : `请求失败（HTTP ${res.status}）`;
  }
  return new ApiError(res.status, detail);
}

export async function request<T>(method: string, path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (method !== 'GET') headers['X-WwTool'] = '1';
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const res = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: 'same-origin',
    signal,
  });
  if (!res.ok) throw await parseError(res);
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const get = <T>(path: string, signal?: AbortSignal) => request<T>('GET', path, undefined, signal);
export const post = <T>(path: string, body?: unknown, signal?: AbortSignal) => request<T>('POST', path, body ?? {}, signal);
export const patch = <T>(path: string, body?: unknown) => request<T>('PATCH', path, body ?? {});
export const del = <T>(path: string) => request<T>('DELETE', path);
