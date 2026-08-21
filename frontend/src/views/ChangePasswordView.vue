<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { authApi } from '@/api/auth'
import { getRoleHome } from '@/router/roleAccess'
import { clearSession } from '@/store/session'
import { useUserStore } from '@/store/user'
import { toUserMessage } from '@/utils/errorMessage'

const router = useRouter()
const userStore = useUserStore()
const form = ref({ oldPassword: '', newPassword: '', confirmPassword: '' })
const loading = ref(false)
const error = ref('')
const strengthReady = computed(
  () =>
    form.value.newPassword.length >= 8 &&
    /[A-Za-z]/.test(form.value.newPassword) &&
    /\d/.test(form.value.newPassword)
)

const submit = async () => {
  error.value = ''
  if (!strengthReady.value) {
    error.value = '新密码至少 8 位，且必须同时包含字母和数字'
    return
  }
  if (form.value.newPassword !== form.value.confirmPassword) {
    error.value = '两次输入的新密码不一致'
    return
  }

  loading.value = true
  try {
    await authApi.changePassword({
      oldPassword: form.value.oldPassword,
      newPassword: form.value.newPassword
    })
    userStore.markPasswordChanged()
    await router.replace(getRoleHome(userStore.userInfo?.role))
  } catch (requestError) {
    error.value = toUserMessage(requestError, '密码修改失败，请稍后重试')
  } finally {
    loading.value = false
  }
}

const logout = async () => {
  await clearSession()
  await router.replace({ name: 'Login' })
}
</script>

<template>
  <main class="password-page">
    <section class="security-note">
      <span>FIRST ACCESS PROTOCOL</span>
      <div class="security-mark" aria-hidden="true">01</div>
      <h1>先把临时密码<br />换成你的密码</h1>
      <p>这是新账号第一次进入系统。完成改密后，系统才会开放对应角色的工作区。</p>
      <ul>
        <li>不少于 8 个字符</li>
        <li>同时包含字母与数字</li>
        <li>不要沿用初始临时密码</li>
      </ul>
    </section>

    <section class="password-card" aria-labelledby="change-password-title">
      <header>
        <span>舍</span>
        <div>
          <p>ACCOUNT SECURITY</p>
          <h2 id="change-password-title">首次登录改密</h2>
        </div>
      </header>
      <form @submit.prevent="submit">
        <label
          >当前临时密码<input
            v-model="form.oldPassword"
            required
            autocomplete="current-password"
            type="password"
        /></label>
        <label
          >设置新密码<input
            v-model="form.newPassword"
            required
            autocomplete="new-password"
            type="password"
        /></label>
        <div class="strength" :class="{ ready: strengthReady }">
          <i></i><span>{{ strengthReady ? '密码强度符合要求' : '至少 8 位，包含字母与数字' }}</span>
        </div>
        <label
          >再次输入新密码<input
            v-model="form.confirmPassword"
            required
            autocomplete="new-password"
            type="password"
        /></label>
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <button class="submit-button" :disabled="loading">
          {{ loading ? '正在更新…' : '保存并进入系统' }}
        </button>
        <button class="logout-button" type="button" @click="logout">退出并更换账号</button>
      </form>
    </section>
  </main>
</template>

<style scoped>
.password-page {
  display: grid;
  grid-template-columns: minmax(0, 1.15fr) minmax(380px, 0.85fr);
  min-height: 100svh;
  background: var(--color-canvas);
}
.security-note {
  display: flex;
  position: relative;
  flex-direction: column;
  justify-content: center;
  overflow: hidden;
  padding: clamp(50px, 8vw, 120px);
  background: var(--color-ink);
  color: #fff;
}
.security-note:after {
  content: '';
  position: absolute;
  right: -140px;
  bottom: -170px;
  width: 480px;
  height: 480px;
  border: 1px solid rgba(208, 108, 81, 0.38);
  border-radius: 50%;
  box-shadow:
    0 0 0 70px rgba(208, 108, 81, 0.05),
    0 0 0 140px rgba(208, 108, 81, 0.025);
}
.security-note > span {
  color: var(--color-accent);
  font: 9px var(--font-mono);
  letter-spacing: 0.18em;
}
.security-mark {
  position: absolute;
  top: 50px;
  right: 8%;
  color: rgba(255, 255, 255, 0.035);
  font: 700 clamp(140px, 22vw, 320px) var(--font-display);
}
.security-note h1 {
  position: relative;
  max-width: 660px;
  margin: 26px 0 18px;
  font: 600 clamp(43px, 6vw, 76px)/1.08 var(--font-display);
  letter-spacing: -0.055em;
}
.security-note p {
  max-width: 520px;
  color: #9eaaa3;
  font-size: 13px;
  line-height: 1.8;
}
.security-note ul {
  position: relative;
  display: flex;
  flex-wrap: wrap;
  padding: 0;
  margin: 25px 0 0;
  list-style: none;
  gap: 8px;
}
.security-note li {
  padding: 8px 11px;
  border: 1px solid #405047;
  color: #c3ccc5;
  font-size: 9px;
}
.password-card {
  align-self: center;
  width: min(430px, calc(100% - 64px));
  margin: 45px auto;
  padding: 38px;
  border: 1px solid var(--color-line-strong);
  background: var(--color-surface);
  box-shadow: var(--shadow-lift);
}
.password-card header {
  display: flex;
  align-items: center;
  padding-bottom: 25px;
  border-bottom: 1px solid var(--color-line);
  gap: 13px;
}
.password-card header > span {
  display: grid;
  width: 45px;
  height: 45px;
  place-items: center;
  border: 1px solid var(--color-accent);
  color: var(--color-accent);
  font: 20px var(--font-display);
  transform: rotate(-3deg);
}
.password-card header p {
  margin: 0;
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.14em;
}
.password-card h2 {
  margin: 5px 0 0;
  font: 600 24px var(--font-display);
}
.password-card form {
  display: grid;
  gap: 17px;
  margin-top: 25px;
}
.password-card label {
  display: flex;
  flex-direction: column;
  gap: 7px;
  color: var(--color-text-muted);
  font-size: 10px;
}
.password-card input {
  min-height: 43px;
  padding: 10px 12px;
  border: 1px solid var(--color-line-strong);
  background: #fffaf0;
}
.password-card input:focus {
  border-color: var(--color-brand);
  outline: 3px solid var(--color-focus);
}
.strength {
  display: flex;
  align-items: center;
  margin-top: -8px;
  color: var(--color-text-soft);
  font-size: 9px;
  gap: 8px;
}
.strength i {
  width: 36px;
  height: 3px;
  background: var(--color-line-strong);
}
.strength.ready {
  color: var(--color-brand);
}
.strength.ready i {
  background: var(--color-brand);
}
.form-error {
  margin: 0;
  padding: 10px;
  border-left: 3px solid var(--color-danger);
  background: var(--color-danger-soft);
  color: var(--color-danger);
  font-size: 10px;
}
.submit-button {
  min-height: 44px;
  border: 0;
  background: var(--color-brand);
  color: #fff;
  font-weight: 700;
  cursor: pointer;
}
.submit-button:disabled {
  opacity: 0.55;
}
.logout-button {
  border: 0;
  background: none;
  color: var(--color-text-muted);
  font-size: 10px;
  cursor: pointer;
}
@media (max-width: 820px) {
  .password-page {
    grid-template-columns: 1fr;
  }
  .security-note {
    min-height: 42svh;
    padding: 48px 32px;
  }
  .security-note h1 {
    font-size: 42px;
  }
  .password-card {
    margin: 34px auto;
  }
}
</style>
