<script setup>
import { computed, onMounted, ref } from 'vue'
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

onMounted(loadProfile)
</script>

<template>
  <div class="profile-page workspace-page">
    <WorkspaceHeader
      eyebrow="STUDENT / DOSSIER"
      title="个人与住宿档案"
      description="个人联系方式由你维护，住宿与床位信息由宿管业务统一同步。"
    >
      <StatusTag
        :label="accommodation ? '当前在住' : '等待同步'"
        :tone="accommodation ? 'success' : 'warning'"
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
  grid-template-columns: minmax(260px, 0.58fr) minmax(0, 1fr);
  margin-top: 27px;
  gap: 18px;
}
.identity-sheet {
  grid-row: 1/3;
  padding: 25px;
  background: var(--color-ink);
  color: var(--color-paper);
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
  color: #d98768;
  font: 8px var(--font-mono);
  letter-spacing: 0.15em;
}
.identity-sheet > header b {
  display: grid;
  width: 42px;
  height: 42px;
  place-items: center;
  border: 1px solid #697b70;
  border-radius: 50%;
  font-family: var(--font-display);
  font-size: 18px;
}
.identity-sheet__name {
  padding: 52px 0 38px;
}
.identity-sheet__name p {
  margin: 0;
  color: #82958a;
  font-size: 9px;
}
.identity-sheet__name h2 {
  margin: 8px 0 3px;
  font-family: var(--font-display);
  font-size: 30px;
  font-weight: 500;
}
.identity-sheet__name small {
  color: #9caf9f;
  font: 9px var(--font-mono);
}
.identity-sheet dl {
  margin: 0;
  border-top: 1px solid #42564a;
}
.identity-sheet dl div {
  display: grid;
  grid-template-columns: 80px 1fr;
  padding: 16px 0;
  border-bottom: 1px solid #364a3f;
}
.identity-sheet dt {
  color: #83958a;
  font-size: 9px;
}
.identity-sheet dd {
  margin: 0;
  font-family: var(--font-display);
  font-size: 12px;
}
.contact-editor,
.residence-sheet,
.history-sheet {
  border: 1px solid var(--color-line-strong);
  background: rgba(250, 246, 237, 0.5);
}
.contact-editor > header,
.residence-sheet > header,
.history-sheet > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  padding: 17px 20px;
  border-bottom: 1px solid var(--color-line);
}
.contact-editor h2,
.residence-sheet h2,
.history-sheet h2 {
  margin: 5px 0 0;
  font-family: var(--font-display);
  font-size: 19px;
  font-weight: 500;
}
.contact-editor header small {
  color: var(--color-text-soft);
  font-size: 8px;
}
.contact-editor form {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  align-items: end;
  padding: 22px;
  gap: 15px;
}
.contact-editor label {
  display: grid;
  gap: 7px;
}
.contact-editor label span {
  color: var(--color-text-muted);
  font-size: 9px;
}
.contact-editor form p {
  grid-column: 1/-1;
  margin: 0;
  color: var(--color-brand);
  font-size: 10px;
}
.residence-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
}
.residence-grid article {
  display: grid;
  min-height: 92px;
  align-content: space-between;
  padding: 17px;
  border-right: 1px solid var(--color-line);
}
.residence-grid span {
  color: var(--color-text-muted);
  font-size: 9px;
}
.residence-grid strong {
  font-family: var(--font-display);
  font-size: 16px;
  font-weight: 500;
}
.residence-sheet footer {
  padding: 12px 17px;
  border-top: 1px solid var(--color-line);
  color: var(--color-text-soft);
  font-size: 8px;
}
.history-sheet {
  grid-column: 1/-1;
}
.history-sheet > article {
  display: grid;
  grid-template-columns: 120px 1fr;
  min-height: 68px;
  align-items: center;
  padding: 12px 20px;
  border-bottom: 1px solid var(--color-line);
  gap: 20px;
}
.history-sheet time {
  color: var(--color-accent-strong);
  font: 9px var(--font-mono);
}
.history-sheet article div {
  display: grid;
  gap: 5px;
}
.history-sheet article b {
  font-family: var(--font-display);
  font-size: 12px;
}
.history-sheet article span {
  color: var(--color-text-muted);
  font-size: 9px;
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
