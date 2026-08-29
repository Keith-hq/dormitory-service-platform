<script setup>
import { computed, nextTick, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const userStore = useUserStore()
const loading = ref(true)
const submitting = ref(false)
const error = ref('')
const feedback = ref('')
const tickets = ref([])
const description = ref('')
const urgency = ref('普通')
const activeTicket = ref(null)
const descriptionInput = ref(null)
// 「发起报修」：预填引导文案并把焦点移到描述框，让点击有明确反馈
const startNewRepair = async () => {
  if (!description.value.trim()) description.value = '请描述具体位置和故障现象'
  feedback.value = ''
  await nextTick()
  descriptionInput.value?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  descriptionInput.value?.focus()
  descriptionInput.value?.select()
}
const studentId = computed(() => userStore.userInfo?.id || '')
const openTickets = computed(() =>
  tickets.value.filter(
    (item) => !['已完成', '已撤销', 'completed', 'cancelled'].includes(item.status)
  )
)

const loadTickets = async () => {
  loading.value = true
  error.value = ''
  try {
    tickets.value = normalizeCollection(await studentApi.getRepairTickets(studentId.value)).items
    const previousId = activeTicket.value?.ticketId
    // 刷新后按 ticketId 重新取最新对象，否则时间线停留旧状态（如完工后仍显示已派单）
    activeTicket.value =
      tickets.value.find((ticket) => ticket.ticketId === previousId) || tickets.value[0] || null
  } catch (requestError) {
    error.value = toUserMessage(requestError, '报修记录暂时无法同步')
  } finally {
    loading.value = false
  }
}
const submitRepair = async () => {
  if (!description.value.trim()) return
  submitting.value = true
  feedback.value = ''
  try {
    const created = await studentApi.createRepairTicket({
      description: description.value.trim(),
      urgency: urgency.value
    })
    // 提交时选好的图片，工单创建成功后一并上传
    if (pendingFiles.value.length && created?.ticketId) {
      const form = new FormData()
      pendingFiles.value.forEach((file) => form.append('files', file))
      await studentApi.addRepairAttachments(created.ticketId, form)
    }
    description.value = ''
    pendingFiles.value = []
    // 清空 file input 元素值，避免选择文件处残留上一次的图片
    if (pendingInput.value) pendingInput.value.value = ''
    feedback.value = '报修已提交，系统将自动进入派单队列'
    await loadTickets()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '提交失败，请稍后重试')
  } finally {
    submitting.value = false
  }
}
// —— 撤销工单（仅待处理/已派单且提交后 10 分钟内可撤，后端兜底校验）——
const cancelling = ref(false)
const canCancel = (ticket) =>
  ['待处理', '已派单'].includes(ticket?.status) &&
  !!ticket?.submitTime &&
  Date.now() - new Date(ticket.submitTime).getTime() <= 10 * 60 * 1000
const cancelActive = async () => {
  if (!activeTicket.value) return
  cancelling.value = true
  feedback.value = ''
  try {
    await studentApi.cancelRepairTicket(activeTicket.value.ticketId)
    feedback.value = '工单已撤销'
    await loadTickets()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '撤销失败：仅待处理/已派单且 10 分钟内可撤')
  } finally {
    cancelling.value = false
  }
}
// —— 报修图片附件（提交时可选；对已有工单可补传）——
const pendingFiles = ref([])
const activeFiles = ref([])
const uploading = ref(false)
const pendingInput = ref(null)
const activeInput = ref(null)
const uploadActive = async () => {
  const ticket = activeTicket.value
  if (!ticket || !activeFiles.value.length) return
  const form = new FormData()
  activeFiles.value.forEach((file) => form.append('files', file))
  uploading.value = true
  feedback.value = ''
  try {
    await studentApi.addRepairAttachments(ticket.ticketId, form)
    feedback.value = '图片已上传'
    activeFiles.value = []
    if (activeInput.value) activeInput.value.value = ''
    await loadTickets()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '上传失败')
  } finally {
    uploading.value = false
  }
}
const tone = (status) =>
  ['已完成', 'completed'].includes(status)
    ? 'success'
    : ['已撤销', 'cancelled'].includes(status)
      ? 'neutral'
      : 'warning'
