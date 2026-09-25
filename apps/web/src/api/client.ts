const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5070'

const ACCESS_KEY = 'auditart_access'
const REFRESH_KEY = 'auditart_refresh'

export function getAccessToken() {
  return localStorage.getItem(ACCESS_KEY)
}

export function getRefreshToken() {
  return localStorage.getItem(REFRESH_KEY)
}

export function setTokens(access: string, refresh: string) {
  localStorage.setItem(ACCESS_KEY, access)
  localStorage.setItem(REFRESH_KEY, refresh)
}

export function clearTokens() {
  localStorage.removeItem(ACCESS_KEY)
  localStorage.removeItem(REFRESH_KEY)
}

type ApiError = { error?: string; title?: string; detail?: string }

async function parseError(res: Response): Promise<string> {
  try {
    const data = (await res.json()) as ApiError
    return data.error ?? data.detail ?? data.title ?? res.statusText
  } catch {
    return res.statusText || 'Error de API'
  }
}

let refreshPromise: Promise<boolean> | null = null

async function tryRefresh(): Promise<boolean> {
  const refresh = getRefreshToken()
  if (!refresh) return false

  const res = await fetch(`${API_BASE}/api/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: refresh }),
  })

  if (!res.ok) {
    clearTokens()
    return false
  }

  const data = await res.json()
  setTokens(data.accessToken, data.refreshToken)
  return true
}

// Endpoints de autenticación: un 401 acá es un error real de credenciales,
// no una sesión vencida, así que no debe dispararse el flujo de refresh.
const AUTH_PATHS = ['/api/auth/login', '/api/auth/refresh']

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {},
  retry = true,
): Promise<T> {
  const headers = new Headers(options.headers)
  const isFormData = typeof FormData !== 'undefined' && options.body instanceof FormData
  if (!headers.has('Content-Type') && options.body && !isFormData) {
    headers.set('Content-Type', 'application/json')
  }

  const isAuthPath = AUTH_PATHS.some((p) => path.startsWith(p))

  const token = getAccessToken()
  if (token && !isAuthPath) headers.set('Authorization', `Bearer ${token}`)

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers })

  if (res.status === 401 && retry && !isAuthPath) {
    refreshPromise ??= tryRefresh().finally(() => {
      refreshPromise = null
    })
    const ok = await refreshPromise
    if (ok) return apiFetch<T>(path, options, false)
    throw new Error('Sesión expirada')
  }

  if (!res.ok) {
    throw new Error(await parseError(res))
  }

  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

export async function apiDownload(path: string, retry = true): Promise<Blob> {
  const headers = new Headers()
  const token = getAccessToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const res = await fetch(`${API_BASE}${path}`, { headers })

  if (res.status === 401 && retry) {
    refreshPromise ??= tryRefresh().finally(() => {
      refreshPromise = null
    })
    const ok = await refreshPromise
    if (ok) return apiDownload(path, false)
    throw new Error('Sesión expirada')
  }

  if (!res.ok) {
    throw new Error(await parseError(res))
  }

  return res.blob()
}

export { API_BASE }
