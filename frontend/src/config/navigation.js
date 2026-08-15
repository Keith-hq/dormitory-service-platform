export const APP_NAVIGATION = Object.freeze({
  student: [
    { index: '01', to: '/student', label: '生活首页', eyebrow: 'HOME' },
    { index: '02', to: '/student/services', label: '服务记录', eyebrow: 'SERVICES' }
  ],
  admin: [
    { index: '01', to: '/admin', label: '运营首页', eyebrow: 'OVERVIEW' },
    { index: '02', to: '/admin/operations', label: '值班工作台', eyebrow: 'OPERATIONS' },
    { index: '03', to: '/building', label: '楼栋档案', eyebrow: 'BUILDINGS' }
  ],
  super_admin: [
    { index: '01', to: '/admin', label: '全局概览', eyebrow: 'OVERVIEW' },
    { index: '02', to: '/admin/operations', label: '运营审阅', eyebrow: 'OPERATIONS' },
    { index: '03', to: '/building', label: '空间档案', eyebrow: 'BUILDINGS' }
  ]
})

export const getNavigation = (role) => APP_NAVIGATION[role] || []
