export const genRequestId = () => {
  try {
    const buf = new Uint32Array(2)
    crypto.getRandomValues(buf)
    return `req-${Date.now().toString(36)}-${buf[0].toString(36)}${buf[1].toString(36)}`
  } catch {
    // crypto unavailable (non-secure context) — fall back to Math.random
    return `req-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
  }
}
