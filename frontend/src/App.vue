<script setup>
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { clearSession } from '@/store/session'
import { useUserStore } from '@/store/user'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

const showAppShell = computed(() => route.meta.layout !== 'auth')

const logout = async () => {
  await clearSession()
  await router.replace({ name: 'Login' })
}
</script>

<template>
  <router-view v-if="!showAppShell" />
  <div v-else id="app-container">
    <header class="app-header">
      <h1>🏠 宿舍服务管理平台</h1>
      <nav>
        <router-link to="/building">楼栋管理</router-link>
      </nav>
      <div class="user-actions">
        <span>{{ userStore.userName || '已登录用户' }}</span>
        <button type="button" @click="logout">退出登录</button>
      </div>
    </header>
    <main>
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.app-header {
  background: #001529;
  color: #fff;
  padding: 12px 24px;
  display: flex;
  align-items: center;
  gap: 32px;
}
.app-header h1 {
  font-size: 18px;
  margin: 0;
}
.app-header nav {
  display: flex;
  align-items: center;
}
.app-header a {
  color: rgba(255, 255, 255, 0.75);
  text-decoration: none;
  font-size: 14px;
}
.app-header a:hover,
.app-header a.router-link-active {
  color: #fff;
}
.user-actions {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-left: auto;
  font-size: 13px;
}
.user-actions button {
  cursor: pointer;
  border: 1px solid rgba(255, 255, 255, 0.45);
  border-radius: 5px;
  padding: 5px 10px;
  background: transparent;
  color: #fff;
}
.user-actions button:hover {
  background: rgba(255, 255, 255, 0.1);
}
main {
  min-height: calc(100vh - 52px);
  background: #f0f2f5;
}
</style>