// 工单提交时间：后端字段是 submitTime，格式化为 YYYY-MM-DD HH:mm
const formatTicketTime = (value) => {
  if (!value) return '时间待同步'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '时间待同步'
  const pad = (n) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}
// —— 图片查看器：从列表打开工单图片的大图弹窗 ——
const viewer = ref({ open: false, images: [], index: 0 })
const openViewer = (ticket) => {
  const images = (ticket.attachments || []).map((att) => ({
    src: `/uploads/${att.storageRef}`,
    name: att.originalName
  }))
  if (!images.length) return
  viewer.value = { open: true, images, index: 0 }
}
const closeViewer = () => {
  viewer.value.open = false
}
const viewerPrev = () => {
  if (viewer.value.images.length > 1)
    viewer.value.index =
      (viewer.value.index + viewer.value.images.length - 1) % viewer.value.images.length
}
const viewerNext = () => {
  if (viewer.value.images.length > 1)
    viewer.value.index = (viewer.value.index + 1) % viewer.value.images.length
}
onMounted(loadTickets)
</script>

<template>
  <div class="repair-page workspace-page">
    <WorkspaceHeader
      eyebrow="STUDENT / REPAIR"
      title="报修与处理进度"
      description="从问题描述到维修结果，所有状态变化集中在一条时间线上。"
    >
      <button class="btn btn-primary" type="button" @click="startNewRepair">发起报修</button>
    </WorkspaceHeader>
    <section class="repair-summary">
      <article>
        <span>进行中</span><strong>{{ openTickets.length }}</strong
        ><small>等待处理或维修中</small>
      </article>
      <article>
        <span>全部记录</span><strong>{{ tickets.length }}</strong
        ><small>含完成与撤销</small>
      </article>
      <article><span>处理承诺</span><strong>4h</strong><small>普通工单目标响应</small></article>
    </section>
    <div class="repair-layout">
      <section class="repair-compose">
        <header>
          <span>NEW REQUEST</span>
          <h2>描述宿舍问题</h2>
          <p>尽量写清房间位置、故障现象和影响范围。</p>
        </header>
        <form @submit.prevent="submitRepair">
          <label
            >紧急程度<select v-model="urgency" class="form-select">
              <option>普通</option>
              <option>紧急</option>
            </select></label
          ><label
            >问题描述<textarea
              ref="descriptionInput"
              v-model="description"
              class="form-input"
              rows="6"
              placeholder="例如：书桌右侧插座松动，使用时有火花…"
            ></textarea>
          </label>
          <label
            >图片附件（可选，jpg/png，单张 ≤5MB）
            <input
              ref="pendingInput"
              type="file"
              accept="image/*"
              multiple
              @change="pendingFiles = [...($event.target.files || [])]"
            />
            <small v-if="pendingFiles.length">已选 {{ pendingFiles.length }} 张</small></label
          >
          <p v-if="feedback" role="status">{{ feedback }}</p>
          <button class="btn btn-primary" :disabled="submitting || !description.trim()">
            {{ submitting ? '提交中…' : '提交报修' }}
          </button>
        </form>
      </section>
      <section class="ticket-queue">
        <header>
          <div>
            <span>MY TICKETS / {{ tickets.length }}</span>
            <h2>我的报修单</h2>
          </div>
          <button class="btn btn-sm" @click="loadTickets">刷新</button>
        </header>
        <InlineState
          :loading="loading"
          :error="error"
          :empty="!loading && !tickets.length"
          empty-text="暂无报修记录"
        /><button
          v-for="ticket in tickets"
          :key="ticket.ticketId"
          class="ticket-row"
          :class="{ active: activeTicket?.ticketId === ticket.ticketId }"
          @click="activeTicket = ticket"
        >
          <time>#{{ ticket.ticketId }}</time>
          <div>
            <b>{{ ticket.issueDesc || ticket.description || '报修事项' }}</b
            ><small>{{ formatTicketTime(ticket.submitTime) }}</small>
            <span
              v-if="ticket.attachments?.length"
              class="ticket-view"
              title="查看报修图片"
              @click.stop="openViewer(ticket)"
            >
              查看图片
            </span>
          </div>
          <StatusTag :label="ticket.status || '待处理'" :tone="tone(ticket.status)" size="small" />
        </button>
      </section>
      <aside class="repair-timeline">
        <header>
          <span>PROCESS / LIVE</span>
          <h2>处理时间线</h2>
        </header>
        <template v-if="activeTicket"
          ><div class="timeline-step done">
            <i></i><b>报修已提交</b><span>系统记录问题并生成工单</span>
          </div>
          <div class="timeline-step" :class="{ done: activeTicket.status !== '待处理' }">
            <i></i><b>等待派单</b><span>根据区域与负载匹配维修员</span>
          </div>
          <div class="timeline-step" :class="{ done: !!activeTicket.claimTime }">
            <i></i><b>已接收</b
            ><span v-if="activeTicket.claimTime"
              >维修员接单 · {{ formatTicketTime(activeTicket.claimTime) }}</span
            ><span v-else>等待维修员接单</span>
          </div>
          <div
            class="timeline-step"
            :class="{ done: ['已完成', 'completed'].includes(activeTicket.status) }"
          >
            <i></i><b>维修处理</b><span>维修员更新过程与材料记录</span>
            <div v-if="activeTicket.log" class="timeline-log">
              <p class="log-desc">{{ activeTicket.log.processDescription }}</p>
              <p v-if="activeTicket.log.repairResult" class="log-result">
                维修结果：{{ activeTicket.log.repairResult }}
              </p>
              <span class="log-meta"
                >维修员 {{ activeTicket.log.adminId || '—' }} ·
                {{ formatTicketTime(activeTicket.log.resolveTime) }}</span
              >
            </div>
          </div>
          <div class="timeline-step" :class="{ done: !!activeTicket.log }">
            <i></i><b>结果归档</b><span>完成后可回看维修结果</span>
          </div>
          <div class="ticket-tools">
            <div v-if="activeTicket.attachments?.length" class="attachments">
              <span>已传附件</span>
              <a
                v-for="att in activeTicket.attachments"
                :key="att.attachmentId"
                :href="`/uploads/${att.storageRef}`"
                target="_blank"
                rel="noopener"
                >{{ att.originalName }}</a
              >
            </div>
            <div class="ticket-tools__row">
              <label class="btn btn-sm">
                上传图片
                <input
                  ref="activeInput"
                  type="file"
                  accept="image/*"
                  multiple
                  hidden
                  @change="activeFiles = [...($event.target.files || [])]"
                />
              </label>
              <button
                class="btn btn-sm btn-primary"
                :disabled="uploading || !activeFiles.length"
                @click="uploadActive"
              >
                {{ uploading ? '上传中…' : '确认上传' }}
              </button>
              <button
                v-if="canCancel(activeTicket)"
                class="btn btn-sm"
                :disabled="cancelling"
                @click="cancelActive"
              >
                {{ cancelling ? '撤销中…' : '撤销工单' }}
              </button>
            </div>
          </div></template
        ><InlineState v-else empty empty-text="选择一张工单查看处理过程" />
      </aside>
    </div>

    <div
      v-if="viewer.open"
      class="image-viewer"
      role="dialog"
      aria-modal="true"
      @click.self="closeViewer"
    >
      <button class="viewer-close" type="button" aria-label="关闭" @click="closeViewer">×</button>
      <figure class="viewer-body">
        <img :src="viewer.images[viewer.index]?.src" :alt="viewer.images[viewer.index]?.name" />
        <figcaption v-if="viewer.images.length > 1" class="viewer-caption">
          <button type="button" :disabled="viewer.images.length < 2" @click="viewerPrev">
            ‹ 上一张
          </button>
          <span>{{ viewer.index + 1 }} / {{ viewer.images.length }}</span>
          <button type="button" :disabled="viewer.images.length < 2" @click="viewerNext">
            下一张 ›
          </button>
        </figcaption>
      </figure>
    </div>
  </div>
