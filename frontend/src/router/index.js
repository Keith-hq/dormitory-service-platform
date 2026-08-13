import { createRouter, createWebHistory } from 'vue-router'
import { getSafeAuthRedirect } from '@/router/authRedirect'
import { getRoleHome, isRoleAllowed } from '@/router/roleAccess'
import { useUserStore } from '@/store/user'

const routes = [
  {
    path: '/',
    name: 'Workspace',
    component: () => import('@/views/RoleDashboard.vue'),
    meta: { requiresAuth: true }
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
    component: () => import('@/views/RoleDashboard.vue'),
    meta: { requiresAuth: true, roles: ['student'] }
  },
  {
    path: '/student/services',
    name: 'StudentServices',
    component: () => import('@/views/StudentServicesView.vue'),
    meta: { requiresAuth: true, roles: ['student'] }
  },
  {
    path: '/admin',
    name: 'AdminHome',
    component: () => import('@/views/RoleDashboard.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'super_admin'] }
  },
  {
    path: '/admin/operations',
    name: 'AdminOperations',
    component: () => import('@/views/AdminOperationsView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'super_admin'] }
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

  if (to.name === 'Workspace' && userStore.isLoggedIn) {
    return roleHome
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
