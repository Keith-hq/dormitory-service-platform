export const APP_NAVIGATION = Object.freeze({
  student: [
    { index: '01', to: '/student', label: '生活首页', eyebrow: 'HOME' },
    { index: '02', to: '/student/profile', label: '我的档案', eyebrow: 'DOSSIER' },
    { index: '03', to: '/student/finance', label: '费用钱包', eyebrow: 'FINANCE' },
    { index: '04', to: '/student/repair', label: '报修流程', eyebrow: 'REPAIR' },
    { index: '05', to: '/student/facilities', label: '设施共享', eyebrow: 'FACILITIES' },
    { index: '06', to: '/student/community', label: '安全社区', eyebrow: 'COMMUNITY' },
    { index: '07', to: '/student/report', label: '生活报告', eyebrow: 'REPORT' }
  ],
  admin: [
    { index: '01', to: '/admin', label: '运营首页', eyebrow: 'OVERVIEW' },
    { index: '02', to: '/building', label: '空间档案', eyebrow: 'SPACES' },
    { index: '03', to: '/admin/duty', label: '安全值班', eyebrow: 'DUTY' },
    { index: '04', to: '/admin/repair', label: '维修调度', eyebrow: 'REPAIR' }
  ],
  repairman: [{ index: '01', to: '/repairman', label: '我的工单', eyebrow: 'WORK ORDERS' }],
  counselor: [{ index: '01', to: '/counselor', label: '离校审批', eyebrow: 'APPROVALS' }],
  super_admin: [
    { index: '01', to: '/super-admin', label: '治理总览', eyebrow: 'GOVERNANCE' },
    { index: '02', to: '/super-admin/people', label: '人员档案', eyebrow: 'PEOPLE' },
    { index: '03', to: '/super-admin/audit', label: '审计报表', eyebrow: 'AUDIT' },
    { index: '04', to: '/building', label: '空间档案', eyebrow: 'SPACES' }
  ]
})

export const getNavigation = (role) => APP_NAVIGATION[role] || []