</template>

<style scoped>
.workspace-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.repair-summary {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  margin: 27px 0;
  border-block: 1px solid var(--color-line-strong);
}
.repair-summary article {
  display: grid;
  grid-template-columns: 1fr auto;
  padding: 17px 20px;
  border-right: 1px solid var(--color-line);
  gap: 5px;
}
.repair-summary span {
  grid-column: 1/-1;
  color: var(--color-text-muted);
  font-size: 9px;
}
.repair-summary strong {
  font-family: var(--font-display);
  font-size: 27px;
  font-weight: 500;
}
.repair-summary small {
  align-self: end;
  color: var(--color-text-soft);
  font-size: 8px;
}
.repair-layout {
  display: grid;
  grid-template-columns: minmax(260px, 0.58fr) minmax(0, 1fr) minmax(240px, 0.55fr);
  gap: 14px;
}
.repair-compose,
.ticket-queue,
.repair-timeline {
  background: #fff;
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1);
}
.repair-compose {
  padding: 30px;
  min-width: 0; /* 允许网格项随窗口收缩，不被内容最小宽撑开 */
}
.repair-compose header > span,
.ticket-queue header span,
.repair-timeline header span {
  color: var(--color-brand);
  font: inherit;
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.repair-compose h2,
.ticket-queue h2,
.repair-timeline h2 {
  margin: 6px 0;
  font-family: var(--font-display);
  font-size: 28px;
  font-weight: 950;
}
.repair-compose header p {
  color: var(--color-text-muted);
  font-size: 16px;
  line-height: 1.8;
}
.repair-compose form {
  display: grid;
  grid-template-columns: minmax(0, 1fr); /* 表单列可收缩并填满容器 */
  margin-top: 21px;
  gap: 14px;
  min-width: 0;
}
.repair-compose label {
  display: grid;
  grid-template-columns: minmax(0, 1fr); /* label 填满并可收缩 */
  color: var(--color-text-muted);
  font-size: 16px;
  gap: 10px;
  min-width: 0;
}
.repair-compose :where(input, select, textarea) {
  width: 100%;
  max-width: 100%;
}
.repair-compose textarea {
  resize: vertical;
}
.repair-compose input[type='file'] {
  min-width: 0;
  font-size: 13px;
}
.repair-compose form button {
  justify-self: start; /* 按钮保持内容宽度，不拉伸占满整行 */
}
.repair-compose form p {
  margin: 0;
  color: var(--color-brand);
  font-size: 15px;
}
.ticket-queue > header,
.repair-timeline > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  min-height: 92px;
  padding: 28px 30px 12px;
  border-bottom: 0;
}
.ticket-row {
  display: grid;
  width: calc(100% - 48px);
  grid-template-columns: 58px 1fr auto;
  align-items: center;
  min-height: 92px;
  margin: 14px 24px;
  padding: 18px 20px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  text-align: left;
  gap: 16px;
  cursor: pointer;
}
.ticket-row:hover,
.ticket-row.active {
  background: var(--color-brand-soft);
}
.ticket-row.active {
  box-shadow: 0 12px 24px rgba(11, 99, 199, 0.12);
}
.ticket-row time {
  color: var(--color-brand);
  font: inherit;
  font-size: 15px;
  font-weight: 850;
}
.ticket-row div {
  display: grid;
  gap: 5px;
  min-width: 0; /* 允许收缩，长描述换行而非溢出 */
}
.ticket-row b {
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 900;
  overflow-wrap: anywhere;
}
.ticket-row small {
  color: var(--color-text-muted);
  font-size: 14px;
  line-height: 1.6;
  overflow-wrap: anywhere;
}
.ticket-view {
  display: inline-block;
  margin-top: 6px;
  padding: 4px 12px;
  border: 1px solid var(--color-brand-border);
  border-radius: 6px;
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
}
.ticket-view:hover {
  background: var(--color-brand);
  color: #fff;
}
.image-viewer {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: grid;
  place-items: center;
  background: rgba(13, 22, 33, 0.82);
}
.viewer-close {
  position: absolute;
  top: 18px;
  right: 22px;
  width: 40px;
  height: 40px;
  border: 0;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.15);
  color: #fff;
  font-size: 22px;
  line-height: 1;
  cursor: pointer;
}
.viewer-close:hover {
  background: rgba(255, 255, 255, 0.3);
}
.viewer-body {
  display: grid;
  place-items: center;
  width: min(88vw, 900px);
  height: min(78vh, 620px); /* 统一弹窗尺寸，不随图片大小变化 */
  margin: 0;
  padding: 18px;
  border-radius: 10px;
  background: rgba(255, 255, 255, 0.08);
  overflow: hidden;
}
.viewer-body img {
  display: block;
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
  border-radius: 6px;
  background: #fff;
}
.viewer-caption {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  margin-top: 12px;
  color: #fff;
  font-size: 13px;
}
.viewer-caption button {
  padding: 4px 12px;
  border: 1px solid rgba(255, 255, 255, 0.4);
  border-radius: 6px;
  background: transparent;
  color: #fff;
  cursor: pointer;
}
.viewer-caption button:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.15);
}
.viewer-caption button:disabled {
  opacity: 0.4;
  cursor: default;
}
.repair-timeline {
  padding-bottom: 18px;
  background: #fff;
  color: var(--color-text);
  min-width: 0; /* 允许收缩，长内容换行而非溢出 */
}
.repair-timeline > header {
  border-color: transparent;
}
.timeline-step {
  position: relative;
  display: grid;
  margin-left: 28px;
  padding: 18px 18px 5px 28px;
  color: var(--color-text-muted);
}
.timeline-step::before {
  position: absolute;
  top: 29px;
  bottom: -23px;
  left: 3px;
  width: 1px;
  background: #41554a;
  content: '';
}
.timeline-step:last-of-type::before {
  display: none;
}
.timeline-step i {
  position: absolute;
  top: 23px;
  left: -1px;
  width: 9px;
  height: 9px;
  border: 1px solid var(--color-brand-border);
  border-radius: 50%;
  background: #fff;
}
.timeline-step.done {
  color: var(--color-ink);
}
.timeline-step.done i {
  border-color: var(--color-brand);
  background: var(--color-brand);
}
.timeline-step b {
  font-family: var(--font-display);
  font-size: 12px;
}
.timeline-step span {
  margin-top: 5px;
  color: #819388;
  font-size: 8px;
  line-height: 1.5;
}
.timeline-log {
  margin-top: 8px;
  padding: 10px 12px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
}
.timeline-log .log-desc {
  margin: 0;
  color: var(--color-ink);
  font-size: 12px;
  line-height: 1.6;
  overflow-wrap: anywhere;
}
.timeline-log .log-result {
  margin: 6px 0 0;
  color: var(--color-brand-strong);
  font-weight: 700;
  font-size: 12px;
}
.timeline-log .log-meta {
  display: block;
  margin-top: 6px;
  color: var(--color-text-soft);
  font-size: 10px;
}
.ticket-tools {
  margin: 6px 28px 0;
  padding: 14px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
}
.ticket-tools__row {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.ticket-tools__row input {
  display: none;
}
.attachments {
  display: grid;
  gap: 6px;
  margin-bottom: 12px;
  min-width: 0; /* 允许收缩，长文件名换行 */
}
.attachments span {
  color: var(--color-brand-strong);
  font: 800 11px var(--font-mono);
  letter-spacing: 0.1em;
}
.attachments a {
  color: var(--color-brand);
  font-size: 13px;
  font-weight: 700;
  text-decoration: none;
  overflow-wrap: anywhere; /* 长文件名换行而非溢出 */
}
@media (max-width: 1000px) {
  .repair-layout {
    grid-template-columns: 1fr 1fr;
  }
  .repair-timeline {
    grid-column: 1/-1;
  }
}
@media (max-width: 720px) {
  .workspace-page {
    width: min(100% - 32px, var(--content-max));
  }
  .repair-summary,
  .repair-layout {
    grid-template-columns: 1fr;
  }
  .repair-summary article {
    border-right: 0;
    border-bottom: 1px solid var(--color-line);
  }
  .repair-timeline {
    grid-column: auto;
  }
}
</style>
