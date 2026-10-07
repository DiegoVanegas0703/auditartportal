import { ChevronDown } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'

const COLLAPSED_MAX_HEIGHT = 260

/** Muestra el cuerpo extenso en un recuadro con scroll propio y permite expandirlo completo. */
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
        className={
          collapsed
            ? 'overflow-y-auto overscroll-contain rounded-xl border border-gray-200 bg-white p-3 [scrollbar-width:thin]'
            : undefined
        }
        style={collapsed ? { maxHeight: COLLAPSED_MAX_HEIGHT } : undefined}
      >
        <div ref={contentRef}>{children}</div>
      </div>
      {overflowing && (
        <button
          type="button"
          onClick={() => setShowAll((v) => !v)}
          className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-auditart-blue hover:underline"
        >
          {showAll ? 'Ver menos' : 'Ver correo completo'}
          <ChevronDown size={14} className={`transition-transform ${showAll ? 'rotate-180' : ''}`} />
        </button>
      )}
    </div>
  )
}
