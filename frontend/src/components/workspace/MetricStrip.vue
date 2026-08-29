<script setup>
defineProps({ metrics: { type: Array, default: () => [] } })
</script>

<template>
  <section class="metric-strip" aria-label="关键指标">
    <article v-for="(metric, index) in metrics" :key="metric.label">
      <span>0{{ index + 1 }} / {{ metric.label }}</span>
      <div>
        <strong>{{ metric.value }}</strong
        ><small>{{ metric.hint }}</small>
      </div>
    </article>
  </section>
</template>

<style scoped>
.metric-strip {
  display: grid;
  grid-template-columns: repeat(var(--metric-count, 3), 1fr);
  overflow: hidden;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1);
}
.metric-strip article {
  display: grid;
  min-height: 116px;
  padding: 22px 26px;
  align-content: space-between;
}
.metric-strip > article > span {
  color: var(--color-brand);
  font-size: 14px;
  font-weight: 850;
  letter-spacing: 0;
}
.metric-strip article div {
  display: flex;
  align-items: baseline;
  gap: 14px; /* 数字与提示靠拢，不再顶到两端 */
}
.metric-strip strong {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: clamp(34px, 3vw, 46px);
  font-weight: 950;
  line-height: 1;
}
.metric-strip small {
  color: var(--color-text-muted);
  font-size: 15px;
  font-weight: 700;
}
@media (max-width: 720px) {
  .metric-strip {
    grid-template-columns: 1fr;
  }
  .metric-strip article {
    min-height: 112px;
  }
}
</style>
