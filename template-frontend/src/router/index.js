import { createRouter, createWebHistory } from 'vue-router'
import { getSafeAuthRedirect } from '@/router/authRedirect'
import { useUserStore } from '@/store/user'

const routes = [
  {
    path: '/',
    redirect: '/building'
  },
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/LoginView.vue'),
    meta: { public: true, layout: 'auth' }
  },
  {
    path: '/building',
    name: 'Building',
    component: () => import('@/views/BuildingList.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/building'
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to) => {
  const userStore = useUserStore()

  if (to.meta.requiresAuth && !userStore.isLoggedIn) {
    return {
      name: 'Login',
      query: { redirect: to.fullPath }
    }
  }

  if (to.name === 'Login' && userStore.isLoggedIn) {
    return getSafeAuthRedirect(to.query.redirect)
  }

  return true
})

export default router
