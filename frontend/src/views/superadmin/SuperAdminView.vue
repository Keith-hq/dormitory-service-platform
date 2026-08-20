<script setup>
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { buildingApi } from '@/api/building'
import { governanceApi } from '@/api/governance'
import { InlineState, MetricStrip, WorkspaceHeader } from '@/components'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'
import { formatLocalMonthInput } from '@/utils/localDate'

const route = useRoute()
const loading = ref(true)
const working = ref(false)
const error = ref('')
const actionError = ref('')
const notice = ref('')
const admins = ref([])
const students = ref([])
const audits = ref([])
const buildings = ref([])
const report = ref(null)
const directory = ref('admins')
const keyword = ref('')
const modalElement = ref(null)
const modal = reactive({ open: false, mode: '', target: null })
const credential = reactive({ title: '', loginName: '', password: '' })
const copyState = ref('')
let modalTrigger = null

const studentForm = reactive({
  studentId: '',
  name: '',
  gender: '',
  majorId: '',
  phone: '',
  email: '',
  loginName: '',
  password: ''
})
const adminForm = reactive({
  adminId: '',
  adminName: '',
  roleLevel: '楼长',
  buildingId: '',
  post: '',
  phone: '',
  password: ''
})
const disableReason = ref('')

const section = computed(() => route.meta.section ?? 'overview')
const title = computed(
  () => ({ overview: '治理总览', people: '人员档案', audit: '审计与报表' })[section.value]
)
const metrics = computed(() => [
  { label: '学生档案', value: students.value.length, hint: '全校范围' },
  { label: '管理人员', value: admins.value.length, hint: '在册账号' },
  { label: '正常账号', value: activeAccountCount.value, hint: '可登录' },
  { label: '审计事件', value: audits.value.length, hint: '最近记录' }
])
const activeAccountCount = computed(
  () => [...admins.value, ...students.value].filter((item) => item.accountStatus === '正常').length
)
const normalizedKeyword = computed(() => keyword.value.trim().toLowerCase())
const filteredAdmins = computed(() =>
  admins.value.filter((item) =>
    [item.adminName, item.adminId, item.roleLevel, item.post, item.buildingName, item.loginName]
      .filter(Boolean)
      .some((value) => String(value).toLowerCase().includes(normalizedKeyword.value))
  )
)
const filteredStudents = computed(() =>
  students.value.filter((item) =>
    [item.name, item.studentId, item.majorName, item.collegeName, item.loginName]
      .filter(Boolean)
      .some((value) => String(value).toLowerCase().includes(normalizedKeyword.value))
  )
)
const modalTitle = computed(
  () =>
    ({
      'student-create': '新增学生与账号',
      'student-edit': '编辑学生档案',
      'admin-create': '新增管理人员',
      'admin-edit': '编辑管理人员',
      disable: '停用登录账号',
      credentials: credential.title
    })[modal.mode] ?? '人员管理'
)
const isStudentModal = computed(() => modal.mode.startsWith('student-'))
const isAdminModal = computed(() => modal.mode.startsWith('admin-'))
const isCreateModal = computed(() => modal.mode.endsWith('-create'))

const display = (row, ...keys) =>
  keys
    .map((key) => row[key])
    .find((value) => value !== undefined && value !== null && value !== '') ?? '—'

const load = async () => {
  loading.value = true
  error.value = ''
  const results = await Promise.allSettled([
    governanceApi.getAdmins(),
    governanceApi.getStudents(),
    governanceApi.getAuditEvents({ page: 1, pageSize: 30 }),
    governanceApi.getReport('occupancy', { yearMonth: formatLocalMonthInput() }),
    buildingApi.getList({ page: 1, pageSize: 100 })
  ])
  const targets = [admins, students, audits]
  results.slice(0, 3).forEach((result, index) => {
    if (result.status === 'fulfilled')
      targets[index].value = normalizeCollection(result.value).items
  })
  if (results[3].status === 'fulfilled') report.value = results[3].value
  if (results[4].status === 'fulfilled')
    buildings.value = normalizeCollection(results[4].value).items
  if (results.every((result) => result.status === 'rejected'))
    error.value = toUserMessage(results[0].reason, '治理数据暂时无法同步')
  loading.value = false
}

const focusModal = () => {
  nextTick(() => {
    modalElement.value?.querySelector('input, select, textarea, button')?.focus()
  })
}
const openModal = (mode, target = null) => {
  modalTrigger = document.activeElement
  actionError.value = ''
  copyState.value = ''
  modal.mode = mode
  modal.target = target
  modal.open = true
  focusModal()
}
const closeModal = () => {
  modal.open = false
  modal.target = null
  credential.password = ''
  actionError.value = ''
  nextTick(() => modalTrigger?.focus?.())
}
const openCreateStudent = () => {
  Object.assign(studentForm, {
    studentId: '',
    name: '',
    gender: '',
    majorId: '',
    phone: '',
    email: '',
    loginName: '',
    password: ''
  })
  openModal('student-create')
}
const openEditStudent = (item) => {
  Object.assign(studentForm, {
    studentId: item.studentId,
    name: item.name ?? '',
    gender: item.gender ?? '',
    majorId: item.majorId ?? '',
    phone: item.phone ?? '',
    email: item.email ?? '',
    loginName: item.loginName ?? '',
    password: ''
  })
  openModal('student-edit', item)
}
const openCreateAdmin = () => {
  Object.assign(adminForm, {
    adminId: '',
    adminName: '',
    roleLevel: '楼长',
    buildingId: '',
    post: '',
    phone: '',
    password: ''
  })
  openModal('admin-create')
}
const openEditAdmin = (item) => {
  Object.assign(adminForm, {
    adminId: item.adminId,
    adminName: item.adminName ?? '',
    roleLevel: item.roleLevel ?? '楼长',
    buildingId: item.buildingId ?? '',
    post: item.post ?? '',
    phone: item.phone ?? '',
    password: ''
  })
  openModal('admin-edit', item)
}
const openDisable = (kind, item) => {
  disableReason.value = ''
  openModal('disable', { kind, item })
}
const numberOrNull = (value) => (value === '' || value === null ? null : Number(value))
const showCredential = (titleText, loginName, password) => {
  credential.title = titleText
  credential.loginName = loginName
  credential.password = password
  modal.mode = 'credentials'
  actionError.value = ''
  focusModal()
}

