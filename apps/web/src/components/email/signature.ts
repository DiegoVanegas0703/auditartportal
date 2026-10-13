export function htmlToPlainText(html: string): string {
  const doc = new DOMParser().parseFromString(html, 'text/html')
  return (doc.body.textContent ?? '').replace(/\u00a0/g, ' ').trim()
}

/** Cuerpo inicial del editor: dos renglones libres para escribir y la firma debajo. */
export function initialBodyWithSignature(signatureHtml: string | undefined): { html: string; text: string } {
  if (!signatureHtml?.trim()) return { html: '', text: '' }
  const html = `<p></p><p></p>${signatureHtml}`
  return { html, text: htmlToPlainText(html) }
}

export function isBodyEmpty(text: string, signatureHtml: string | undefined): boolean {
  const normalize = (value: string) => value.replace(/\s+/g, '')
  const body = normalize(text)
  if (!body) return true
  return !!signatureHtml?.trim() && body === normalize(htmlToPlainText(signatureHtml))
}
