import { createRouter, createWebHistory } from 'vue-router'
import { getSafeAuthRedirect } from '@/router/authRedirect'
import { getRoleHome, isRoleAllowed } from '@/router/roleAccess'
import { useUserStore } from '@/store/user'

const routes = [
  {
    path: '/',
    redirect: '/login'
  },
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/LoginView.vue'),
    meta: { public: true, layout: 'auth' }
  },
  {
    path: '/student',
    name: 'StudentHome',
    component: () => import('@/views/student/StudentHomeView.vue'),
    meta: { requiresAuth: true, roles: ['student'] }
  },
  {
    path: '/student/services',
    redirect: '/student/finance'
  },
  {
    path: '/student/profile',
    name: 'StudentProfile',
    component: () => import('@/views/student/StudentProfileView.vue'),
    meta: { requiresAuth: true, roles: ['student'], title: '个人与住宿档案' }
  },
  {
    path: '/student/finance',
    name: 'StudentFinance',
    component: () => import('@/views/student/StudentFinanceView.vue'),
    meta: { requiresAuth: true, roles: ['student'] }
  },
  {
    path: '/student/repair',
    name: 'StudentRepair',
    component: () => import('@/views/student/StudentRepairView.vue'),
    meta: { requiresAuth: true, roles: ['student'], title: '报修流程' }
  },
  {
    path: '/student/facilities',
    name: 'StudentFacilities',
    component: () => import('@/views/student/StudentFacilitiesView.vue'),
    meta: { requiresAuth: true, roles: ['student'], title: '设施与共享物品' }
  },
  {
    path: '/student/community',
    name: 'StudentCommunity',
    component: () => import('@/views/student/StudentCommunityView.vue'),
    meta: { requiresAuth: true, roles: ['student'], title: '安全与社区' }
  },
  {
    path: '/student/report',
    name: 'StudentReport',
    component: () => import('@/views/student/StudentReportView.vue'),
    meta: { requiresAuth: true, roles: ['student'], title: '月度生活统计' }
  },
  {
    path: '/admin',
    name: 'AdminHome',
    component: () => import('@/views/admin/AdminHomeView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'super_admin'] }
  },
  {
    path: '/admin/operations',
    redirect: '/admin/duty'
  },
  {
    path: '/admin/accommodation',
    name: 'AdminAccommodation',
    component: () => import('@/views/admin/AdminAccommodationView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'super_admin'], title: '住宿管理' }
  },
  {
    path: '/admin/billing',
    name: 'AdminBilling',
    component: () => import('@/views/admin/AdminBillingView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'super_admin'], title: '水电账单' }
  },
  {
    path: '/admin/safety',
    name: 'AdminSafety',
    component: () => import('@/views/admin/AdminSafetyView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'super_admin'], title: '卫生与晚归' }
  },
  {
    path: '/admin/duty',
    name: 'AdminDuty',
    component: () => import('@/views/admin/AdminDutyView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'super_admin'], title: '访客值守' }
  },
  {
    path: '/admin/repair',
    name: 'AdminRepair',
    component: () => import('@/views/admin/AdminRepairView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'super_admin'] }
  },
  {
    path: '/repairman',
    name: 'RepairmanHome',
    component: () => import('@/views/admin/AdminRepairView.vue'),
    meta: { requiresAuth: true, roles: ['repairman'], title: '我的维修工单' }
  },
  {
    path: '/counselor',
    name: 'CounselorHome',
    component: () => import('@/views/counselor/CounselorApprovalView.vue'),
    meta: { requiresAuth: true, roles: ['counselor'] }
  },
  {
    path: '/super-admin',
    name: 'SuperAdminHome',
    component: () => import('@/views/superadmin/SuperAdminView.vue'),
    meta: { requiresAuth: true, roles: ['super_admin'], section: 'overview' }
  },
  {
    path: '/super-admin/people',
    name: 'SuperAdminPeople',
    component: () => import('@/views/superadmin/SuperAdminView.vue'),
    meta: { requiresAuth: true, roles: ['super_admin'], section: 'people' }
  },
  {
    path: '/super-admin/audit',
    name: 'SuperAdminAudit',
    component: () => import('@/views/superadmin/SuperAdminView.vue'),
    meta: { requiresAuth: true, roles: ['super_admin'], section: 'audit' }
  },
  {
    path: '/building',
    name: 'Building',
    component: () => import('@/views/BuildingList.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'super_admin'] }
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/'
  }
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes
})

router.beforeEach((to) => {
  const userStore = useUserStore()
  const roleHome = getRoleHome(userStore.userInfo?.role)

  if (to.meta.requiresAuth && !userStore.isLoggedIn) {
    return {
      name: 'Login',
      query: { redirect: to.fullPath }
    }
  }

  if (to.meta.requiresAuth && !isRoleAllowed(userStore.userInfo?.role, to.meta.roles)) {
    return roleHome
  }

  if (to.name === 'Login' && userStore.isLoggedIn) {
    return getSafeAuthRedirect(to.query.redirect, roleHome)
  }

  return true
})

export default router
