import test from 'node:test'
import assert from 'node:assert/strict'
import { resolveRoleServiceMockAdapter } from '../src/mock/roleServices.js'
import {
  formatPackageArrivalTime,
  isPackageReady,
  normalizePackagePayload,
  PACKAGE_STATUS
} from '../src/utils/package.js'

const studentRequest = (url, method = 'get') => ({
  url,
  method,
  headers: { Authorization: 'Bearer mock-token-student-contract-test' }
})

test('package mock follows the published Apifox Package schema', async () => {
  const config = studentRequest('/api/students/S2026001/packages')
  const adapter = resolveRoleServiceMockAdapter(config)
  const response = await adapter(config)

  assert.equal(response.data.code, 200)
  assert.ok(response.data.data.items.length > 0)
  for (const item of response.data.data.items) {
    assert.deepEqual(Object.keys(item).sort(), [
      'arrivalTime',
      'carrier',
      'expressNo',
      'packageId',
      'pickupCode',
      'status',
      'studentId'
    ])
    assert.ok([PACKAGE_STATUS.READY, PACKAGE_STATUS.PICKED_UP].includes(item.status))
  }
})

test('pickup moves a package through the published status enum', async () => {
  const config = studentRequest('/api/packages/7102/pickup', 'post')
  const response = await resolveRoleServiceMockAdapter(config)(config)

  assert.equal(response.data.code, 200)
  assert.equal(response.data.data, null)

  const listConfig = studentRequest('/api/students/S2026001/packages')
  const listResponse = await resolveRoleServiceMockAdapter(listConfig)(listConfig)
  const pickedUpPackage = listResponse.data.data.items.find((item) => item.packageId === 7102)

  assert.equal(pickedUpPackage.status, PACKAGE_STATUS.PICKED_UP)
  assert.equal(isPackageReady(pickedUpPackage.status), false)
})

test('formats package arrival timestamps without changing invalid values', () => {
  assert.notEqual(formatPackageArrivalTime('2026-08-13T10:36:00+08:00'), '—')
  assert.equal(formatPackageArrivalTime('pending sync'), 'pending sync')
  assert.equal(formatPackageArrivalTime(''), '—')
})

test('normalizes the legacy parcel response at the API boundary', () => {
  const payload = normalizePackagePayload({
    items: [
      {
        parcelId: 18,
        studentId: 'S2026001',
        arriveTime: '2026-08-15T10:00:00+08:00',
        pickupTime: null,
        courierCompany: '联调快递'
      }
    ],
    total: 1
  })

  assert.deepEqual(payload.items[0], {
    parcelId: 18,
    studentId: 'S2026001',
    arriveTime: '2026-08-15T10:00:00+08:00',
    pickupTime: null,
    courierCompany: '联调快递',
    packageId: 18,
    expressNo: '18',
    carrier: '联调快递',
    pickupCode: '18',
    arrivalTime: '2026-08-15T10:00:00+08:00',
    status: PACKAGE_STATUS.READY
  })
})
