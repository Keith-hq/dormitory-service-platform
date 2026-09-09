<script setup>
import { computed, onMounted, ref } from 'vue'
import { authApi } from '@/api/auth'
import { studentApi } from '@/api/student'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const userStore = useUserStore()
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const feedback = ref('')
const accommodation = ref(null)
const history = ref([])
const phone = ref(userStore.userInfo?.phone || '')
const email = ref(userStore.userInfo?.email || '')

const studentId = computed(() => userStore.userInfo?.id || '')
const residenceFields = computed(() => [
  {
    label: '住宿状态',
    value: accommodation.value?.checkOutDate ? '已退宿' : accommodation.value ? '在住' : '待同步'
  },
  { label: '房间编号', value: accommodation.value?.roomId || '—' },
  {
    label: '床位号',
    value: accommodation.value?.bedNo ? `${accommodation.value.bedNo} 号床` : '—'
  },
  {
    label: '入住日期',
    value: accommodation.value?.checkInDate
      ? new Date(accommodation.value.checkInDate).toLocaleDateString('zh-CN')
      : '—'
  }
])

const loadProfile = async () => {
  loading.value = true
  error.value = ''
  const [current, records] = await Promise.allSettled([
    studentApi.getAccommodation(studentId.value),
    studentApi.getAccommodationHistory(studentId.value)
  ])
  if (current.status === 'fulfilled') accommodation.value = current.value
  if (records.status === 'fulfilled') history.value = normalizeCollection(records.value).items
  if (current.status === 'rejected' && records.status === 'rejected')
    error.value = '住宿档案暂时无法同步'
  loading.value = false
}

const saveProfile = async () => {
  saving.value = true
  feedback.value = ''
  try {
    await studentApi.updateProfile(studentId.value, {
      phone: phone.value || null,
      email: email.value || null
    })
    feedback.value = '联系方式已更新'
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '保存失败，请稍后重试')
  } finally {
    saving.value = false
  }
}

// 楼栋以 /auth/me 实时为准：合并进 userInfo 并持久化，旧会话无需重新登录也能更新
const refreshServerBuilding = async () => {
  try {
    const me = await authApi.me()
    if (me && (me.buildingId != null || me.buildingName)) {
      userStore.syncServerUserInfo({
        buildingId: me.buildingId,
        buildingName: me.buildingName
      })
    }
  } catch {
    // 静默失败：沿用会话内已有值，不打扰页面
  }
}

onMounted(() => {
  loadProfile()
  refreshServerBuilding()
})
</script>

<template>
  <div class="profile-page workspace-page">
    <WorkspaceHeader
      eyebrow="STUDENT / DOSSIER"
      title="个人与住宿档案"
      description="个人联系方式由你维护，住宿与床位信息由宿管业务统一同步。"
    >
      <StatusTag
        :label="accommodation?.checkOutDate ? '已退宿' : accommodation ? '当前在住' : '等待同步'"
        :tone="accommodation?.checkOutDate ? 'default' : accommodation ? 'success' : 'warning'"
      />
    </WorkspaceHeader>

    <InlineState :loading="loading" :error="error" />

    <div v-if="!loading" class="profile-layout">
      <section class="identity-sheet">
        <header>
          <span>IDENTITY / VERIFIED</span><b>{{ userStore.userName?.slice(0, 1) || '学' }}</b>
        </header>
        <div class="identity-sheet__name">
          <p>学生身份</p>
          <h2>{{ userStore.userName || '学生用户' }}</h2>
          <small>{{ studentId }}</small>
        </div>
        <dl>
          <div>
            <dt>账户角色</dt>
            <dd>在校学生</dd>
          </div>
          <div>
            <dt>所属楼栋</dt>
            <dd>{{ userStore.userInfo?.buildingName || '待同步' }}</dd>
          </div>
          <div>
            <dt>房间信息</dt>
            <dd>{{ userStore.userInfo?.roomName || accommodation?.roomId || '待同步' }}</dd>
          </div>
        </dl>
      </section>

      <section class="contact-editor">
        <header>
          <div>
            <span>CONTACT / EDITABLE</span>
            <h2>联系方式</h2>
          </div>
          <small>用于通知与紧急联系</small>
        </header>
        <form @submit.prevent="saveProfile">
          <label
            ><span>手机号码</span
            ><input
              v-model.trim="phone"
              class="form-input"
              inputmode="numeric"
              maxlength="11"
              placeholder="请输入 11 位手机号"
          /></label>
          <label
            ><span>电子邮箱</span
            ><input
              v-model.trim="email"
              class="form-input"
              type="email"
              placeholder="name@example.com"
          /></label>
          <p v-if="feedback" role="status">{{ feedback }}</p>
          <button class="btn btn-primary" type="submit" :disabled="saving">
            {{ saving ? '保存中…' : '保存修改' }}
          </button>
        </form>
      </section>

      <section class="residence-sheet">
        <header>
          <span>RESIDENCE / CURRENT</span>
          <h2>当前住宿</h2>
        </header>
        <div class="residence-grid">
          <article v-for="field in residenceFields" :key="field.label">
            <span>{{ field.label }}</span
            ><strong>{{ field.value }}</strong>
          </article>
        </div>
        <footer><span>住宿记录由分配、调寝和退宿流程自动更新，学生端不可直接修改。</span></footer>
      </section>

      <section class="history-sheet">
        <header>
          <span>HISTORY / {{ history.length }}</span>
          <h2>住宿历史</h2>
        </header>
        <InlineState :empty="!history.length" empty-text="暂无历史住宿记录" />
        <article v-for="item in history" :key="item.allocationId">
          <time>{{
            item.checkInDate ? new Date(item.checkInDate).toLocaleDateString('zh-CN') : '—'
          }}</time>
          <div>
            <b>房间 {{ item.roomId }} · {{ item.bedNo }} 号床</b
            ><span>{{
              item.checkOutDate
                ? `退宿于 ${new Date(item.checkOutDate).toLocaleDateString('zh-CN')}`
                : '当前住宿'
            }}</span>
          </div>
        </article>
      </section>
    </div>
  </div>
