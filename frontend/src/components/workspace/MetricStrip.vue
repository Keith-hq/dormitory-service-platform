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
  border-block: 1px solid var(--color-line-strong);
}
.metric-strip article {
  display: grid;
  min-height: 104px;
  padding: 17px 20px;
  border-right: 1px solid var(--color-line);
  align-content: space-between;
}
.metric-strip article:last-child {
  border-right: 0;
}
.metric-strip > article > span {
  color: var(--color-text-muted);
  font: 8px var(--font-mono);
  letter-spacing: 0.1em;
}
.metric-strip article div {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 10px;
}
.metric-strip strong {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 28px;
  font-weight: 500;
}
.metric-strip small {
  color: var(--color-text-soft);
  font-size: 9px;
}
@media (max-width: 720px) {
  .metric-strip {
    grid-template-columns: 1fr;
  }
  .metric-strip article {
    min-height: 78px;
    border-right: 0;
    border-bottom: 1px solid var(--color-line);
  }
}
</style>
