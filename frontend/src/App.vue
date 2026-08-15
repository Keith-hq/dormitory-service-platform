<script setup>
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getNavigation } from '@/config/navigation'
import { getRoleHome, ROLE_LABEL } from '@/router/roleAccess'
import { clearSession } from '@/store/session'
import { useUserStore } from '@/store/user'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

const showAppShell = computed(() => !['auth', 'prototype'].includes(route.meta.layout))
const userInitial = computed(() => userStore.userName?.trim().slice(0, 1) || '用')
const userRole = computed(() => userStore.userInfo?.role || '')
const roleHome = computed(() => getRoleHome(userRole.value))
const roleLabel = computed(() => ROLE_LABEL[userRole.value] || '平台用户')
const navigation = computed(() => getNavigation(userRole.value))
const currentNav = computed(() => {
  const exact = navigation.value.find((item) => route.path === item.to)
  if (exact) return exact
  return [...navigation.value]
    .sort((first, second) => second.to.length - first.to.length)
    .find((item) => route.path.startsWith(`${item.to}/`))
})

const logout = async () => {
  await clearSession()
  await router.replace({ name: 'Login' })
}
</script>

<template>
  <router-view v-if="!showAppShell" />
  <div v-else class="app-shell">
    <aside class="app-sidebar">
      <router-link class="brand" :to="roleHome" aria-label="返回宿舍服务台首页">
        <span class="brand__mark" aria-hidden="true">舍</span>
        <span class="brand__copy">
          <strong>住校誌</strong>
          <small>CAMPUS LIVING</small>
        </span>
      </router-link>

      <div class="sidebar-context">
        <span>当前空间</span>
        <strong>{{ roleLabel }}工作区</strong>
      </div>

      <nav class="primary-nav" aria-label="主导航">
        <router-link v-for="item in navigation" :key="item.to" :to="item.to">
          <span>{{ item.index }}</span>
          <div>
            <b>{{ item.label }}</b
            ><small>{{ item.eyebrow }}</small>
          </div>
        </router-link>
      </nav>

      <div class="sidebar-status">
        <i aria-hidden="true"></i>
        <div><b>服务运行正常</b><span>API CHANNEL / ONLINE</span></div>
      </div>

      <div class="sidebar-profile">
        <span class="user-avatar" aria-hidden="true">{{ userInitial }}</span>
        <span class="user-copy">
          <strong>{{ userStore.userName || '已登录用户' }}</strong>
          <small>{{ roleLabel }}</small>
        </span>
        <button type="button" aria-label="退出登录" @click="logout">退出</button>
      </div>
    </aside>

    <div class="app-frame">
      <header class="app-topbar">
        <div class="topbar-route">
          <span>{{ currentNav?.eyebrow || 'WORKSPACE' }}</span>
          <b>{{ currentNav?.label || route.meta.title || '宿舍服务平台' }}</b>
        </div>
        <div class="topbar-meta">
          <span><i></i> 数据通道已连接</span>
          <time>2026 · 08 · 15</time>
        </div>
      </header>

      <main class="app-main">
        <router-view v-slot="{ Component }">
          <transition name="page" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </main>
    </div>
  </div>
</template>

