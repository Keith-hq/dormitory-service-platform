import test from 'node:test'
import assert from 'node:assert/strict'
import {
  getDaysRepresented,
  getMetricProgress,
  normalizeAnnualReport
} from '../src/utils/annualReport.js'

test('normalizes the REP-03 response without inventing extra fields', () => {
  assert.deepEqual(
    normalizeAnnualReport(
      {
        year: 2025,
        utilityTotal: '756.8',
        hygieneAvg: 94.2,
        accessCount: 1015.6,
        overview: '  年度概述  '
      },
      2026
    ),
    {
      year: 2025,
      utilityTotal: 756.8,
      hygieneAvg: 94.2,
      accessCount: 1016,
      overview: '年度概述'
    }
  )
})

test('clamps visual progress to the reference ring', () => {
  assert.equal(getMetricProgress(50, 100), 50)
  assert.equal(getMetricProgress(150, 100), 100)
  assert.equal(getMetricProgress(-1, 100), 0)
  assert.equal(getMetricProgress(50, 0), 0)
})

test('uses elapsed days for the current year and full days for past years', () => {
  const now = new Date('2026-08-14T12:00:00+08:00')
  assert.equal(getDaysRepresented(2026, now), 226)
  assert.equal(getDaysRepresented(2024, now), 366)
  assert.equal(getDaysRepresented(2025, now), 365)
})
