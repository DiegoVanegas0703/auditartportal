import { useEffect, useRef } from 'react'

const ACTIVITY_EVENTS = ['pointerdown', 'keydown', 'touchstart', 'scroll'] as const
const STORAGE_KEY = 'auditart_last_activity'

/** 25 minutos sin interacción → se considera sesión idle. */
export const IDLE_TIMEOUT_MS = 25 * 60 * 1000

function now() {
  return Date.now()
}

function markActivity() {
  try {
    localStorage.setItem(STORAGE_KEY, String(now()))
  } catch {
    /* private mode */
  }
}

function lastActivity(): number {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    const parsed = raw ? Number(raw) : 0
    return Number.isFinite(parsed) ? parsed : 0
  } catch {
    return now()
  }
}

export function useIdleSession(enabled: boolean, onIdle: () => void) {
  const onIdleRef = useRef(onIdle)
  onIdleRef.current = onIdle

  useEffect(() => {
    if (!enabled) return

    markActivity()
    let timer: ReturnType<typeof setTimeout>

    const arm = () => {
      clearTimeout(timer)
      const remaining = IDLE_TIMEOUT_MS - (now() - lastActivity())
      if (remaining <= 0) {
        onIdleRef.current()
        return
      }
      timer = setTimeout(() => {
        if (now() - lastActivity() >= IDLE_TIMEOUT_MS) {
          onIdleRef.current()
        } else {
          arm()
        }
      }, remaining)
    }

    const onActivity = () => {
      markActivity()
      arm()
    }

    const onVisibility = () => {
      if (document.visibilityState === 'visible') {
        if (now() - lastActivity() >= IDLE_TIMEOUT_MS) onIdleRef.current()
        else arm()
      }
    }

    ACTIVITY_EVENTS.forEach((event) => window.addEventListener(event, onActivity, { passive: true }))
    document.addEventListener('visibilitychange', onVisibility)
    arm()

    return () => {
      clearTimeout(timer)
      ACTIVITY_EVENTS.forEach((event) => window.removeEventListener(event, onActivity))
      document.removeEventListener('visibilitychange', onVisibility)
    }
  }, [enabled])
}
