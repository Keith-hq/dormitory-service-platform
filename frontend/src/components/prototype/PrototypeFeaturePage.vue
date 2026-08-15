<!-- PROTOTYPE ONLY — generic feature workbench used to validate page structure and coverage. -->
<script setup>
import { computed, ref } from 'vue'

const props = defineProps({
  feature: { type: Object, required: true },
  roleLabel: { type: String, required: true }
})

const activeTab = ref('workspace')
const feedback = ref('')

const templates = {
  form: {
    metrics: ['资料完整度 92%', '本月更新 1 次', '待确认 1 项'],
    columns: ['资料项', '当前内容', '更新时间', '状态'],
    states: ['已验证', '正常', '待确认']
  },
  records: {
    metrics: ['记录总数 28', '本月新增 4', '异常 1 项'],
    columns: ['记录编号', '事项', '发生时间', '状态'],
    states: ['正常', '已完成', '待处理']
  },
  finance: {
    metrics: ['本期金额 ¥486.20', '待处理 7 笔', '完成率 96.8%'],
    columns: ['账期 / 单号', '费用项目', '金额', '状态'],
    states: ['待支付', '已完成', '核对中']
  },
  request: {
    metrics: ['进行中 3 项', '今日更新 5 次', '按时完成 98%'],
    columns: ['申请编号', '申请事项', '更新时间', '状态'],
    states: ['处理中', '已通过', '待补充']
  },
  schedule: {
    metrics: ['今日可约 18 段', '我的预约 2 项', '履约率 100%'],
    columns: ['设施', '预约时段', '使用位置', '状态'],
    states: ['可预约', '已预约', '使用中']
  },
  inventory: {
    metrics: ['库存记录 286 项', '低库存 4 项', '今日变动 12 次'],
    columns: ['物品 / 资产', '所在位置', '数量', '状态'],
    states: ['充足', '使用中', '需补充']
  },
  safety: {
    metrics: ['今日记录 46 条', '异常 3 条', '已处理 91%'],
    columns: ['时间', '对象', '事件', '处理状态'],
    states: ['正常', '待核验', '已处置']
  },
  report: {
    metrics: ['本月样本 286 条', '较上月 +6.4%', '数据更新 15:40'],
    columns: ['统计维度', '当前结果', '环比变化', '状态'],
    states: ['已更新', '上升', '稳定']
  },
  community: {
    metrics: ['进行中 6 项', '今日参与 42 人', '覆盖率 88%'],
    columns: ['主题', '参与范围', '发布时间', '状态'],
    states: ['进行中', '已发布', '已结束']
  },
  management: {
    metrics: ['档案记录 1,286 条', '今日变更 18 条', '待复核 3 条'],
    columns: ['档案编号', '名称', '归属 / 范围', '状态'],
    states: ['启用', '正常', '待复核']
  },
  workflow: {
    metrics: ['当前队列 12 项', '临近超时 2 项', '今日完成 24 项'],
    columns: ['业务编号', '当前环节', '负责人', '状态'],
    states: ['待处理', '流转中', '已完成']
  }
}

const template = computed(() => templates[props.feature.mode] || templates.records)
const rows = computed(() => {
  const items = props.feature.samples?.length
    ? props.feature.samples
    : ['示例记录一', '示例记录二', '示例记录三']
  return items
    .slice(0, 3)
    .map((item, index) => [
      index === 0 ? 'TODAY' : `P-${String(index + 1).padStart(3, '0')}`,
      item,
      index === 0 ? '刚刚更新' : index === 1 ? '今天 13:40' : '昨天 18:20',
      template.value.states[index % template.value.states.length]
    ])
})

const triggerPrototypeAction = () => {
  feedback.value = `“${props.feature.action}”交互将在真实页面阶段接入，这里只验证入口和布局。`
  window.setTimeout(() => (feedback.value = ''), 2600)
}
</script>