const submitPerson = async () => {
  working.value = true
  actionError.value = ''
  try {
    if (modal.mode === 'student-create') {
      const studentId = studentForm.studentId.trim()
      const result = await governanceApi.createStudent({
        profile: {
          studentId,
          name: studentForm.name.trim(),
          gender: studentForm.gender || null,
          majorId: numberOrNull(studentForm.majorId),
          phone: studentForm.phone.trim() || null,
          email: studentForm.email.trim() || null
        },
        account: {
          studentId,
          loginName: studentForm.loginName.trim() || studentId,
          password: studentForm.password || null
        }
      })
      await load()
      showCredential('学生账号已创建', result.loginName, result.initialPassword)
    } else if (modal.mode === 'student-edit') {
      await governanceApi.updateStudent(studentForm.studentId, {
        name: studentForm.name.trim(),
        gender: studentForm.gender || null,
        majorId: numberOrNull(studentForm.majorId),
        phone: studentForm.phone.trim() || null,
        email: studentForm.email.trim() || null
      })
      notice.value = `学生 ${studentForm.studentId} 的档案已更新`
      closeModal()
      await load()
    } else if (modal.mode === 'admin-create') {
      const result = await governanceApi.createAdmin({
        adminId: adminForm.adminId.trim(),
        name: adminForm.adminName.trim(),
        role: adminForm.roleLevel,
        buildingId: numberOrNull(adminForm.buildingId),
        post: adminForm.post.trim() || null,
        password: adminForm.password || null
      })
      await load()
      showCredential('管理账号已创建', result.loginName, result.initialPassword)
    } else if (modal.mode === 'admin-edit') {
      await governanceApi.updateAdmin(adminForm.adminId, {
        adminName: adminForm.adminName.trim(),
        phone: adminForm.phone.trim() || null,
        roleLevel: adminForm.roleLevel,
        buildingId: numberOrNull(adminForm.buildingId),
        post: adminForm.post.trim() || null
      })
      notice.value = `管理人员 ${adminForm.adminId} 的档案已更新`
      closeModal()
      await load()
    } else if (modal.mode === 'disable') {
      const { kind, item } = modal.target
      if (kind === 'admin')
        await governanceApi.disableAdmin(item.adminId, disableReason.value.trim())
      else await governanceApi.disableStudent(item.studentId, disableReason.value.trim())
      notice.value = `${display(item, 'adminName', 'name')} 的登录账号已停用`
      closeModal()
      await load()
    }
  } catch (cause) {
    actionError.value = toUserMessage(cause, '操作未完成，请检查填写内容')
  } finally {
    working.value = false
  }
}

const resetPassword = async (kind, item) => {
  const personName = display(item, 'adminName', 'name')
  if (!window.confirm(`确认重置 ${personName} 的登录密码？旧密码将立即失效。`)) return
  working.value = true
  notice.value = ''
  try {
    const result =
      kind === 'admin'
        ? await governanceApi.resetAdminPassword(item.adminId)
        : await governanceApi.resetStudentPassword(item.studentId)
    openModal('credentials', item)
    credential.title = `${personName} 的密码已重置`
    credential.loginName = item.loginName
    credential.password = result.newPassword
  } catch (cause) {
    notice.value = toUserMessage(cause, '密码重置失败')
  } finally {
    working.value = false
  }
}

const copyCredentials = async () => {
  try {
    await navigator.clipboard.writeText(
      `登录名：${credential.loginName}\n临时密码：${credential.password}`
    )
    copyState.value = '已复制，请通过安全渠道交付'
  } catch {
    copyState.value = '复制失败，请手动记录'
  }
}

watch(section, load)
onMounted(load)
</script>

