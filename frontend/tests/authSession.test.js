import test from 'node:test'
import assert from 'node:assert/strict'
import { normalizeAuthSession } from '../src/utils/authSession.js'

test('keeps the mock login session contract unchanged', () => {
  const session = normalizeAuthSession({
    token: 'mock-token',
    userInfo: { id: 'S001', name: '测试学生', role: 'student', roomName: '503' }
  })

  assert.deepEqual(session, {
    token: 'mock-token',
    userInfo: {
      id: 'S001',
      name: '测试学生',
      role: 'student',
      roomName: '503',
      needChangePassword: false
    }
  })
})

test('combines real login and auth me responses into the frontend session', () => {
  const session = normalizeAuthSession(
    {
      token: 'real-token',
      role: 'student',
      accountId: 12,
      loginName: 'student001'
    },
    {
      accountId: 12,
      loginName: 'student001',
      role: 'student',
      studentId: 'S2026001',
      adminId: null,
      needChangePassword: true
    }
  )

  assert.equal(session.token, 'real-token')
  assert.equal(session.userInfo.id, 'S2026001')
  assert.equal(session.userInfo.name, 'student001')
  assert.equal(session.userInfo.role, 'student')
  assert.equal(session.userInfo.needChangePassword, true)
})
