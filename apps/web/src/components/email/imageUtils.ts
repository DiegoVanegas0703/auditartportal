const MAX_SOURCE_BYTES = 8 * 1024 * 1024

/**
 * Lee una imagen y la devuelve como data URI reducida (máx. `maxWidth` px de ancho).
 * Mantiene PNG si la original puede tener transparencia (png/gif/webp); si no, JPEG.
 */
export async function fileToResizedDataUrl(
  file: File,
  maxWidth = 600,
): Promise<{ src: string; width: number; height: number }> {
  if (!file.type.startsWith('image/')) throw new Error('El archivo no es una imagen.')
  if (file.size > MAX_SOURCE_BYTES) throw new Error('La imagen supera los 8 MB.')
  if (file.type === 'image/svg+xml')
    throw new Error('Los SVG no se ven en muchos clientes de correo. Usá PNG o JPG.')

  const sourceUrl = await readAsDataUrl(file)
  const img = await loadImage(sourceUrl)
  const scale = Math.min(1, maxWidth / img.naturalWidth)
  const width = Math.max(1, Math.round(img.naturalWidth * scale))
  const height = Math.max(1, Math.round(img.naturalHeight * scale))

  if (scale === 1 && file.size <= 200 * 1024) return { src: sourceUrl, width, height }

  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const ctx = canvas.getContext('2d')
  if (!ctx) return { src: sourceUrl, width, height }
  const keepAlpha = /png|gif|webp/i.test(file.type)
  if (!keepAlpha) {
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, width, height)
  }
  ctx.drawImage(img, 0, 0, width, height)
  const src = keepAlpha ? canvas.toDataURL('image/png') : canvas.toDataURL('image/jpeg', 0.88)
  return { src, width, height }
}

function readAsDataUrl(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(String(reader.result))
    reader.onerror = () => reject(new Error('No se pudo leer la imagen.'))
    reader.readAsDataURL(file)
  })
}

function loadImage(src: string) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const img = new Image()
    img.onload = () => resolve(img)
    img.onerror = () => reject(new Error('No se pudo procesar la imagen.'))
    img.src = src
  })
}
