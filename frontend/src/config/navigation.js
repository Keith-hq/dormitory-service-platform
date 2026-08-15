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
  super_admin: [
    { index: '01', to: '/admin', label: '全局概览', eyebrow: 'OVERVIEW' },
    { index: '02', to: '/admin/duty', label: '运营审阅', eyebrow: 'OPERATIONS' },
    { index: '03', to: '/building', label: '空间档案', eyebrow: 'BUILDINGS' }
  ]
})

export const getNavigation = (role) => APP_NAVIGATION[role] || []
