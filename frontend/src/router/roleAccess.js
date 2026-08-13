export const ROLE_HOME = Object.freeze({
  student: '/student',
  admin: '/admin',
  super_admin: '/admin'
})

export const ROLE_LABEL = Object.freeze({
  student: '学生',
  admin: '宿管',
  super_admin: '超级管理员'
})

export const normalizeRole = (role) => (typeof role === 'string' ? role.trim().toLowerCase() : '')

export const getRoleHome = (role) => {
  const normalizedRole = normalizeRole(role)
  return Object.hasOwn(ROLE_HOME, normalizedRole) ? ROLE_HOME[normalizedRole] : '/login'
}

export const isRoleAllowed = (role, allowedRoles = []) => {
  if (!Array.isArray(allowedRoles) || allowedRoles.length === 0) return true
  return allowedRoles.includes(normalizeRole(role))
}
