const DEFAULT_AUTH_REDIRECT = '/student'
const INTERNAL_ORIGIN = 'https://dormitory.local'

// 维护要求：新增 meta.requiresAuth 业务路由时，必须同步登记其静态 path。
const ALLOWED_REDIRECT_PATHS = new Set([
  '/student',
  '/student/services',
  '/student/profile',
  '/student/finance',
  '/student/repair',
  '/student/facilities',
  '/student/community',
  '/student/report',
  '/admin',
  '/admin/operations',
  '/admin/duty',
  '/admin/repair',
  '/repairman',
  '/counselor',
  '/super-admin',
  '/super-admin/people',
  '/super-admin/audit',
  '/building'
])

export const getSafeAuthRedirect = (value, fallback = DEFAULT_AUTH_REDIRECT) => {
  const safeFallback = ALLOWED_REDIRECT_PATHS.has(fallback) ? fallback : DEFAULT_AUTH_REDIRECT

  if (
    typeof value !== 'string' ||
    !value.startsWith('/') ||
    value.startsWith('//') ||
    /\\|%5c/i.test(value)
  ) {
    return safeFallback
  }

  try {
    const target = new URL(value, INTERNAL_ORIGIN)
    if (target.origin !== INTERNAL_ORIGIN || !ALLOWED_REDIRECT_PATHS.has(target.pathname)) {
      return safeFallback
    }

    return `${target.pathname}${target.search}${target.hash}`
  } catch {
    return safeFallback
  }
}