<template>
  <main class="governance-page">
    <WorkspaceHeader
      eyebrow="PLATFORM GOVERNANCE"
      :title="title"
      description="跨楼栋查看人员、权限和关键操作留痕，所有高风险动作均保留审计依据。"
    >
      <button class="btn btn-sm" type="button" :disabled="loading" @click="load">刷新</button>
    </WorkspaceHeader>

    <MetricStrip :metrics="metrics" style="--metric-count: 4" />
    <InlineState :loading="loading" :error="error" />

    <section v-if="!loading && !error && section === 'overview'" class="overview-grid">
      <article>
        <span>01 / OCCUPANCY</span>
        <h2>入住概览</h2>
        <pre>{{ report?.reportData ? '本月入住报表已生成' : '本月暂无入住报表' }}</pre>
      </article>
      <article>
        <span>02 / ROLE DISTRIBUTION</span>
        <h2>管理角色</h2>
        <div v-for="role in ['超级管理员', '楼长', '维修员', '辅导员']" :key="role">
          <strong>{{ role }}</strong
          ><b>{{ admins.filter((item) => item.roleLevel === role).length }}</b>
        </div>
      </article>
    </section>

    <section v-else-if="!loading && !error && section === 'people'" class="people-workspace">
      <header class="people-toolbar">
        <div>
          <p>IDENTITY CONTROL</p>
          <h2>人员与账号</h2>
          <span>档案、角色与登录状态在同一处管理；密码只在创建或重置后展示一次。</span>
        </div>
        <div class="toolbar-actions">
          <button class="btn" type="button" @click="openCreateStudent">新增学生</button>
          <button class="btn btn-primary" type="button" @click="openCreateAdmin">
            新增管理人员
          </button>
        </div>
      </header>

      <div v-if="notice" class="notice" role="status">
        <span>{{ notice }}</span
        ><button type="button" @click="notice = ''">关闭</button>
      </div>

      <div class="directory-controls">
        <div class="directory-tabs" aria-label="人员类型">
          <button
            type="button"
            :class="{ active: directory === 'admins' }"
            @click="directory = 'admins'"
          >
            管理团队 <b>{{ admins.length }}</b>
          </button>
          <button
            type="button"
            :class="{ active: directory === 'students' }"
            @click="directory = 'students'"
          >
            学生名册 <b>{{ students.length }}</b>
          </button>
        </div>
        <label class="directory-search">
          <span>SEARCH</span>
          <input v-model="keyword" type="search" placeholder="姓名、编号、角色或登录名" />
        </label>
      </div>

      <div v-if="directory === 'admins'" class="directory-list admin-directory">
        <article v-for="item in filteredAdmins" :key="item.adminId" class="person-row">
          <div class="person-index">{{ item.adminId }}</div>
          <div class="person-primary">
            <div>
              <h3>{{ display(item, 'adminName', 'name') }}</h3>
              <span class="role-chip">{{ display(item, 'roleLevel') }}</span>
              <span :class="['status-chip', { muted: item.accountStatus !== '正常' }]">
                {{ display(item, 'accountStatus') }}
              </span>
            </div>
            <p>{{ display(item, 'post') }} · {{ display(item, 'buildingName') }}</p>
          </div>
          <dl>
            <div>
              <dt>登录名</dt>
              <dd>{{ display(item, 'loginName') }}</dd>
            </div>
            <div>
              <dt>联系电话</dt>
              <dd>{{ display(item, 'phone') }}</dd>
            </div>
          </dl>
          <div class="row-actions">
            <button type="button" @click="openEditAdmin(item)">编辑</button>
            <button
              type="button"
              :disabled="item.accountStatus !== '正常' || working"
              @click="resetPassword('admin', item)"
            >
              重置密码
            </button>
            <button
              class="danger"
              type="button"
              :disabled="item.accountStatus !== '正常' || working"
              @click="openDisable('admin', item)"
            >
              停用
            </button>
          </div>
        </article>
        <p v-if="!filteredAdmins.length" class="empty">没有符合条件的管理人员</p>
      </div>

      <div v-else class="directory-list student-directory">
        <article v-for="item in filteredStudents" :key="item.studentId" class="person-row">
          <div class="person-index">{{ item.studentId }}</div>
          <div class="person-primary">
            <div>
              <h3>{{ display(item, 'name') }}</h3>
              <span class="role-chip student">学生</span>
              <span :class="['status-chip', { muted: item.accountStatus !== '正常' }]">
                {{ display(item, 'accountStatus') }}
              </span>
            </div>
            <p>{{ display(item, 'collegeName') }} · {{ display(item, 'majorName') }}</p>
          </div>
          <dl>
            <div>
              <dt>登录名</dt>
              <dd>{{ display(item, 'loginName') }}</dd>
            </div>
            <div>
              <dt>联系方式</dt>
              <dd>{{ display(item, 'phone', 'email') }}</dd>
            </div>
          </dl>
          <div class="row-actions">
            <button type="button" @click="openEditStudent(item)">编辑</button>
            <button
              type="button"
              :disabled="item.accountStatus !== '正常' || working"
              @click="resetPassword('student', item)"
            >
              重置密码
            </button>
            <button
              class="danger"
              type="button"
              :disabled="item.accountStatus !== '正常' || working"
              @click="openDisable('student', item)"
            >
              停用
            </button>
          </div>
        </article>
        <p v-if="!filteredStudents.length" class="empty">没有符合条件的学生</p>
      </div>
    </section>

    <section v-else-if="!loading && !error" class="audit-stream">
      <header><span>TIME</span><span>ACTOR</span><span>EVENT</span><span>TARGET</span></header>
      <article v-for="item in audits" :key="item.auditId">
        <time>{{ display(item, 'eventTime') }}</time>
        <strong>{{ display(item, 'actorLoginName', 'actorAccountId') }}</strong>
        <p>{{ display(item, 'eventType') }}</p>
        <small>{{ display(item, 'targetType') }} / {{ display(item, 'targetId') }}</small>
      </article>
      <p v-if="!audits.length" class="empty">暂无审计记录</p>
    </section>

    <Teleport to="body">
      <div v-if="modal.open" class="modal-overlay" @click.self="closeModal">
        <form
          ref="modalElement"
          class="person-modal"
          autocomplete="off"
          role="dialog"
          aria-modal="true"
          aria-labelledby="person-modal-title"
          @submit.prevent="submitPerson"
          @keydown.esc.stop.prevent="closeModal"
        >
          <header>
            <div>
              <p>IDENTITY OPERATION</p>
              <h2 id="person-modal-title">{{ modalTitle }}</h2>
            </div>
            <button class="modal-close" type="button" aria-label="关闭" @click="closeModal">
              ×
            </button>
          </header>

          <div v-if="isStudentModal" class="modal-fields">
            <label>
              <span>学号</span>
              <input
                v-model="studentForm.studentId"
                autocomplete="off"
                name="student-record-id"
                required
                :disabled="!isCreateModal"
              />
            </label>
            <label>
              <span>姓名</span>
              <input
                v-model="studentForm.name"
                autocomplete="off"
                name="student-record-name"
                required
              />
            </label>
            <label>
              <span>性别</span>
              <select v-model="studentForm.gender">
                <option value="">未填写</option>
                <option value="男">男</option>
                <option value="女">女</option>
              </select>
            </label>
            <label>
              <span>专业 ID</span>
              <input
                v-model.number="studentForm.majorId"
                type="number"
                min="1"
                placeholder="可选"
              />
            </label>
            <label>
              <span>手机号</span>
              <input
                v-model="studentForm.phone"
                autocomplete="off"
                name="student-record-phone"
                inputmode="tel"
              />
            </label>
            <label>
              <span>邮箱</span>
              <input
                v-model="studentForm.email"
                autocomplete="off"
                name="student-record-email"
                type="email"
              />
            </label>
            <label v-if="isCreateModal">
              <span>登录名</span>
              <input
                v-model="studentForm.loginName"
                autocomplete="off"
                name="student-new-login"
                :placeholder="studentForm.studentId || '默认使用学号'"
              />
            </label>
            <label v-if="isCreateModal">
              <span>初始密码</span>
              <input
                v-model="studentForm.password"
                autocomplete="new-password"
                name="student-new-password"
                type="password"
                placeholder="留空则安全生成"
              />
            </label>
          </div>

          <div v-else-if="isAdminModal" class="modal-fields">
            <label>
              <span>人员编号</span>
              <input
                v-model="adminForm.adminId"
                autocomplete="off"
                name="admin-record-id"
                required
                :disabled="!isCreateModal"
              />
            </label>
            <label>
              <span>姓名</span>
              <input
                v-model="adminForm.adminName"
                autocomplete="off"
                name="admin-record-name"
                required
              />
            </label>
            <label>
              <span>角色</span>
              <select v-model="adminForm.roleLevel" required>
                <option value="楼长">楼长</option>
                <option value="维修员">维修员</option>
                <option value="辅导员">辅导员</option>
                <option value="超级管理员">超级管理员</option>
              </select>
            </label>
            <label>
              <span>负责楼栋</span>
              <select v-model="adminForm.buildingId">
                <option value="">跨楼栋 / 暂不指定</option>
                <option v-for="item in buildings" :key="item.buildingId" :value="item.buildingId">
                  {{ item.buildingName }}
                </option>
              </select>
            </label>
            <label>
              <span>岗位说明</span>
              <input
                v-model="adminForm.post"
                autocomplete="off"
                name="admin-record-post"
                placeholder="如：夜间值班楼长"
              />
            </label>
            <label v-if="!isCreateModal">
              <span>联系电话</span>
              <input
                v-model="adminForm.phone"
                autocomplete="off"
                name="admin-record-phone"
                inputmode="tel"
              />
            </label>
            <label v-if="isCreateModal" class="full-field">
              <span>初始密码</span>
              <input
                v-model="adminForm.password"
                autocomplete="new-password"
                name="admin-new-password"
                type="password"
                placeholder="留空则安全生成"
              />
            </label>
          </div>

          <div v-else-if="modal.mode === 'disable'" class="confirm-panel">
            <p>
              将停用
              <strong>{{ display(modal.target.item, 'adminName', 'name') }}</strong> 的登录账号，
              人员档案与历史记录仍会保留。
            </p>
            <label>
              <span>停用原因</span>
              <textarea
                v-model="disableReason"
                rows="4"
                required
                placeholder="用于审计追踪，请说明原因"
              />
            </label>
          </div>

          <div v-else class="credential-panel">
            <p>凭据只在本次窗口中展示，关闭后无法再次查看。</p>
            <dl>
              <div>
                <dt>登录名</dt>
                <dd>{{ credential.loginName }}</dd>
              </div>
              <div>
                <dt>临时密码</dt>
                <dd>{{ credential.password }}</dd>
              </div>
            </dl>
            <button class="btn btn-primary" type="button" @click="copyCredentials">
              复制登录凭据
            </button>
            <small role="status">{{ copyState || '请通过安全渠道交付给本人' }}</small>
          </div>

          <p v-if="actionError" class="modal-error" role="alert">{{ actionError }}</p>
          <footer v-if="modal.mode !== 'credentials'">
            <button class="btn" type="button" @click="closeModal">取消</button>
            <button class="btn btn-primary" type="submit" :disabled="working">
              {{ working ? '处理中…' : modal.mode === 'disable' ? '确认停用' : '保存' }}
            </button>
          </footer>
          <footer v-else>
            <button class="btn" type="button" @click="closeModal">我已安全记录</button>
          </footer>
        </form>
      </div>
    </Teleport>
  </main>
