<script setup>
import { onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const userStore = useUserStore()
const studentId = ref(userStore.userInfo?.id || '')
const roomId = ref('')
const reason = ref('')
const feedback = ref('')
const saving = ref(false)
const records = ref([])

const load = async () => {
  const accommodation = await studentApi.getAccommodation(studentId.value).catch(() => null)
  if (accommodation?.roomId) roomId.value = accommodation.roomId
  const list = await studentApi.getMyCleaningRequests().catch(() => [])
  records.value = normalizeCollection(list).items || list || []
}

const submit = async () => {
  saving.value = true
  feedback.value = ''
  try {
    await studentApi.applyCleaning({ reason: reason.value || null })
    feedback.value = '保洁申请已提交，等待宿管处理'
    reason.value = ''
    await load()
  } catch (error) {
    feedback.value = toUserMessage(error, '保洁申请提交失败')
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  load().catch(() => {})
})
</script>

<template>
  <main class="cleaning-page">
    <WorkspaceHeader
      eyebrow="STUDENT / CLEANING"
      title="申请保洁"
      description="为当前在住宿舍提交保洁申请；提交后由宿管处理，可在这里查看进度。"
    >
      <b v-if="roomId" class="room-pill">当前宿舍 · 房间 {{ roomId }}</b>
    </WorkspaceHeader>

    <p v-if="feedback" class="feedback" role="status">{{ feedback }}</p>

    <section class="apply-card">
      <h2>申请内容</h2>
      <textarea
        v-model="reason"
        rows="3"
        maxlength="200"
        placeholder="可选：填写需要保洁的区域或备注"
      ></textarea>
      <button class="btn btn-primary" :disabled="saving || !roomId" @click="submit">
        {{ saving ? '提交中…' : roomId ? '申请保洁' : '当前无在住房间，无法申请' }}
      </button>
      <p v-if="!roomId" class="muted">退宿或无在住房间时无法申请，请先由宿管分配住宿。</p>
    </section>

    <section class="history-card">
      <h2>我的保洁申请</h2>
      <ul v-if="records.length" class="record-list">
        <li v-for="item in records" :key="item.taskId">
          <div>
            <strong>{{ item.roomNumber ? `房间 ${item.roomNumber}` : `房间 ${item.roomId || '—'}` }}</strong>
            <small>{{ item.description }} · 提交 {{ (item.createTime || '').slice?.(0, 16) || '—' }}</small>
          </div>
          <b>{{ item.status }}</b>
        </li>
      </ul>
      <p v-else class="muted">暂无保洁申请记录。</p>
    </section>
  </main>
</template>

<style scoped>
.cleaning-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.room-pill {
  padding: 8px 16px;
  border-radius: 999px;
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 14px;
  font-weight: 800;
}
.feedback {
  margin: 18px 0 0;
  padding: 12px 16px;
  background: var(--color-brand-soft);
  border-radius: var(--radius-lg);
  color: var(--color-brand-strong);
  font-size: 13px;
}
.apply-card,
.history-card {
  margin-top: 24px;
  padding: 26px 28px;
  border: 1px solid var(--color-line);
  border-radius: var(--radius-lg);
  background: #fff;
}
.apply-card h2,
.history-card h2 {
  margin: 0 0 14px;
  font: 700 20px var(--font-display);
}
.apply-card textarea {
  width: 100%;
  min-height: 90px;
  padding: 12px 14px;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  color: var(--color-ink);
  font: 600 14px var(--font-body);
}
.apply-card .btn {
  margin-top: 12px;
}
.muted {
  color: var(--color-text-muted);
  font-size: 13px;
}
.record-list {
  margin: 0;
  padding: 0;
  list-style: none;
}
.record-list li {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 12px 0;
  border-top: 1px solid var(--color-line);
}
.record-list li div {
  display: grid;
  gap: 4px;
}
.record-list strong {
  color: var(--color-ink);
}
.record-list small {
  color: var(--color-text-muted);
  font-size: 12px;
}
.record-list b {
  color: var(--color-brand);
  font-size: 13px;
}
</style>
