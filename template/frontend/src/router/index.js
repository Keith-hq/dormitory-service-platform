import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  {
    path: '/',
    redirect: '/building'
  },
  {
    path: '/building',
    name: 'Building',
    component: () => import('@/views/BuildingList.vue')
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router
