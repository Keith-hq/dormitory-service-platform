const pickText = (...values) =>
  values.find((value) => typeof value === 'string' && value.trim().length > 0)?.trim() || ''

export const normalizeAuthSession = (loginResult, currentUser = null) => {
  const token = pickText(loginResult?.token)
  const source = loginResult?.userInfo || currentUser || {}
  const accountId = source.accountId ?? loginResult?.accountId
  const id = pickText(
    source.id,
    source.studentId,
    source.adminId,
    accountId === null || accountId === undefined ? '' : String(accountId)
  )
  const name = pickText(source.name, source.loginName, loginResult?.loginName)
  const role = pickText(source.role, loginResult?.role).toLowerCase()

  return {
    token,
    userInfo: {
      ...source,
      id,
      name,
      role
    }
  }
}