</template>

<style scoped>
.governance-page {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.overview-grid {
  display: grid;
  grid-template-columns: 1.2fr 0.8fr;
  gap: 28px;
  margin-top: 30px;
}
.overview-grid article {
  min-height: 300px;
  padding: 30px;
  border: 1px solid var(--color-line-strong);
  background: rgba(255, 255, 255, 0.35);
}
.overview-grid span,
.people-toolbar p {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.13em;
}
.overview-grid h2,
.people-toolbar h2 {
  margin: 10px 0;
  font: 500 28px var(--font-display);
}
.overview-grid pre {
  display: grid;
  min-height: 150px;
  place-items: center;
  background: var(--color-ink);
  color: #d9d1c4;
  font: 11px var(--font-mono);
}
.overview-grid article div {
  display: flex;
  justify-content: space-between;
  padding: 16px 0;
  border-bottom: 1px solid var(--color-line);
}
.overview-grid article div strong {
  font-size: 12px;
}
.overview-grid article div b {
  font: 500 24px var(--font-display);
}
.people-workspace {
  margin-top: 30px;
  border-top: 3px solid var(--color-ink);
}
.people-toolbar {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 30px;
  padding: 26px 0 24px;
}
.people-toolbar p,
.people-toolbar h2 {
  margin-top: 0;
}
.people-toolbar span {
  color: var(--color-text-muted);
  font-size: 10px;
}
.toolbar-actions {
  display: flex;
  gap: 8px;
}
.notice {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-left: 3px solid #326149;
  background: rgba(50, 97, 73, 0.09);
  font-size: 10px;
}
.notice button {
  border: 0;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  font: 9px var(--font-mono);
}
.directory-controls {
  display: grid;
  grid-template-columns: 1fr minmax(260px, 0.45fr);
  border: 1px solid var(--color-line-strong);
  background: rgba(255, 255, 255, 0.32);
}
.directory-tabs {
  display: flex;
}
.directory-tabs button {
  min-width: 180px;
  padding: 17px 20px;
  border: 0;
  border-right: 1px solid var(--color-line-strong);
  background: transparent;
  color: var(--color-text-soft);
  cursor: pointer;
  text-align: left;
  font: 600 10px var(--font-sans);
}
.directory-tabs button.active {
  background: var(--color-ink);
  color: #f6f1e8;
}
.directory-tabs b {
  margin-left: 8px;
  font: 500 16px var(--font-display);
}
.directory-search {
  display: grid;
  grid-template-columns: auto 1fr;
  align-items: center;
  gap: 12px;
  padding: 0 15px;
  border-left: 1px solid var(--color-line-strong);
}
.directory-search span {
  color: var(--color-text-muted);
  font: 8px var(--font-mono);
}
.directory-search input {
  width: 100%;
  padding: 10px 0;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--color-text);
  font-size: 11px;
}
.directory-list {
  border: 1px solid var(--color-line-strong);
  border-top: 0;
}
.person-row {
  display: grid;
  grid-template-columns: 110px minmax(230px, 1fr) minmax(280px, 0.9fr) auto;
  align-items: center;
  gap: 22px;
  min-height: 112px;
  padding: 18px 20px;
  border-bottom: 1px solid var(--color-line);
}
.person-row:last-child {
  border-bottom: 0;
}
.person-index {
  color: var(--color-text-muted);
  font: 9px var(--font-mono);
  word-break: break-all;
}
.person-primary > div {
  display: flex;
  align-items: center;
  gap: 7px;
  flex-wrap: wrap;
}
.person-primary h3 {
  margin: 0 5px 0 0;
  font: 500 20px var(--font-display);
}
.person-primary p {
  margin: 8px 0 0;
  color: var(--color-text-muted);
  font-size: 9px;
}
.role-chip,
.status-chip {
  padding: 4px 7px;
  border: 1px solid rgba(117, 65, 40, 0.35);
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
}
.role-chip.student {
  border-color: rgba(49, 86, 104, 0.35);
  color: #315668;
}
.status-chip {
  border-color: rgba(50, 97, 73, 0.35);
  color: #326149;
}
.status-chip.muted {
  border-color: var(--color-line-strong);
  color: var(--color-text-muted);
}
.person-row dl {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  margin: 0;
}
.person-row dt {
  margin-bottom: 5px;
  color: var(--color-text-muted);
  font: 8px var(--font-mono);
}
.person-row dd {
  margin: 0;
  font-size: 10px;
  overflow-wrap: anywhere;
}
.row-actions {
  display: flex;
  justify-content: flex-end;
  gap: 5px;
}
.row-actions button {
  padding: 7px 8px;
  border: 1px solid var(--color-line-strong);
  background: transparent;
  color: var(--color-text-soft);
  cursor: pointer;
  font-size: 9px;
}
.row-actions button:hover:not(:disabled) {
  border-color: var(--color-ink);
  color: var(--color-ink);
}
.row-actions button.danger:hover:not(:disabled) {
  border-color: #93473d;
  color: #93473d;
}
.row-actions button:disabled {
  cursor: not-allowed;
  opacity: 0.35;
}
.audit-stream {
  margin-top: 30px;
  border-top: 1px solid var(--color-line-strong);
}
.audit-stream header,
.audit-stream article {
  display: grid;
  grid-template-columns: 180px 140px 1fr 180px;
  gap: 18px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--color-line);
}
.audit-stream header {
  color: var(--color-text-soft);
  font: 8px var(--font-mono);
}
.audit-stream time,
.audit-stream small {
  color: var(--color-text-muted);
  font: 9px var(--font-mono);
}
.audit-stream strong,
.audit-stream p {
  margin: 0;
  font-size: 10px;
}
.empty {
  margin: 0;
  padding: 32px;
  color: var(--color-text-muted);
  text-align: center;
  font-size: 10px;
}
.modal-overlay {
  position: fixed;
  z-index: 1000;
  inset: 0;
  display: grid;
  place-items: center;
  padding: 24px;
  background: rgba(22, 21, 19, 0.68);
  backdrop-filter: blur(4px);
}
.person-modal {
  width: min(680px, 100%);
  max-height: min(780px, calc(100vh - 48px));
  overflow: auto;
  border: 1px solid #292722;
  background: #f3eee5;
  box-shadow: 16px 20px 0 rgba(0, 0, 0, 0.18);
}
.person-modal > header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 24px 26px;
  border-bottom: 1px solid var(--color-line-strong);
}
.person-modal header p {
  margin: 0 0 8px;
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.13em;
}
.person-modal h2 {
  margin: 0;
  font: 500 25px var(--font-display);
}
.modal-close {
  border: 0;
  background: transparent;
  cursor: pointer;
  font: 300 28px/1 var(--font-display);
}
.modal-fields {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 18px;
  padding: 26px;
}
.modal-fields label,
.confirm-panel label {
  display: grid;
  gap: 7px;
}
.modal-fields label > span,
.confirm-panel label > span {
  color: var(--color-text-muted);
  font: 8px var(--font-mono);
  letter-spacing: 0.08em;
}
.modal-fields input,
.modal-fields select,
.confirm-panel textarea {
  width: 100%;
  min-height: 42px;
  padding: 10px 12px;
  border: 1px solid var(--color-line-strong);
  border-radius: 0;
  background: rgba(255, 255, 255, 0.45);
  color: var(--color-text);
  font: 11px var(--font-sans);
}
.modal-fields input:disabled {
  color: var(--color-text-muted);
  background: rgba(0, 0, 0, 0.04);
}
.full-field {
  grid-column: 1 / -1;
}
.confirm-panel,
.credential-panel {
  padding: 28px 26px;
}
.confirm-panel > p,
.credential-panel > p {
  margin: 0 0 22px;
  color: var(--color-text-soft);
  font-size: 11px;
  line-height: 1.8;
}
.confirm-panel textarea {
  resize: vertical;
}
.credential-panel dl {
  margin: 0 0 22px;
  border-top: 1px solid var(--color-line-strong);
}
.credential-panel dl div {
  display: grid;
  grid-template-columns: 120px 1fr;
  padding: 16px 0;
  border-bottom: 1px solid var(--color-line);
}
.credential-panel dt {
  color: var(--color-text-muted);
  font: 8px var(--font-mono);
}
.credential-panel dd {
  margin: 0;
  font: 600 15px var(--font-mono);
  letter-spacing: 0.04em;
  overflow-wrap: anywhere;
}
.credential-panel small {
  display: block;
  margin-top: 12px;
  color: var(--color-text-muted);
  font-size: 9px;
}
.modal-error {
  margin: 0 26px 18px;
  padding: 10px 12px;
  border-left: 3px solid #93473d;
  background: rgba(147, 71, 61, 0.08);
  color: #7f342d;
  font-size: 10px;
}
.person-modal > footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 18px 26px;
  border-top: 1px solid var(--color-line-strong);
}
@media (max-width: 1050px) {
  .person-row {
    grid-template-columns: 90px 1fr auto;
  }
  .person-row dl {
    grid-column: 2 / 3;
  }
  .row-actions {
    grid-column: 3;
    grid-row: 1 / span 2;
    flex-direction: column;
  }
}
@media (max-width: 800px) {
  .overview-grid {
    grid-template-columns: 1fr;
  }
  .people-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }
  .directory-controls {
    grid-template-columns: 1fr;
  }
  .directory-tabs button {
    min-width: 0;
    flex: 1;
  }
  .directory-search {
    min-height: 50px;
    border-top: 1px solid var(--color-line-strong);
    border-left: 0;
  }
  .person-row {
    grid-template-columns: 1fr;
    gap: 13px;
  }
  .person-row dl,
  .row-actions {
    grid-column: 1;
    grid-row: auto;
  }
  .row-actions {
    flex-direction: row;
    justify-content: flex-start;
  }
  .audit-stream header {
    display: none;
  }
  .audit-stream article {
    grid-template-columns: 1fr;
  }
  .modal-fields {
    grid-template-columns: 1fr;
  }
  .full-field {
    grid-column: auto;
  }
}
</style>

