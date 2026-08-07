const DEFAULT_AUTH_REDIRECT = '/building'
const INTERNAL_ORIGIN = 'https://dormitory.local'
const ALLOWED_REDIRECT_PATHS = new Set(['/building'])

export const getSafeAuthRedirect = (value) => {
  if (
    typeof value !== 'string' ||
    !value.startsWith('/') ||
    value.startsWith('//') ||
    /\\|%5c/i.test(value)
  ) {
    return DEFAULT_AUTH_REDIRECT
  }

  try {
    const target = new URL(value, INTERNAL_ORIGIN)
    if (target.origin !== INTERNAL_ORIGIN || !ALLOWED_REDIRECT_PATHS.has(target.pathname)) {
      return DEFAULT_AUTH_REDIRECT
    }

    return `${target.pathname}${target.search}${target.hash}`
  } catch {
    return DEFAULT_AUTH_REDIRECT
  }
}