<style scoped>
.app-shell {
  min-height: 100svh;
}
.app-sidebar {
  position: fixed;
  z-index: 50;
  inset: 0 auto 0 0;
  display: flex;
  width: 244px;
  flex-direction: column;
  overflow: hidden;
  padding: 30px 22px 22px;
  background: var(--color-ink);
  color: var(--color-paper);
}
.brand {
  display: inline-flex;
  align-items: center;
  gap: 12px;
  color: inherit;
  text-decoration: none;
}
.brand__mark {
  display: grid;
  width: 42px;
  height: 42px;
  place-items: center;
  border: 1px solid var(--color-accent);
  color: var(--color-accent);
  font-family: var(--font-display);
  font-size: 20px;
  transform: rotate(-3deg);
}
.brand__copy {
  display: grid;
  gap: 1px;
}
.brand__copy strong {
  font-family: var(--font-display);
  font-size: 20px;
  letter-spacing: 0.11em;
}
.brand__copy small {
  color: #92a198;
  font-size: 8px;
  letter-spacing: 0.18em;
}
.sidebar-context {
  display: grid;
  margin: 48px 9px 12px;
  gap: 4px;
}
.sidebar-context span {
  color: #718278;
  font-size: 8px;
  letter-spacing: 0.15em;
}
.sidebar-context strong {
  color: #dfe3d9;
  font-family: var(--font-display);
  font-size: 13px;
  font-weight: 500;
}
.primary-nav {
  display: grid;
  gap: 3px;
}
.primary-nav a {
  display: grid;
  grid-template-columns: 30px 1fr;
  align-items: center;
  min-height: 58px;
  padding: 8px 10px;
  border-left: 2px solid transparent;
  color: #89988f;
  text-decoration: none;
  transition: 0.16s ease;
}
.primary-nav > a > span {
  color: #56685e;
  font: 8px var(--font-mono);
}
.primary-nav a div {
  display: grid;
  gap: 3px;
}
.primary-nav a b {
  font-size: 12px;
  font-weight: 600;
}
.primary-nav a small {
  color: #506157;
  font-size: 7px;
  letter-spacing: 0.13em;
}
.primary-nav a:hover {
  background: rgba(255, 255, 255, 0.035);
  color: #f1eadc;
}
.primary-nav a.router-link-active {
  border-color: var(--color-accent);
  background: rgba(255, 255, 255, 0.045);
  color: #fff7e8;
}
.primary-nav a.router-link-active span {
  color: var(--color-accent);
}
.sidebar-status {
  display: flex;
  align-items: center;
  margin-top: auto;
  padding: 14px 10px;
  border: 1px solid #3a4d43;
  gap: 10px;
}
.sidebar-status > i {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #70a785;
  box-shadow: 0 0 0 4px rgba(112, 167, 133, 0.12);
}
.sidebar-status div {
  display: grid;
  gap: 2px;
}
.sidebar-status b {
  font-size: 9px;
}
.sidebar-status span {
  color: #63776c;
  font: 7px var(--font-mono);
  letter-spacing: 0.08em;
}
.sidebar-profile {
  display: grid;
  grid-template-columns: 34px 1fr auto;
  align-items: center;
  margin-top: 14px;
  padding-top: 17px;
  border-top: 1px solid #35473e;
  gap: 9px;
}
.user-avatar {
  display: grid;
  width: 34px;
  height: 34px;
  place-items: center;
  border-radius: 50%;
  background: var(--color-paper);
  color: var(--color-ink);
  font-family: var(--font-display);
}
.user-copy {
  display: grid;
  min-width: 0;
  gap: 2px;
}
.user-copy strong {
  overflow: hidden;
  color: #f4eddf;
  font-size: 10px;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.user-copy small {
  color: #71857a;
  font-size: 8px;
}
.sidebar-profile button {
  padding: 4px;
  border: 0;
  background: none;
  color: #87978e;
  font-size: 9px;
  cursor: pointer;
}
.sidebar-profile button:hover {
  color: #e7a07e;
}
.app-frame {
  min-height: 100svh;
  margin-left: 244px;
}
.app-topbar {
  position: sticky;
  z-index: 40;
  top: 0;
  display: flex;
  min-height: 64px;
  align-items: center;
  justify-content: space-between;
  padding: 0 clamp(24px, 4vw, 58px);
  border-bottom: 1px solid var(--color-line-strong);
  background: rgba(246, 241, 230, 0.92);
  backdrop-filter: blur(16px);
}
.topbar-route {
  display: flex;
  align-items: baseline;
  gap: 9px;
}
.topbar-route span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.13em;
}
.topbar-route b {
  font-family: var(--font-display);
  font-size: 13px;
  font-weight: 500;
}
.topbar-meta {
  display: flex;
  align-items: center;
  color: var(--color-text-muted);
  font: 8px var(--font-mono);
  gap: 24px;
}
.topbar-meta span {
  display: flex;
  align-items: center;
  gap: 7px;
}
.topbar-meta i {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #5e9473;
}
.app-main {
  min-height: calc(100svh - 65px);
}
.page-enter-active,
.page-leave-active {
  transition:
    opacity 0.16s ease,
    transform 0.16s ease;
}
.page-enter-from {
  opacity: 0;
  transform: translateY(5px);
}
.page-leave-to {
  opacity: 0;
  transform: translateY(-3px);
}
@media (max-width: 820px) {
  .app-sidebar {
    position: static;
    width: 100%;
    padding: 15px 18px;
  }
  .sidebar-context,
  .sidebar-status,
  .sidebar-profile {
    display: none;
  }
  .primary-nav {
    display: flex;
    margin-top: 13px;
    overflow-x: auto;
  }
  .primary-nav a {
    min-width: 126px;
    border-left: 0;
    border-bottom: 2px solid transparent;
  }
  .primary-nav a.router-link-active {
    border-bottom-color: var(--color-accent);
  }
  .app-frame {
    margin-left: 0;
  }
  .app-topbar {
    padding: 0 18px;
  }
  .topbar-meta span {
    display: none;
  }
}
</style>