<style scoped>
/* Superadmin workspace redesign: align the page with the shared blue-white functional views. */
.governance-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 76px;
}

.overview-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.15fr) minmax(320px, 0.85fr);
  gap: 22px;
  margin-top: 28px;
}

.overview-grid article {
  min-width: 0;
  min-height: 280px;
  padding: 28px 30px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}

.overview-grid span,
.people-toolbar p {
  display: block;
  margin: 0;
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  line-height: 1.4;
  letter-spacing: 0;
}

.overview-grid h2,
.people-toolbar h2 {
  margin: 8px 0 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 28px;
  font-weight: 900;
  line-height: 1.2;
}

.overview-grid pre {
  display: grid;
  min-height: 146px;
  margin: 28px 0 0;
  place-items: center;
  overflow: hidden;
  padding: 22px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 750;
  line-height: 1.6;
  white-space: pre-wrap;
}

.overview-grid article div {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-top: 12px;
  padding: 15px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
}

.overview-grid article div:first-of-type {
  margin-top: 28px;
}

.overview-grid article div strong {
  color: var(--color-ink);
  font-size: 15px;
  font-weight: 800;
}

.overview-grid article div b {
  color: var(--color-brand);
  font-family: var(--font-display);
  font-size: 24px;
  font-weight: 900;
  line-height: 1;
}

