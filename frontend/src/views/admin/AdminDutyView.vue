<script setup>
import { ref } from 'vue'
import { adminApi } from '@/api/admin'
import { WorkspaceHeader } from '@/components'
import { toUserMessage } from '@/utils/errorMessage'

const visitor = ref({ visitorName: '', phone: '', studentId: '', visitReason: '' })
const feedback = ref('')
const submitting = ref(false)

const submitVisitor = async () => {
  submitting.value = true
  feedback.value = ''
  try {
    await adminApi.registerVisitor(visitor.value)
    feedback.value = '访客登记已提交'
    visitor.value = { visitorName: '', phone: '', studentId: '', visitReason: '' }
  } catch (e) {
    feedback.value = toUserMessage(e, '访客登记失败')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main class="duty-page">
    <WorkspaceHeader
      eyebrow="VISITOR DUTY DESK"
      title="访客值守"
      description="把现场登记集中到一张表单，核对来访人、被访学生和访问事由后直接提交。"
    >
      <span class="live-dot">值班在线</span>
    </WorkspaceHeader>
    <section class="visitor-desk">
      <div class="visitor-copy">
        <span>ON-SITE REGISTRATION</span>
        <h2>现场访客登记</h2>
        <p>核对来访人身份和被访学生后提交。后续扫码核验与离场记录沿用同一登记编号。</p>
      </div>
      <form @submit.prevent="submitVisitor">
        <label
          >访客姓名<input v-model.trim="visitor.visitorName" required placeholder="请输入真实姓名"
        /></label>
        <label
          >联系电话<input v-model.trim="visitor.phone" required placeholder="用于现场核验"
        /></label>
        <label
          >被访学生学号<input v-model.trim="visitor.studentId" required placeholder="如 20260001"
        /></label>
        <label class="wide"
          >来访事由<textarea
            v-model.trim="visitor.visitReason"
            required
            rows="3"
            placeholder="简要说明来访事项"
          ></textarea>
        </label>
        <p v-if="feedback" class="feedback">{{ feedback }}</p>
        <button class="btn btn-primary" :disabled="submitting">
          {{ submitting ? '提交中…' : '登记访客' }}
        </button>
      </form>
    </section>
  </main>
</template>

<style scoped>
.duty-page {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.live-dot {
  display: flex;
  align-items: center;
  gap: 8px;
  font: 10px var(--font-mono);
  color: var(--color-success);
}
.live-dot:before {
  content: '';
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
}
.visitor-desk {
  display: grid;
  grid-template-columns: 0.7fr 1.3fr;
  gap: 48px;
  margin-top: 28px;
  padding: 38px;
  background: var(--color-ink);
  color: #fff;
}
.visitor-copy span {
  color: var(--color-accent);
  font: 8px var(--font-mono);
  letter-spacing: 0.14em;
}
.visitor-copy h2 {
  margin: 12px 0;
  font: 500 30px var(--font-display);
}
.visitor-copy p {
  color: #aaa39a;
  font-size: 11px;
  line-height: 1.8;
}
.visitor-desk form {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 18px;
}
.visitor-desk label {
  display: flex;
  flex-direction: column;
  gap: 8px;
  font-size: 10px;
  color: #c7c0b6;
}
.visitor-desk .wide {
  grid-column: 1/-1;
}
.visitor-desk input,
.visitor-desk textarea {
  border: 1px solid #4c4944;
  background: #252422;
  color: #fff;
  padding: 12px;
  font: 12px var(--font-body);
}
.feedback {
  margin: 0;
  color: var(--color-accent);
  font-size: 11px;
}
@media (max-width: 760px) {
  .visitor-desk {
    grid-template-columns: 1fr;
    padding: 25px;
  }
  .visitor-desk form {
    grid-template-columns: 1fr;
  }
  .visitor-desk .wide {
    grid-column: auto;
  }
}
</style>
