import { Check, ChevronDown, Search, X } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { artsApi, type ArtDto } from '../../api/auditartApi'

let cache: Promise<ArtDto[]> | null = null

export function loadArts(force = false): Promise<ArtDto[]> {
  if (!cache || force) {
    cache = artsApi.list().catch((e) => {
      cache = null
      throw e
    })
  }
  return cache
}

function normalize(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
}

/** Desplegable de ARTs del catálogo con búsqueda por nombre o CUIT. El valor es el nombre de la ART. */
export function ArtSelect({
  value,
  onChange,
  placeholder = 'Seleccioná la ART',
  className = '',
  required,
}: {
  value: string
  onChange: (nombre: string) => void
  placeholder?: string
  className?: string
  required?: boolean
}) {
  const [arts, setArts] = useState<ArtDto[]>([])
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [highlight, setHighlight] = useState(0)
  const rootRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    void loadArts().then(setArts).catch(() => setArts([]))
  }, [])

  useEffect(() => {
    if (!open) return
    const onClick = (e: MouseEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [open])

  const filtered = useMemo(() => {
    const q = normalize(query.trim())
    const digits = query.replace(/\D/g, '')
    if (!q) return arts
    return arts.filter(
      (a) =>
        normalize(a.nombre).includes(q) ||
        (digits.length > 0 && a.cuit.replace(/\D/g, '').includes(digits)),
    )
  }, [arts, query])

  const selected = arts.find((a) => normalize(a.nombre) === normalize(value))

  const openList = () => {
    setQuery('')
    setHighlight(0)
    setOpen(true)
    setTimeout(() => inputRef.current?.focus(), 0)
  }

  const pick = (art: ArtDto) => {
    onChange(art.nombre)
    setOpen(false)
  }

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setHighlight((h) => Math.min(h + 1, filtered.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setHighlight((h) => Math.max(h - 1, 0))
    } else if (e.key === 'Enter') {
      e.preventDefault()
      if (filtered[highlight]) pick(filtered[highlight])
    } else if (e.key === 'Escape') {
      setOpen(false)
    }
  }

  return (
    <div ref={rootRef} className={`relative ${className}`}>
      <button
        type="button"
        onClick={() => (open ? setOpen(false) : openList())}
        className="input-modern flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm"
        aria-haspopup="listbox"
        aria-expanded={open}
      >
        <span className={`min-w-0 truncate ${value ? 'text-auditart-navy' : 'text-auditart-muted'}`}>
          {value ? (
            <>
              {selected?.nombre ?? value}
              {selected && <span className="ml-2 text-xs text-auditart-muted">{selected.cuit}</span>}
              {!selected && <span className="ml-2 text-xs text-amber-600">(fuera del catálogo)</span>}
            </>
          ) : (
            placeholder
          )}
        </span>
        <span className="flex shrink-0 items-center gap-1 text-auditart-muted">
          {value && !required && (
            <X
              size={14}
              className="hover:text-red-600"
              onClick={(e) => {
                e.stopPropagation()
                onChange('')
              }}
            />
          )}
          <ChevronDown size={16} />
        </span>
      </button>
      {/* Mantiene la validación nativa del formulario cuando el campo es obligatorio. */}
      {required && (
        <input
          tabIndex={-1}
          aria-hidden
          required
          value={value}
          onChange={() => undefined}
          className="pointer-events-none absolute inset-x-0 bottom-0 h-0 opacity-0"
        />
      )}

      {open && (
        <div className="absolute z-50 mt-1 w-full overflow-hidden rounded-xl border border-gray-200 bg-white shadow-xl">
          <div className="flex items-center gap-2 border-b border-gray-100 px-3 py-2">
            <Search size={14} className="text-auditart-muted" />
            <input
              ref={inputRef}
              value={query}
              onChange={(e) => {
                setQuery(e.target.value)
                setHighlight(0)
              }}
              onKeyDown={onKeyDown}
              placeholder="Buscar por nombre o CUIT…"
              className="w-full bg-transparent text-sm outline-none"
            />
          </div>
          <ul role="listbox" className="max-h-60 overflow-y-auto py-1">
            {filtered.length === 0 && (
              <li className="px-3 py-2 text-xs text-auditart-muted">
                {arts.length === 0 ? 'No hay ARTs cargadas (módulo ARTs).' : 'Sin resultados.'}
              </li>
            )}
            {filtered.map((art, index) => {
              const isSelected = selected?.id === art.id
              return (
                <li
                  key={art.id}
                  role="option"
                  aria-selected={isSelected}
                  onMouseEnter={() => setHighlight(index)}
                  onMouseDown={(e) => {
                    e.preventDefault()
                    pick(art)
                  }}
                  className={`flex cursor-pointer items-center justify-between gap-2 px-3 py-2 text-sm ${
                    index === highlight ? 'bg-auditart-blue/8' : ''
                  }`}
                >
                  <span className="min-w-0 truncate font-medium text-auditart-navy">{art.nombre}</span>
                  <span className="flex shrink-0 items-center gap-2 text-xs text-auditart-muted">
                    {art.cuit}
                    {isSelected && <Check size={14} className="text-auditart-blue" />}
                  </span>
                </li>
              )
            })}
          </ul>
        </div>
      )}
    </div>
  )
}
