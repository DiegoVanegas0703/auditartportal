import { useCallback, useEffect, useRef, useState } from 'react'

const BASE_STYLES = `
  html, body { margin: 0; padding: 0; }
  body {
    font-family: system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif;
    font-size: 14px;
    line-height: 1.55;
    color: #1f2a44;
    overflow-wrap: anywhere;
  }
  img { max-width: 100%; height: auto; }
  table { max-width: 100%; }
  pre { white-space: pre-wrap; }
  a { color: #2f6f9f; }
  blockquote { margin: 0 0 0 8px; padding-left: 8px; border-left: 2px solid #d6dbe4; color: #5b6478; }
`

function buildDocument(html: string) {
  return `<!doctype html><html><head><meta charset="utf-8"><base target="_blank"><style>${BASE_STYLES}</style></head><body>${html}</body></html>`
}

/** Renderiza el HTML del correo aislado (sin scripts) y ajusta el alto al contenido completo. */
export function EmailHtmlFrame({ html }: { html: string }) {
  const frameRef = useRef<HTMLIFrameElement>(null)
  const [height, setHeight] = useState(120)

  const measure = useCallback(() => {
    const doc = frameRef.current?.contentDocument
    if (!doc?.body) return
    const next = Math.max(doc.body.scrollHeight, doc.documentElement.scrollHeight)
    if (next > 0) setHeight(next + 4)
  }, [])

  useEffect(() => {
    const frame = frameRef.current
    if (!frame) return
    let observer: ResizeObserver | null = null

    const onLoad = () => {
      measure()
      const doc = frame.contentDocument
      if (!doc?.body) return
      observer = new ResizeObserver(measure)
      observer.observe(doc.body)
      doc.querySelectorAll('img').forEach((img) => img.addEventListener('load', measure))
    }

    frame.addEventListener('load', onLoad)
    return () => {
      frame.removeEventListener('load', onLoad)
      observer?.disconnect()
    }
  }, [html, measure])

  return (
    <iframe
      ref={frameRef}
      title="Contenido del correo"
      sandbox="allow-same-origin allow-popups allow-popups-to-escape-sandbox"
      srcDoc={buildDocument(html)}
      style={{ height }}
      className="block w-full border-0 bg-transparent"
    />
  )
}
