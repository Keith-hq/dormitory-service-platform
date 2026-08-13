import test from 'node:test'
import assert from 'node:assert/strict'
import { getSafeAuthRedirect } from '../src/router/authRedirect.js'
import { getRoleHome, isRoleAllowed } from '../src/router/roleAccess.js'
import { normalizeCollection } from '../src/utils/collection.js'

test('maps supported roles to their own workspace', () => {
  assert.equal(getRoleHome('student'), '/student')
  assert.equal(getRoleHome('ADMIN'), '/admin')
  assert.equal(getRoleHome('super_admin'), '/admin')
  assert.equal(getRoleHome('unknown'), '/login')
  assert.equal(getRoleHome('__proto__'), '/login')
})

test('checks route role allowlists', () => {
  assert.equal(isRoleAllowed('student', ['student']), true)
  assert.equal(isRoleAllowed('student', ['admin', 'super_admin']), false)
  assert.equal(isRoleAllowed('admin'), true)
})

test('keeps redirects on registered internal role routes', () => {
  assert.equal(
    getSafeAuthRedirect('/student/services?tab=packages'),
    '/student/services?tab=packages'
  )
  assert.equal(getSafeAuthRedirect('//example.com', '/admin'), '/admin')
  assert.equal(getSafeAuthRedirect('/unknown', '/admin'), '/admin')
})

test('normalizes common collection response shapes', () => {
  assert.deepEqual(normalizeCollection([{ id: 1 }]), { items: [{ id: 1 }], total: 1 })
  assert.deepEqual(normalizeCollection({ records: [{ id: 2 }], totalCount: 4 }), {
    items: [{ id: 2 }],
    total: 4
  })
  assert.deepEqual(normalizeCollection(null), { items: [], total: 0 })
})