<template>
  <section class="feature-page" :class="`feature-page--${feature.mode}`">
    <header class="feature-page__header">
      <div>
        <p>{{ feature.code }} · {{ roleLabel }}工作区</p>
        <h1>{{ feature.title }}</h1>
        <span>{{ feature.description }}</span>
      </div>
      <button class="primary-action" type="button" @click="triggerPrototypeAction">
        {{ feature.action }} <b>↗</b>
      </button>
    </header>

    <div class="coverage-line">
      <span>功能覆盖</span>
      <b>{{ feature.coverage }}</b>
      <i></i>
      <small>原型数据 · 不会写入系统</small>
    </div>

    <section class="feature-metrics">
      <article v-for="(metric, index) in template.metrics" :key="metric">
        <span>0{{ index + 1 }}</span>
        <strong>{{ metric }}</strong>
      </article>
    </section>

    <div class="feature-page__body">
      <section class="work-surface">
        <header>
          <div class="surface-tabs">
            <button :class="{ active: activeTab === 'workspace' }" @click="activeTab = 'workspace'">
              工作台
            </button>
            <button :class="{ active: activeTab === 'records' }" @click="activeTab = 'records'">
              全部记录
            </button>
            <button :class="{ active: activeTab === 'rules' }" @click="activeTab = 'rules'">
              规则说明
            </button>
          </div>
          <label
            ><span>筛选</span><input aria-label="筛选当前功能记录" placeholder="输入关键词…"
          /></label>
        </header>

        <div v-if="activeTab === 'rules'" class="rule-sheet">
          <p>功能边界</p>
          <h2>{{ feature.title }}的业务规则</h2>
          <ol>
            <li>所有状态变化均由后端校验业务条件与操作者权限。</li>
            <li>重复提交、并发写入和失败回滚需要在真实实现中验证。</li>
            <li>页面字段与操作入口最终以 Apifox 锁定契约为准。</li>
          </ol>
        </div>

        <div
          v-else-if="feature.mode === 'report' && activeTab === 'workspace'"
          class="report-sheet"
        >
          <header><span>趋势概览</span><b>2026 / 08</b></header>
          <div class="report-bars">
            <i
              v-for="(height, index) in [42, 66, 51, 82, 74, 93, 78, 88]"
              :key="index"
              :style="{ height: `${height}%` }"
              ><small>{{ index + 1 }}</small></i
            >
          </div>
          <footer><span>月初</span><span>数据按日聚合</span><span>今日</span></footer>
        </div>

        <div v-else class="prototype-table" role="table" :aria-label="`${feature.title}示例记录`">
          <div class="prototype-table__row prototype-table__head" role="row">
            <span v-for="column in template.columns" :key="column" role="columnheader">{{
              column
            }}</span>
          </div>
          <div
            v-for="(row, rowIndex) in rows"
            :key="rowIndex"
            class="prototype-table__row"
            role="row"
          >
            <span
              v-for="(cell, cellIndex) in row"
              :key="cellIndex"
              role="cell"
              :class="{ status: cellIndex === 3 }"
              >{{ cell }}</span
            >
          </div>
        </div>

        <footer class="surface-footer">
          <span>显示 3 条代表性数据</span><button type="button">上一页</button><b>1 / 4</b
          ><button type="button">下一页</button>
        </footer>
      </section>

      <aside class="process-note">
        <span class="process-note__eyebrow">PROCESS / GUIDE</span>
        <h2>建议操作顺序</h2>
        <ol>
          <li>
            <i>1</i>
            <div><b>确认业务对象</b><span>先定位学生、房间或业务记录。</span></div>
          </li>
          <li>
            <i>2</i>
            <div><b>核对当前状态</b><span>仅展示当前状态允许的动作。</span></div>
          </li>
          <li>
            <i>3</i>
            <div><b>执行并留痕</b><span>关键操作进入日志和通知中心。</span></div>
          </li>
        </ol>
        <div class="process-note__tip">
          <b>设计说明</b><span>真实页面会根据此功能的接口字段替换示例内容。</span>
        </div>
      </aside>
    </div>

    <transition name="toast">
      <div v-if="feedback" class="prototype-toast" role="status">{{ feedback }}</div>
    </transition>
  </section>
</template>