.people-workspace {
  margin-top: 28px;
  overflow: hidden;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}

.people-toolbar {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 24px;
  padding: 28px 30px 22px;
}

.people-toolbar h2 {
  margin-bottom: 8px;
}

.people-toolbar > div:first-child > span {
  display: block;
  max-width: 680px;
  color: var(--color-text-muted);
  font-size: 14px;
  line-height: 1.7;
}

.toolbar-actions {
  display: flex;
  flex: 0 0 auto;
  gap: 10px;
}

.notice {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin: 0 30px 16px;
  padding: 12px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-size: 14px;
  font-weight: 700;
}

.notice button {
  border: 0;
  background: transparent;
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 13px;
  font-weight: 800;
  cursor: pointer;
}

.directory-controls {
  display: grid;
  grid-template-columns: auto minmax(260px, 1fr);
  gap: 12px;
  align-items: center;
  margin: 0 30px;
  padding: 10px;
  border-radius: var(--radius-lg);
  background: var(--color-surface-muted);
}

.directory-tabs {
  display: flex;
  gap: 6px;
}

.directory-tabs button {
  min-height: 42px;
  padding: 8px 14px;
  border: 0;
  border-radius: var(--radius-lg);
  background: transparent;
  color: var(--color-text-muted);
  font-family: var(--font-body);
  font-size: 14px;
  font-weight: 850;
  cursor: pointer;
}

