<script setup>
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { authApi } from '@/api/auth'
import { getSafeAuthRedirect } from '@/router/authRedirect'
import { getRoleHome } from '@/router/roleAccess'
import { useUserStore } from '@/store/user'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const mockEnabled = import.meta.env.DEV && import.meta.env.VITE_USE_MOCK === 'true'
const mockAccounts = import.meta.env.DEV
  ? [
      { label: '学生', loginName: 'student001', password: '123456' },
      { label: '宿管', loginName: 'admin001', password: '123456' }
    ]
  : []

const loginName = ref('')
const password = ref('')
const loading = ref(false)
const errorMessage = ref('')

const clearError = () => {
  errorMessage.value = ''
}

const useMockAccount = (account) => {
  loginName.value = account.loginName
  password.value = account.password
  clearError()
}

const submitLogin = async () => {
  clearError()

  const normalizedLoginName = loginName.value.trim()
  if (!normalizedLoginName || !password.value) {
    errorMessage.value = '请输入登录名和密码'
    return
  }

  loading.value = true
  try {
    const session = await authApi.login({
      loginName: normalizedLoginName,
      password: password.value
    })
    userStore.setSession(session)
    await router.replace(
      getSafeAuthRedirect(route.query.redirect, getRoleHome(session.userInfo.role))
    )
  } catch (error) {
    errorMessage.value = error.message || '登录失败，请稍后重试'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <section class="login-intro" aria-labelledby="platform-title">
      <span class="eyebrow">DORMITORY SERVICE PLATFORM</span>
      <h1 id="platform-title">让宿舍服务更清晰、更高效</h1>
      <p>统一管理楼栋、房间、报修与生活服务，让每一次申请都有回应。</p>
    </section>

    <section class="login-card" aria-labelledby="login-title">
      <div class="login-heading">
        <span class="brand-mark" aria-hidden="true">舍</span>
        <div>
          <h2 id="login-title">欢迎登录</h2>
          <p>请输入你的平台账号</p>
        </div>
      </div>

      <form class="login-form" @submit.prevent="submitLogin">
        <label for="login-name">登录名</label>
        <input
          id="login-name"
          v-model="loginName"
          name="loginName"
          type="text"
          autocomplete="username"
          placeholder="请输入登录名"
          required
          aria-required="true"
          :aria-invalid="Boolean(errorMessage)"
          :aria-describedby="errorMessage ? 'login-error' : undefined"
          :disabled="loading"
          @input="clearError"
        />

        <label for="password">密码</label>
        <input
          id="password"
          v-model="password"
          name="password"
          type="password"
          autocomplete="current-password"
          placeholder="请输入密码"
          required
          aria-required="true"
          :aria-invalid="Boolean(errorMessage)"
          :aria-describedby="errorMessage ? 'login-error' : undefined"
          :disabled="loading"
          @input="clearError"
        />

        <p v-if="errorMessage" id="login-error" class="login-error" role="alert">
          {{ errorMessage }}
        </p>

        <button type="submit" :disabled="loading">
          {{ loading ? '登录中…' : '登录' }}
        </button>
      </form>

      <aside v-if="mockEnabled" class="mock-tip">
        <strong>选择 Mock 角色</strong>
        <span>测试密码统一为 123456</span>
        <div class="mock-accounts">
          <button
            v-for="account in mockAccounts"
            :key="account.loginName"
            type="button"
            @click="useMockAccount(account)"
          >
            {{ account.label }} · {{ account.loginName }}
          </button>
        </div>
      </aside>
    </section>
  </main>
</template>

<style scoped>
.login-page {
  min-height: 100svh;
  display: grid;
  grid-template-columns: minmax(0, 1.15fr) minmax(360px, 0.85fr);
  background: var(--color-canvas);
  color: var(--color-ink);
}

.login-intro {
  position: relative;
  display: flex;
  flex-direction: column;
  justify-content: center;
  overflow: hidden;
  padding: clamp(48px, 8vw, 112px);
  text-align: left;
  background:
    linear-gradient(rgba(255, 255, 255, 0.045) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255, 255, 255, 0.045) 1px, transparent 1px),
    radial-gradient(circle at 16% 18%, rgba(110, 213, 190, 0.24), transparent 32%),
    linear-gradient(145deg, #132f33 0%, #135c59 58%, #168279 100%);
  background-size:
    40px 40px,
    40px 40px,
    auto,
    auto;
  color: #fff;
}

.login-intro::after {
  content: '';
  position: absolute;
  right: -120px;
  bottom: -150px;
  width: 420px;
  height: 420px;
  border: 1px solid rgba(255, 255, 255, 0.18);
  border-radius: 50%;
  box-shadow:
    0 0 0 64px rgba(255, 255, 255, 0.04),
    0 0 0 128px rgba(255, 255, 255, 0.025);
}

.eyebrow {
  margin-bottom: 24px;
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.18em;
  opacity: 0.72;
}

.login-intro h1 {
  max-width: 600px;
  margin: 0 0 22px;
  font-size: clamp(42px, 5vw, 68px);
  line-height: 1.08;
  color: #fffdf8;
  font-family: var(--font-display);
  font-weight: 700;
  letter-spacing: -0.04em;
}

.login-intro p {
  max-width: 520px;
  font-size: 17px;
  line-height: 1.8;
  color: rgba(255, 255, 255, 0.76);
}

.login-card {
  align-self: center;
  width: min(420px, calc(100% - 64px));
  margin: 48px auto;
  padding: 40px;
  box-sizing: border-box;
  border: 1px solid var(--color-line);
  border-radius: var(--radius-xl);
  background: var(--color-surface);
  box-shadow: var(--shadow-lift);
  text-align: left;
}

.login-heading {
  display: flex;
  align-items: center;
  gap: 14px;
  margin-bottom: 32px;
}

.brand-mark {
  display: grid;
  width: 48px;
  height: 48px;
  place-items: center;
  border-radius: var(--radius-md);
  background: var(--color-ink);
  color: #fff;
  font-size: 21px;
  font-weight: 700;
}

.login-heading h2 {
  margin: 0 0 5px;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 25px;
  font-weight: 700;
}

.login-heading p {
  color: var(--color-text-muted);
  font-size: 14px;
}

.login-form {
  display: flex;
  flex-direction: column;
}

.login-form label {
  margin: 0 0 8px;
  color: var(--color-text);
  font-size: 14px;
  font-weight: 600;
}

.login-form input {
  width: 100%;
  height: 46px;
  margin-bottom: 20px;
  padding: 0 14px;
  box-sizing: border-box;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-ink);
  font: inherit;
  font-size: 15px;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
}

.login-form input:focus {
  border-color: var(--color-brand);
  outline: none;
  box-shadow: 0 0 0 3px var(--color-focus);
}

.login-form input:disabled {
  background: var(--color-canvas-deep);
  cursor: not-allowed;
}

.login-form button {
  height: 48px;
  margin-top: 4px;
  border: 0;
  border-radius: var(--radius-sm);
  background: var(--color-brand);
  color: #fff;
  font: inherit;
  font-weight: 700;
  cursor: pointer;
  transition:
    background 0.2s,
    transform 0.2s;
}

.login-form button:hover:not(:disabled) {
  background: var(--color-brand-strong);
  transform: translateY(-1px);
}

.login-form button:focus-visible {
  outline: 3px solid var(--color-focus);
  outline-offset: 2px;
}

.login-form button:disabled {
  opacity: 0.65;
  cursor: wait;
}

.login-error {
  margin: -8px 0 16px;
  padding: 10px 12px;
  border-radius: var(--radius-sm);
  background: var(--color-danger-soft);
  color: var(--color-danger);
  font-size: 13px;
}

.mock-tip {
  display: grid;
  gap: 4px;
  margin-top: 24px;
  padding: 13px 15px;
  border: 1px solid var(--color-brand-border);
  border-radius: var(--radius-sm);
  background: var(--color-brand-soft);
  color: var(--color-text);
  font-size: 13px;
}

.mock-accounts {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
  margin-top: 8px;
}

.mock-accounts button {
  min-height: 36px;
  border: 1px solid var(--color-brand-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-brand-strong);
  font: inherit;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
}

.mock-accounts button:hover,
.mock-accounts button:focus-visible {
  border-color: var(--color-brand);
  outline: 2px solid var(--color-focus);
  outline-offset: 1px;
}

.mock-tip strong {
  color: var(--color-brand-strong);
}

@media (max-width: 820px) {
  .login-page {
    grid-template-columns: 1fr;
  }

  .login-intro {
    min-height: 220px;
    padding: 40px 28px;
  }

  .login-intro h1 {
    max-width: 480px;
    font-size: 38px;
  }

  .login-intro p {
    display: none;
  }

  .login-card {
    width: min(420px, calc(100% - 32px));
    margin: 28px auto;
    padding: 30px 24px;
  }
}
</style>