<style scoped>
* {
  box-sizing: border-box;
}
button,
input {
  font: inherit;
}
.feature-page {
  color: #1c2520;
  font-family: 'Avenir Next', 'PingFang SC', sans-serif;
}
.feature-page__header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  padding: 37px 0 27px;
  border-bottom: 1px solid #9fa49c;
  gap: 30px;
}
.feature-page__header p {
  margin: 0 0 10px;
  color: #c15640;
  font:
    9px 'SFMono-Regular',
    monospace;
  letter-spacing: 0.15em;
}
.feature-page__header h1 {
  margin: 0;
  font-family: 'Songti SC', STSong, serif;
  font-size: clamp(32px, 4vw, 52px);
  font-weight: 600;
  letter-spacing: -0.04em;
}
.feature-page__header span {
  display: block;
  max-width: 680px;
  margin-top: 12px;
  color: #6e746d;
  font-size: 12px;
  line-height: 1.7;
}
.primary-action {
  min-width: 148px;
  padding: 13px 16px;
  border: 1px solid #23332b;
  background: #23332b;
  color: #f6f0e4;
  text-align: left;
  cursor: pointer;
}
.primary-action b {
  float: right;
  color: #df8b66;
}
.coverage-line {
  display: flex;
  align-items: center;
  min-height: 41px;
  border-bottom: 1px solid #d0cbc0;
  color: #7b817a;
  font-size: 9px;
  gap: 9px;
}
.coverage-line b {
  color: #35483d;
  font-weight: 600;
}
.coverage-line i {
  width: 1px;
  height: 13px;
  margin: 0 5px;
  background: #bcb9b0;
}
.coverage-line small {
  font-size: 9px;
}
.feature-metrics {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  margin: 22px 0 13px;
  gap: 10px;
}
.feature-metrics article {
  display: grid;
  min-height: 80px;
  align-content: space-between;
  padding: 14px 16px;
  border: 1px solid #c3c1b7;
  background: rgba(238, 234, 222, 0.55);
}
.feature-metrics span {
  color: #c4624b;
  font: 8px monospace;
}
.feature-metrics strong {
  font-family: 'Songti SC', serif;
  font-size: 17px;
  font-weight: 500;
}
.feature-page__body {
  display: grid;
  grid-template-columns: minmax(0, 1.6fr) minmax(240px, 0.55fr);
  gap: 14px;
}
.work-surface,
.process-note {
  border: 1px solid #bcbdb4;
  background: rgba(247, 243, 233, 0.56);
}
.work-surface > header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 54px;
  padding: 0 16px;
  border-bottom: 1px solid #c6c4ba;
}
.surface-tabs {
  display: flex;
  align-self: stretch;
}
.surface-tabs button {
  padding: 0 13px;
  border: 0;
  border-bottom: 2px solid transparent;
  background: none;
  color: #858981;
  font-size: 10px;
  cursor: pointer;
}
.surface-tabs button.active {
  border-color: #c45f48;
  color: #28372f;
}
.work-surface label {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #848980;
  font-size: 9px;
}
.work-surface input {
  width: 138px;
  padding: 7px 9px;
  border: 1px solid #cbc8bd;
  background: #f7f2e7;
  color: #26342d;
  font-size: 9px;
  outline: 0;
}
.work-surface input:focus {
  border-color: #5f7669;
}
.prototype-table__row {
  display: grid;
  grid-template-columns: 0.75fr 1.5fr 1fr 0.7fr;
  min-height: 62px;
  align-items: center;
  padding: 0 17px;
  border-bottom: 1px solid #d6d1c6;
  gap: 12px;
}
.prototype-table__head {
  min-height: 38px;
  color: #8b8e86;
  font-size: 8px;
  letter-spacing: 0.08em;
}
.prototype-table__row:not(.prototype-table__head) {
  font-family: 'Songti SC', serif;
  font-size: 12px;
}
.prototype-table__row .status {
  width: max-content;
  padding: 4px 8px;
  border: 1px solid #93a096;
  color: #526359;
  font-family: 'Avenir Next', 'PingFang SC', sans-serif;
  font-size: 8px;
}
.report-sheet {
  min-height: 225px;
  padding: 22px 24px;
}
.report-sheet > header,
.report-sheet > footer {
  display: flex;
  justify-content: space-between;
  color: #727971;
  font-size: 9px;
}
.report-bars {
  display: flex;
  height: 155px;
  align-items: end;
  padding: 20px 4px 0;
  border-bottom: 1px solid #929a91;
  gap: clamp(10px, 2vw, 24px);
}
.report-bars i {
  position: relative;
  flex: 1;
  min-width: 13px;
  background: #506c5c;
}
.report-bars i:nth-child(3n) {
  background: #d07a5e;
}
.report-bars small {
  position: absolute;
  right: 0;
  bottom: -18px;
  left: 0;
  color: #868b84;
  font: 7px monospace;
  text-align: center;
}
.report-sheet > footer {
  margin-top: 25px;
}
.rule-sheet {
  min-height: 225px;
  padding: 27px;
}
.rule-sheet > p {
  color: #c15c45;
  font: 8px monospace;
  letter-spacing: 0.15em;
}
.rule-sheet h2 {
  margin: 8px 0 20px;
  font-family: 'Songti SC', serif;
  font-weight: 500;
}
.rule-sheet ol {
  display: grid;
  margin: 0;
  padding-left: 20px;
  color: #616a62;
  font-size: 11px;
  line-height: 2.1;
  gap: 4px;
}
.surface-footer {
  display: flex;
  align-items: center;
  min-height: 45px;
  padding: 0 16px;
  color: #858981;
  font-size: 8px;
  gap: 8px;
}
.surface-footer > span {
  margin-right: auto;
}
.surface-footer button {
  border: 0;
  background: none;
  color: #667168;
  font-size: 8px;
  cursor: pointer;
}
.surface-footer b {
  font: 8px monospace;
}
.process-note {
  padding: 20px 20px 18px;
  background: #263a30;
  color: #f4eddf;
}
.process-note__eyebrow {
  color: #dd906d;
  font: 8px monospace;
  letter-spacing: 0.15em;
}
.process-note h2 {
  margin: 7px 0 22px;
  font-family: 'Songti SC', serif;
  font-size: 20px;
  font-weight: 500;
}
.process-note ol {
  display: grid;
  margin: 0;
  padding: 0;
  list-style: none;
  gap: 18px;
}
.process-note li {
  display: grid;
  grid-template-columns: 28px 1fr;
  gap: 10px;
}
.process-note li i {
  display: grid;
  width: 25px;
  height: 25px;
  place-items: center;
  border: 1px solid #789081;
  border-radius: 50%;
  color: #e7a27e;
  font: 8px monospace;
}
.process-note li div {
  display: grid;
  gap: 4px;
}
.process-note li b {
  font-family: 'Songti SC', serif;
  font-size: 12px;
}
.process-note li span {
  color: #a7b5ab;
  font-size: 9px;
  line-height: 1.6;
}
.process-note__tip {
  display: grid;
  margin-top: 27px;
  padding: 13px;
  border-top: 1px solid #536b5e;
  background: #30483b;
  gap: 4px;
}
.process-note__tip b {
  font-size: 9px;
}
.process-note__tip span {
  color: #aab8af;
  font-size: 8px;
  line-height: 1.5;
}
.prototype-toast {
  position: fixed;
  z-index: 1000;
  right: 26px;
  bottom: 26px;
  max-width: 360px;
  padding: 13px 16px;
  border-left: 3px solid #d26d53;
  background: #1d2d25;
  color: #f8f0e4;
  box-shadow: 0 16px 45px rgba(0, 0, 0, 0.22);
  font-size: 10px;
}
.toast-enter-active,
.toast-leave-active {
  transition: 0.2s ease;
}
.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateY(8px);
}
@media (max-width: 900px) {
  .feature-page__header {
    align-items: start;
    flex-direction: column;
  }
  .feature-page__body {
    grid-template-columns: 1fr;
  }
  .feature-metrics {
    grid-template-columns: 1fr;
  }
  .work-surface > header {
    align-items: start;
    flex-direction: column;
    padding: 10px;
  }
  .surface-tabs {
    min-height: 36px;
  }
  .work-surface label,
  .work-surface input {
    width: 100%;
  }
  .prototype-table {
    overflow-x: auto;
  }
  .prototype-table__row {
    min-width: 660px;
  }
  .primary-action {
    width: 100%;
  }
}
</style>