</template>

<style scoped>
.workspace-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.profile-layout {
  display: grid;
  grid-template-columns: minmax(300px, 0.52fr) minmax(0, 1fr);
  margin-top: 36px;
  gap: 28px;
}
.identity-sheet {
  grid-row: 1/3;
  padding: 36px 38px;
  background: #fff;
  color: var(--color-text);
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1);
}
.identity-sheet > header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.identity-sheet > header span,
.contact-editor header span,
.residence-sheet header span,
.history-sheet header span {
  color: var(--color-brand);
  font: inherit;
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.identity-sheet > header b {
  display: grid;
  width: 50px;
  height: 50px;
  place-items: center;
  border-radius: 50%;
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 950;
}
.identity-sheet__name {
  padding: 58px 0 44px;
}
.identity-sheet__name p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 16px;
  line-height: 1.7;
}
.identity-sheet__name h2 {
  margin: 10px 0 8px;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 34px;
  font-weight: 950;
  line-height: 1.18;
}
.identity-sheet__name small {
  color: var(--color-text-muted);
  font: inherit;
  font-size: 15px;
  font-weight: 700;
}
.identity-sheet dl {
  display: grid;
  gap: 14px;
  margin: 0;
}
.identity-sheet dl div {
  display: grid;
  grid-template-columns: 118px 1fr;
  padding: 18px 20px;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  gap: 18px;
}
.identity-sheet dt {
  color: var(--color-text-muted);
  font-size: 16px;
  line-height: 1.6;
}
.identity-sheet dd {
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 17px;
  font-weight: 850;
  line-height: 1.6;
}
.contact-editor,
.residence-sheet,
.history-sheet {
  background: #fff;
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1);
}
.contact-editor > header,
.residence-sheet > header,
.history-sheet > header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  padding: 30px 34px 12px;
  gap: 20px;
}
.contact-editor h2,
.residence-sheet h2,
.history-sheet h2 {
  margin: 8px 0 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 30px;
  font-weight: 950;
  line-height: 1.2;
}
.contact-editor header small {
  color: var(--color-text-muted);
  font-size: 15px;
  font-weight: 700;
}
.contact-editor form {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  align-items: end;
  padding: 24px 34px 34px;
  gap: 20px;
}
.contact-editor label {
  display: grid;
  gap: 10px;
}
.contact-editor label span {
  color: var(--color-text-muted);
  font-size: 16px;
  font-weight: 750;
}
.contact-editor form p {
  grid-column: 1/-1;
  margin: 0;
  color: var(--color-brand);
  font-size: 15px;
  line-height: 1.7;
}
.residence-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  padding: 24px 34px 32px;
}
.residence-grid article {
  display: grid;
  min-height: 118px;
  align-content: space-between;
  padding: 22px;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
}
.residence-grid span {
  color: var(--color-text-muted);
  font-size: 16px;
  line-height: 1.6;
}
.residence-grid strong {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 22px;
  font-weight: 950;
  line-height: 1.35;
}
.residence-sheet footer {
  padding: 0 34px 34px;
  color: var(--color-text-muted);
  font-size: 15px;
  line-height: 1.8;
}
.history-sheet {
  grid-column: 1/-1;
  padding-bottom: 20px;
}
.history-sheet > article {
  display: grid;
  grid-template-columns: 120px 1fr;
  min-height: 84px;
  align-items: center;
  margin: 14px 34px;
  padding: 18px 22px;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  gap: 20px;
}
.history-sheet time {
  color: var(--color-brand);
  font: inherit;
  font-size: 15px;
  font-weight: 850;
}
.history-sheet article div {
  display: grid;
  gap: 8px;
}
.history-sheet article b {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 900;
}
.history-sheet article span {
  color: var(--color-text-muted);
  font-size: 15px;
  line-height: 1.7;
}
@media (max-width: 850px) {
  .workspace-page {
    width: min(100% - 32px, var(--content-max));
  }
  .profile-layout {
    grid-template-columns: 1fr;
  }
  .identity-sheet {
    grid-row: auto;
  }
  .contact-editor form {
    grid-template-columns: 1fr;
  }
  .residence-grid {
    grid-template-columns: 1fr 1fr;
  }
  .history-sheet {
    grid-column: auto;
  }
}
</style>
