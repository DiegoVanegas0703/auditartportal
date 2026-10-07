import { ChevronDown } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'

const COLLAPSED_MAX_HEIGHT = 320

/** Limita el alto del cuerpo del correo y ofrece "Ver más" cuando el contenido es extenso. */
export function EmailCollapsibleBody({ children }: { children: ReactNode }) {
  const contentRef = useRef<HTMLDivElement>(null)
  const [overflowing, setOverflowing] = useState(false)
  const [showAll, setShowAll] = useState(false)

  useEffect(() => {
    const el = contentRef.current
    if (!el) return
    const check = () => setOverflowing(el.scrollHeight > COLLAPSED_MAX_HEIGHT + 40)
    check()
    const observer = new ResizeObserver(check)
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  const collapsed = overflowing && !showAll

  return (
    <div>
      <div
        className="relative overflow-hidden"
        style={collapsed ? { maxHeight: COLLAPSED_MAX_HEIGHT } : undefined}
      >
        <div ref={contentRef}>{children}</div>
        {collapsed && (
          <div className="pointer-events-none absolute inset-x-0 bottom-0 h-16 bg-gradient-to-t from-white to-transparent" />
        )}
      </div>
      {overflowing && (
        <button
          type="button"
          onClick={() => setShowAll((v) => !v)}
          className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-auditart-blue hover:underline"
        >
          {showAll ? 'Ver menos' : 'Ver más'}
          <ChevronDown size={14} className={`transition-transform ${showAll ? 'rotate-180' : ''}`} />
        </button>
      )}
    </div>
  )
}
