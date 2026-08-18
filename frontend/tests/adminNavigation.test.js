import test from 'node:test'
import assert from 'node:assert/strict'
import { APP_NAVIGATION } from '../src/config/navigation.js'

test('exposes the three admin business workspaces in the sidebar', () => {
  const routes = APP_NAVIGATION.admin.map((item) => item.to)

  assert.ok(routes.includes('/admin/accommodation'))
  assert.ok(routes.includes('/admin/assets'))
  assert.ok(routes.includes('/admin/billing'))
  assert.ok(routes.includes('/admin/safety'))
})

test('keeps deferred water delivery and violation businesses out of navigation', () => {
  const labels = APP_NAVIGATION.admin.map((item) => item.label).join(' ')

  assert.equal(labels.includes('订水'), false)
  assert.equal(labels.includes('违规'), false)
})