.directory-tabs button.active {
  background: var(--color-brand-soft);
  color: var(--color-brand);
}

.directory-tabs b {
  margin-left: 6px;
  color: currentColor;
  font-family: var(--font-display);
  font-size: 17px;
  font-weight: 900;
}

.directory-search {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  gap: 12px;
  align-items: center;
  min-height: 42px;
  padding: 0 14px;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  background: #fff;
}

.directory-search span {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 13px;
  font-weight: 850;
  letter-spacing: 0;
}

.directory-search input {
  width: 100%;
  min-width: 0;
  min-height: 40px;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--color-ink);
  font-family: var(--font-body);
  font-size: 14px;
}

.directory-search input::placeholder {
  color: var(--color-text-soft);
}

.directory-list {
  display: grid;
  gap: 12px;
  margin-top: 12px;
  padding: 0 30px 30px;
}

.person-row {
  display: grid;
  grid-template-columns: minmax(120px, 0.7fr) minmax(220px, 1.25fr) minmax(260px, 1fr) auto;
  align-items: center;
  gap: 22px;
  min-width: 0;
  padding: 18px 20px;
  border-radius: var(--radius-lg);
  background: #f6f9fe;
  transition:
    background 0.18s ease,
    box-shadow 0.18s ease,
    transform 0.18s ease;
}

.person-row:hover {
  background: var(--color-brand-soft);
  box-shadow: 0 12px 26px rgba(11, 99, 199, 0.1);
  transform: translateY(-1px);
}

.person-index {
  overflow-wrap: anywhere;
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 14px;
  font-weight: 850;
  line-height: 1.5;
}

.person-primary {
  min-width: 0;
}

.person-primary > div {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.person-primary h3 {
  margin: 0 6px 0 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 900;
  line-height: 1.25;
}

.person-primary p {
  margin: 8px 0 0;
  overflow-wrap: anywhere;
  color: var(--color-text-muted);
  font-size: 14px;
  line-height: 1.6;
}

.role-chip,
.status-chip {
  display: inline-flex;
  align-items: center;
  min-height: 28px;
  padding: 5px 9px;
  border: 1px solid var(--color-brand-border);
  border-radius: 999px;
  background: #fff;
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 12px;
  font-weight: 800;
  line-height: 1;
}

.role-chip.student {
  border-color: #b8d7e8;
  color: #276582;
}

.status-chip {
  border-color: #b4e6e1;
  background: #e4f8f6;
  color: #007c73;
}

.status-chip.muted {
  border-color: var(--color-line-strong);
  background: #fff;
  color: var(--color-text-muted);
}

.person-row dl {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  min-width: 0;
  margin: 0;
}

.person-row dt {
  margin-bottom: 5px;
  color: var(--color-text-muted);
  font-size: 12px;
  font-weight: 700;
}

.person-row dd {
  margin: 0;
  overflow-wrap: anywhere;
  color: var(--color-ink);
  font-size: 14px;
  font-weight: 800;
  line-height: 1.5;
}

.row-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  flex-wrap: wrap;
}

.row-actions button {
  min-height: 36px;
  padding: 7px 12px;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  background: #fff;
  color: var(--color-text-muted);
  font-family: var(--font-body);
  font-size: 13px;
  font-weight: 800;
  cursor: pointer;
}

.row-actions button:hover:not(:disabled) {
  border-color: var(--color-brand-border);
  color: var(--color-brand);
  box-shadow: 0 8px 18px rgba(11, 99, 199, 0.08);
}

.row-actions button.danger {
  color: var(--color-danger);
}

.row-actions button.danger:hover:not(:disabled) {
  border-color: #efc4bc;
  background: var(--color-danger-soft);
  color: #a34239;
}

.row-actions button:disabled {
  cursor: not-allowed;
  opacity: 0.4;
}

.audit-stream {
  display: grid;
  gap: 10px;
  margin-top: 28px;
  padding: 22px 30px 30px;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}

.audit-stream header,
.audit-stream article {
  display: grid;
  grid-template-columns: minmax(170px, 0.8fr) minmax(140px, 0.7fr) minmax(220px, 1.5fr) minmax(150px, 0.8fr);
  gap: 18px;
  align-items: center;
  min-width: 0;
}

.audit-stream header {
  padding: 0 16px 8px;
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 13px;
  font-weight: 850;
  letter-spacing: 0;
}

.audit-stream article {
  padding: 16px;
  border-radius: var(--radius-lg);
  background: #f6f9fe;
}

.audit-stream time,
.audit-stream small {
  overflow-wrap: anywhere;
  color: var(--color-text-muted);
  font-family: var(--font-body);
  font-size: 13px;
  line-height: 1.5;
}

