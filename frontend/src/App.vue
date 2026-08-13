<script setup>
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { clearSession } from '@/store/session'
import { useUserStore } from '@/store/user'
import { getRoleHome, ROLE_LABEL } from '@/router/roleAccess'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

const showAppShell = computed(() => route.meta.layout !== 'auth')
const userInitial = computed(() => userStore.userName?.trim().slice(0, 1) || '用')
const userRole = computed(() => userStore.userInfo?.role || '')
const roleHome = computed(() => getRoleHome(userRole.value))
const roleLabel = computed(() => ROLE_LABEL[userRole.value] || '平台用户')
const navigation = computed(() => {
  if (userRole.value === 'student') {
    return [
      { to: '/student', label: '生活首页' },
      { to: '/student/services', label: '我的服务' }
    ]
  }

  return [
    { to: '/admin', label: '运营首页' },
    { to: '/admin/operations', label: '值班工作台' },
    { to: '/building', label: '楼栋档案' }
  ]
})

const logout = async () => {
  await clearSession()
  await router.replace({ name: 'Login' })
}
</script>

<template>
  <router-view v-if="!showAppShell" />
  <div v-else class="app-shell">
    <header class="app-header">
      <div class="app-header__inner">
        <router-link class="brand" :to="roleHome" aria-label="返回宿舍服务台首页">
          <span class="brand__mark" aria-hidden="true">舍</span>
          <span class="brand__copy">
            <strong>宿舍服务台</strong>
            <small>Campus Living Operations</small>
          </span>
        </router-link>

        <nav class="primary-nav" aria-label="主导航">
          <router-link v-for="item in navigation" :key="item.to" :to="item.to">
            <span class="nav-dot" aria-hidden="true"></span>
            {{ item.label }}
          </router-link>
        </nav>

        <div class="user-actions">
          <span class="user-avatar" aria-hidden="true">{{ userInitial }}</span>
          <span class="user-copy">
            <strong>{{ userStore.userName || '已登录用户' }}</strong>
            <small>{{ roleLabel }}</small>
          </span>
          <button type="button" class="btn btn-ghost logout-button" @click="logout">退出</button>
        </div>
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
</template>

<style scoped>
.app-shell {
  min-height: 100svh;
}

.app-header {
  position: sticky;
  z-index: 40;
  top: 0;
  border-bottom: 1px solid rgba(208, 216, 208, 0.88);
  background: rgba(250, 248, 242, 0.9);
  backdrop-filter: blur(18px);
}

.app-header__inner {
  display: flex;
  align-items: center;
  width: min(100% - 40px, var(--content-max));
  min-height: 72px;
  margin: 0 auto;
  gap: var(--space-8);
}

.brand {
  display: inline-flex;
  align-items: center;
  gap: var(--space-3);
  color: var(--color-ink);
  text-decoration: none;
}

.brand__mark {
  display: grid;
  width: 40px;
  height: 40px;
  place-items: center;
  border-radius: 12px 4px 12px 4px;
  background: var(--color-ink);
  color: var(--color-surface);
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 700;
  box-shadow: 5px 5px 0 var(--color-brand-soft);
}

.brand__copy {
  display: grid;
  gap: 1px;
  text-align: left;
}

.brand__copy strong {
  font-family: var(--font-display);
  font-size: 17px;
  letter-spacing: 0.03em;
}

.brand__copy small {
  color: var(--color-text-soft);
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.13em;
  text-transform: uppercase;
}

.primary-nav {
  display: flex;
  align-items: center;
  align-self: stretch;
}

.primary-nav a {
  position: relative;
  display: inline-flex;
  align-items: center;
  height: 100%;
  gap: var(--space-2);
  padding: 0 var(--space-3);
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 700;
  text-decoration: none;
}

.primary-nav a::after {
  content: '';
  position: absolute;
  right: var(--space-3);
  bottom: -1px;
  left: var(--space-3);
  height: 2px;
  border-radius: 999px 999px 0 0;
  background: var(--color-brand);
  opacity: 0;
  transform: scaleX(0.4);
  transition:
    opacity 0.18s ease,
    transform 0.18s ease;
}

.primary-nav a:hover,
.primary-nav a.router-link-active {
  color: var(--color-brand-strong);
}

.primary-nav a.router-link-active::after {
  opacity: 1;
  transform: scaleX(1);
}

.nav-dot {
  width: 6px;
  height: 6px;
  border: 1px solid currentColor;
  border-radius: 50%;
}

.router-link-active .nav-dot {
  border-color: var(--color-brand);
  background: var(--color-brand);
  box-shadow: 0 0 0 4px var(--color-brand-soft);
}

.user-actions {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-left: auto;
}

.user-avatar {
  display: grid;
  width: 34px;
  height: 34px;
  place-items: center;
  border: 1px solid var(--color-brand-border);
  border-radius: 50%;
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-family: var(--font-display);
  font-size: 15px;
  font-weight: 700;
}

.user-copy {
  display: grid;
  min-width: 72px;
  gap: 1px;
  text-align: left;
}

.user-copy strong {
  overflow: hidden;
  max-width: 120px;
  color: var(--color-ink);
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-copy small {
  color: var(--color-text-soft);
  font-size: 10px;
}

.logout-button {
  min-height: 32px;
  padding: 5px 10px;
}

.app-main {
  min-height: calc(100svh - 73px);
}

.page-enter-active,
.page-leave-active {
  transition:
    opacity 0.18s ease,
    transform 0.18s ease;
}

.page-enter-from,
.page-leave-to {
  opacity: 0;
  transform: translateY(5px);
}

@media (max-width: 760px) {
  .app-header__inner {
    width: min(100% - 24px, var(--content-max));
    min-height: 64px;
    gap: var(--space-4);
  }

  .brand__copy small,
  .user-copy {
    display: none;
  }

  .primary-nav a {
    padding-inline: var(--space-2);
  }

  .primary-nav a::after {
    right: var(--space-2);
    left: var(--space-2);
  }
}

@media (max-width: 520px) {
  .brand__copy {
    display: none;
  }

  .brand__mark {
    width: 36px;
    height: 36px;
  }

  .user-avatar {
    display: none;
  }
}
</style>
