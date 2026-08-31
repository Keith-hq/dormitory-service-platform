<script setup>
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getNavigation } from '@/config/navigation'
import { getRoleHome, ROLE_LABEL } from '@/router/roleAccess'
import { clearSession } from '@/store/session'
import { useUserStore } from '@/store/user'
import campusLogoMark from '@/assets/campus-logo-mark.png'

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
      <router-link class="brand" :to="roleHome" aria-label="返回高校宿舍后勤与共享生活服务系统首页">
        <img class="brand__mark" :src="campusLogoMark" alt="" aria-hidden="true" />
        <span class="brand__copy">
          <strong>高校宿舍后勤与共享生活服务系统</strong>
          <small>CAMPUS LIVING SERVICE</small>
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
          <b>{{ currentNav?.label || route.meta.title || '高校宿舍后勤与共享生活服务系统' }}</b>
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
  background:
    radial-gradient(circle at 18% 0, rgba(207, 232, 255, 0.78), transparent 34%),
    linear-gradient(180deg, #edf7ff 0, #f8fbff 340px, #f3f8ff 100%);
}
.app-sidebar {
  position: sticky;
  z-index: 50;
  top: 0;
  display: flex;
  width: 100%;
  min-height: 104px;
  align-items: center;
  overflow: visible;
  padding: 0 clamp(22px, 4vw, 60px);
  border-bottom: 0;
  background: linear-gradient(90deg, #074ea4, #0b65c8 58%, #078cba), var(--color-brand);
  color: #fff;
  box-shadow: 0 18px 45px rgba(7, 58, 124, 0.18);
  backdrop-filter: blur(16px);
  gap: 30px;
}
.brand {
  display: inline-flex;
  align-items: center;
  flex: 0 0 auto;
  gap: 12px;
  color: inherit;
  text-decoration: none;
}
.brand__mark {
  display: block;
  flex: 0 0 58px;
  width: 58px;
  height: 58px;
  box-sizing: border-box;
  border: 0;
  border-radius: var(--radius-lg);
  padding: 4px;
  background: #fff;
  object-fit: contain;
  box-shadow: 0 14px 28px rgba(8, 45, 105, 0.22);
}
.brand__copy {
  display: grid;
  gap: 2px;
  min-width: 260px;
}
.brand__copy strong {
  font-family: var(--font-display);
  font-size: 24px;
  font-weight: 900;
  line-height: 1.15;
  letter-spacing: 0;
}
.brand__copy small {
  color: rgba(255, 255, 255, 0.72);
  font-size: 14px;
  font-weight: 800;
  letter-spacing: 0;
}
.sidebar-context {
  display: none;
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
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex: 1 1 auto;
  gap: 2px;
  min-width: 0;
  align-self: stretch;
}
.primary-nav a {
  display: flex;
  align-items: center;
  min-height: 100%;
  padding: 0 16px;
  border: 1px solid transparent;
  border-radius: 0;
  color: rgba(255, 255, 255, 0.9);
  font-weight: 850;
  text-decoration: none;
  transition: 0.16s ease;
}
.primary-nav > a > span {
  display: none;
}
.primary-nav a div {
  display: block;
}
.primary-nav a b {
  font-size: 17px;
  font-weight: 950;
  white-space: nowrap;
}
.primary-nav a small {
  display: none;
}
.primary-nav a:hover {
  background: rgba(255, 255, 255, 0.12);
  color: #fff;
}
.primary-nav a.router-link-active {
  border-color: transparent;
  background: rgba(255, 255, 255, 0.18);
  color: #fff;
  box-shadow: none;
}
.primary-nav a.router-link-active span {
  color: #fff;
}
.sidebar-status {
  display: none;
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
  display: flex;
  align-items: center;
  flex: 0 0 auto;
  padding: 0;
  border: 0;
  gap: 12px;
}
.user-avatar {
  display: grid;
  width: 44px;
  height: 44px;
  place-items: center;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.2);
  color: #fff;
  font-family: var(--font-display);
  font-weight: 900;
}
.user-copy {
  display: grid;
  min-width: 0;
  gap: 2px;
}
.user-copy strong {
  overflow: hidden;
  color: #fff;
  font-size: 15px;
  font-weight: 900;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.user-copy small {
  color: rgba(255, 255, 255, 0.72);
  font-size: 12px;
  font-weight: 700;
}
.sidebar-profile button {
  min-height: 48px;
  padding: 0 18px;
  border: 1px solid rgba(255, 255, 255, 0.32);
  border-radius: var(--radius-lg);
  background: rgba(255, 255, 255, 0.14);
  color: #fff;
  font-size: 15px;
  font-weight: 900;
  box-shadow: none;
  cursor: pointer;
}
.sidebar-profile button:hover {
  color: #fff;
  transform: translateY(-1px);
}
.app-frame {
  min-height: 100svh;
  margin-left: 0;
}
.app-topbar {
  position: relative;
  z-index: 1;
  display: flex;
  min-height: 82px;
  align-items: center;
  justify-content: space-between;
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding: 0;
  border-bottom: 0;
  background: transparent;
  backdrop-filter: none;
}
.topbar-route {
  display: grid;
  align-items: start;
  gap: 4px;
}
.topbar-route span {
  color: var(--color-accent-strong);
  font-size: 13px;
  font-weight: 950;
  letter-spacing: 0;
}
.topbar-route b {
  display: block;
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 950;
  color: var(--color-ink);
}
.topbar-meta {
  display: flex;
  align-items: center;
  color: var(--color-text-muted);
  font: 13px var(--font-body);
  font-weight: 800;
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
  min-height: calc(100svh - 158px);
  padding-bottom: 54px;
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
@media (max-width: 1180px) {
  .app-sidebar {
    align-items: flex-start;
    flex-wrap: wrap;
    padding: 18px;
    gap: 14px;
  }
  .primary-nav {
    display: flex;
    order: 3;
    width: 100%;
    margin-top: 2px;
    justify-content: flex-start;
    overflow-x: auto;
  }
  .primary-nav a {
    min-width: auto;
    min-height: 48px;
    border-radius: var(--radius-lg);
  }
  .sidebar-profile {
    margin-left: auto;
  }
}
@media (max-width: 820px) {
  .app-frame {
    margin-left: 0;
  }
  .app-topbar {
    width: min(100% - 32px, var(--content-max));
  }
  .topbar-meta span {
    display: none;
  }
  .brand__copy strong {
    font-size: 17px;
  }
  .brand__copy small,
  .user-copy {
    display: none;
  }
  .sidebar-profile button {
    min-height: 42px;
    padding: 0 14px;
  }
}
</style>