.audit-stream strong,
.audit-stream p {
  margin: 0;
  overflow-wrap: anywhere;
  color: var(--color-ink);
  font-size: 14px;
  font-weight: 800;
  line-height: 1.5;
}

.empty {
  margin: 0;
  padding: 30px 16px;
  color: var(--color-text-muted);
  text-align: center;
  font-size: 14px;
}

.modal-overlay {
  position: fixed;
  z-index: 1000;
  inset: 0;
  display: grid;
  place-items: center;
  padding: 24px;
  background: rgba(7, 27, 58, 0.58);
  backdrop-filter: blur(5px);
}

.person-modal {
  width: min(720px, 100%);
  max-height: min(800px, calc(100vh - 48px));
  overflow: auto;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: 0 28px 72px rgba(7, 27, 58, 0.22);
}

.person-modal > header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  padding: 24px 28px 18px;
}

.person-modal header p {
  margin: 0 0 8px;
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}

.person-modal h2 {
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 26px;
  font-weight: 900;
  line-height: 1.2;
}

.modal-close {
  display: grid;
  width: 36px;
  height: 36px;
  place-items: center;
  border: 0;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 24px;
  line-height: 1;
  cursor: pointer;
}

.modal-fields {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
  padding: 18px 28px 26px;
}

.modal-fields label,
.confirm-panel label {
  display: grid;
  gap: 7px;
  min-width: 0;
}

.modal-fields label > span,
.confirm-panel label > span {
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 700;
}

.modal-fields input,
.modal-fields select,
.confirm-panel textarea {
  width: 100%;
  min-height: 44px;
  min-width: 0;
  padding: 10px 12px;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-ink);
  font-family: var(--font-body);
  font-size: 14px;
}

.modal-fields input:focus-visible,
.modal-fields select:focus-visible,
.confirm-panel textarea:focus-visible {
  outline: 3px solid var(--color-focus);
  outline-offset: 1px;
}

.modal-fields input:disabled {
  color: var(--color-text-muted);
  background: var(--color-surface-muted);
}

.full-field {
  grid-column: 1 / -1;
}

.confirm-panel,
.credential-panel {
  padding: 18px 28px 26px;
}

.confirm-panel > p,
.credential-panel > p {
  margin: 0 0 18px;
  color: var(--color-text-muted);
  font-size: 14px;
  line-height: 1.8;
}

.confirm-panel textarea {
  min-height: 120px;
  resize: vertical;
}

.credential-panel dl {
  display: grid;
  gap: 10px;
  margin: 0 0 18px;
}

.credential-panel dl div {
  display: grid;
  grid-template-columns: 120px minmax(0, 1fr);
  gap: 16px;
  padding: 14px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
}

.credential-panel dt {
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 700;
}

.credential-panel dd {
  margin: 0;
  overflow-wrap: anywhere;
  color: var(--color-ink);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
}

.credential-panel small {
  display: block;
  margin-top: 12px;
  color: var(--color-text-muted);
  font-size: 13px;
}

.modal-error {
  margin: 0 28px 18px;
  padding: 12px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-danger-soft);
  color: var(--color-danger);
  font-size: 13px;
  line-height: 1.6;
}

.person-modal > footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 18px 28px;
  background: var(--color-surface-muted);
}

@media (max-width: 1120px) {
  .person-row {
    grid-template-columns: minmax(120px, 0.7fr) minmax(220px, 1fr) auto;
  }

  .person-row dl {
    grid-column: 2 / 3;
  }

  .row-actions {
    grid-column: 3;
    grid-row: 1 / span 2;
    flex-direction: column;
    align-items: stretch;
  }
}

@media (max-width: 900px) {
  .overview-grid {
    grid-template-columns: 1fr;
  }

  .people-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }

  .toolbar-actions {
    width: 100%;
  }

  .toolbar-actions .btn {
    flex: 1;
  }

  .directory-controls {
    grid-template-columns: 1fr;
  }

  .directory-search {
    min-height: 48px;
  }

  .person-row {
    grid-template-columns: 1fr;
    gap: 14px;
  }

  .person-row dl,
  .row-actions {
    grid-column: 1;
    grid-row: auto;
  }

  .row-actions {
    flex-direction: row;
    justify-content: flex-start;
  }

  .audit-stream header {
    display: none;
  }

  .audit-stream article {
    grid-template-columns: 1fr 1fr;
  }
}

@media (max-width: 640px) {
  .governance-page {
    width: min(100% - 32px, var(--content-max));
  }

  .overview-grid article,
  .people-toolbar,
  .audit-stream {
    padding-left: 20px;
    padding-right: 20px;
  }

  .notice,
  .directory-controls {
    margin-left: 20px;
    margin-right: 20px;
  }

  .directory-list {
    padding-right: 20px;
    padding-left: 20px;
  }

  .directory-tabs {
    display: grid;
    grid-template-columns: 1fr 1fr;
  }

  .directory-tabs button {
    width: 100%;
  }

  .person-row dl,
  .audit-stream article {
    grid-template-columns: 1fr;
  }

  .row-actions {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
  }

  .row-actions button {
    width: 100%;
    padding-right: 8px;
    padding-left: 8px;
  }

  .modal-overlay {
    padding: 12px;
  }

  .person-modal > header,
  .modal-fields,
  .confirm-panel,
  .credential-panel,
  .person-modal > footer {
    padding-right: 20px;
    padding-left: 20px;
  }

  .modal-fields {
    grid-template-columns: 1fr;
  }

  .full-field {
    grid-column: auto;
  }
}
</style>
