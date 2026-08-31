<script setup>
import { onMounted, ref } from 'vue'
import { adminApi } from '@/api/admin'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const loading = ref(true)
const error = ref('')
const feedback = ref('')
const workingId = ref(null)
const statusFilter = ref('待复核')
const appeals = ref([])
const total = ref(0)

const tabs = [
  { key: '待复核', label: '待复核' },
  { key: '已通过', label: '已通过' },
  { key: '已驳回', label: '已驳回' },
  { key: '', label: '全部' }
]

const statusTone = (status) =>
  ({ 待复核: 'warning', 已通过: 'success', 已驳回: 'danger' })[status] || 'info'

const formatTime = (value) => (value ? new Date(value).toLocaleString('zh-CN') : '—')

const load = async () => {
  loading.value = true
  error.value = ''
  try {
    const data = await adminApi.getCreditAppeals({
      status: statusFilter.value,
      page: 1,
      pageSize: 50
    })
    appeals.value = normalizeCollection(data).items
    total.value = data?.total ?? appeals.value.length
  } catch (e) {
    error.value = toUserMessage(e, '申诉队列暂时无法同步')
  } finally {
    loading.value = false
  }
}

const switchTab = (key) => {
  statusFilter.value = key
  load()
}

const review = async (item, result) => {
  let note
  if (result === '驳回') {
    note = window.prompt(
      `驳回学生 ${item.studentId} 的申诉（扣分明细 #${item.creditRecordId}），请填写驳回原因：`
    )
    if (note === null) return
    if (!note.trim()) {
      window.alert('驳回原因不能为空')
      return
    }
    note = note.trim()
  } else if (
    !window.confirm(
      `确认通过学生 ${item.studentId} 的申诉？将恢复 ${Math.abs(item.scoreChange ?? 0)} 分。`
    )
  ) {
    return
  }

  workingId.value = item.appealId
  feedback.value = ''
  try {
    await adminApi.reviewCreditAppeal(item.appealId, {
      result,
      ...(result === '驳回' ? { note } : {})
    })
    feedback.value =
      result === '通过'
        ? `申诉 #${item.appealId} 已通过，信用分已恢复`
        : `申诉 #${item.appealId} 已驳回`
    await load()
  } catch (e) {
    error.value = toUserMessage(e, '复核操作失败')
  } finally {
    workingId.value = null
  }
}

onMounted(load)
</script>

<template>
  <main class="appeal-page workspace-page">
    <WorkspaceHeader
      eyebrow="ADMIN / CREDIT APPEALS"
      title="信用申诉复核"
      description="学生对本人的扣分明细发起申诉，楼长复核通过后自动恢复信用分。"
    >
      <StatusTag
        :label="loading ? '同步中' : error ? '同步失败' : `${total} 条申诉`"
        :tone="loading ? 'info' : error ? 'warning' : 'success'"
        :dot="false"
      />
    </WorkspaceHeader>

    <p v-if="feedback" class="appeal-feedback" role="status">{{ feedback }}</p>

    <div class="appeal-tabs" aria-label="申诉状态筛选">
      <button
        v-for="tab in tabs"
        :key="tab.key || 'all'"
        type="button"
        :class="{ active: statusFilter === tab.key }"
        @click="switchTab(tab.key)"
      >
        {{ tab.label }}
      </button>
    </div>

    <InlineState
      :loading="loading"
      :error="error"
      :empty="!loading && !appeals.length"
      empty-text="暂无该状态的申诉"
    />

    <div class="appeal-list">
      <article v-for="item in appeals" :key="item.appealId">
        <time>{{ formatTime(item.createTime) }}</time>
        <div class="appeal-main">
          <div class="appeal-title">
            <h3>{{ item.studentId }}</h3>
            <StatusTag :label="item.status" :tone="statusTone(item.status)" size="small" />
          </div>
          <p class="appeal-reason">{{ item.reason }}</p>
          <div class="appeal-meta">
            <span
              >扣分明细 #{{ item.creditRecordId }}：{{ item.scoreChange ?? 0 }} 分 ·
              {{ item.creditReason || '—' }}</span
            >
            <span v-if="item.reviewedBy">复核人：{{ item.reviewedBy }}</span>
            <span v-if="item.resultDesc">复核说明：{{ item.resultDesc }}</span>
          </div>
        </div>
        <div v-if="item.status === '待复核'" class="appeal-actions">
          <button
            type="button"
            class="approve"
            :disabled="workingId === item.appealId"
            @click="review(item, '通过')"
          >
            {{ workingId === item.appealId ? '处理中…' : '通过' }}
          </button>
          <button
            type="button"
            class="reject"
            :disabled="workingId === item.appealId"
            @click="review(item, '驳回')"
          >
            驳回
          </button>
        </div>
      </article>
    </div>
  </main>
</template>

<style scoped>
.workspace-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 76px;
}
.appeal-feedback {
  margin: 18px 0 0;
  padding: 12px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-size: 14px;
  font-weight: 700;
}
.appeal-tabs {
  display: inline-flex;
  gap: 6px;
  margin-top: 26px;
  padding: 8px;
  border-radius: var(--radius-lg);
  background: var(--color-surface-muted);
}
.appeal-tabs button {
  min-height: 40px;
  padding: 8px 18px;
  border: 0;
  border-radius: var(--radius-lg);
  background: transparent;
  color: var(--color-text-muted);
  font-family: var(--font-body);
  font-size: 14px;
  font-weight: 850;
  cursor: pointer;
}
.appeal-tabs button.active {
  background: var(--color-brand-soft);
  color: var(--color-brand);
}
.appeal-list {
  display: grid;
  gap: 12px;
  margin-top: 16px;
}
.appeal-list article {
  display: grid;
  grid-template-columns: 150px minmax(0, 1fr) auto;
  align-items: center;
  gap: 22px;
  min-width: 0;
  padding: 20px 24px;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
  transition:
    background 0.18s ease,
    box-shadow 0.18s ease;
}
.appeal-list article:hover {
  background: var(--color-brand-soft);
  box-shadow: 0 12px 26px rgba(11, 99, 199, 0.1);
}
.appeal-list time {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 13px;
  font-weight: 900;
  line-height: 1.5;
}
.appeal-main {
  min-width: 0;
}
.appeal-title {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.appeal-title h3 {
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 900;
}
.appeal-reason {
  margin: 8px 0 0;
  color: var(--color-text);
  font-size: 15px;
  font-weight: 650;
  line-height: 1.7;
}
.appeal-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 18px;
  margin-top: 10px;
}
.appeal-meta span {
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 700;
}
.appeal-actions {
  display: flex;
  gap: 8px;
}
.appeal-actions button {
  min-height: 38px;
  padding: 8px 18px;
  border: 1px solid var(--color-line-strong);
  border-radius: 999px;
  background: #fff;
  font-family: var(--font-body);
  font-size: 13px;
  font-weight: 850;
  cursor: pointer;
}
.appeal-actions button.approve {
  border-color: var(--color-brand-border);
  background: var(--color-brand-soft);
  color: var(--color-brand);
}
.appeal-actions button.reject {
  color: var(--color-danger);
}
.appeal-actions button.reject:hover:not(:disabled) {
  border-color: #efc4bc;
  background: var(--color-danger-soft);
}
.appeal-actions button:disabled {
  cursor: not-allowed;
  opacity: 0.45;
}
@media (max-width: 760px) {
  .appeal-list article {
    grid-template-columns: 1fr;
    gap: 12px;
  }
  .appeal-actions {
    justify-content: flex-end;
  }
}
</style>
