<script setup>
defineProps({
  eyebrow: { type: String, required: true },
  title: { type: String, required: true },
  description: { type: String, default: '' }
})
</script>

<template>
  <header class="workspace-header">
    <div>
      <p>{{ eyebrow }}</p>
      <h1>{{ title }}</h1>
      <span v-if="description">{{ description }}</span>
    </div>
    <aside v-if="$slots.default"><slot /></aside>
  </header>
</template>

<style scoped>
.workspace-header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  padding: 40px 0 28px;
  border-bottom: 1px solid var(--color-line-strong);
  gap: 28px;
}
.workspace-header p {
  margin: 0 0 11px;
  color: var(--color-accent-strong);
  font: 9px var(--font-mono);
  font-weight: 700;
  letter-spacing: 0.17em;
}
.workspace-header h1 {
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: clamp(34px, 4vw, 54px);
  font-weight: 600;
  line-height: 1.05;
  letter-spacing: -0.045em;
}
.workspace-header span {
  display: block;
  max-width: 700px;
  margin-top: 13px;
  color: var(--color-text-muted);
  font-size: 12px;
  line-height: 1.75;
}
.workspace-header aside {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 9px;
}
@media (max-width: 720px) {
  .workspace-header {
    align-items: start;
    flex-direction: column;
    padding-top: 28px;
  }
  .workspace-header aside {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>
