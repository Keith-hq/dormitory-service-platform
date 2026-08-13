<template>
  <header class="page-header">
    <div class="page-header__copy">
      <p class="page-header__eyebrow">{{ eyebrow }}</p>
      <h1>{{ title }}</h1>
      <p v-if="description" class="page-header__description">{{ description }}</p>
    </div>
    <div v-if="$slots.default" class="page-header__aside">
      <slot></slot>
    </div>
  </header>
</template>

<script setup>
defineProps({
  eyebrow: { type: String, default: 'CAMPUS OPERATIONS' },
  title: { type: String, required: true },
  description: { type: String, default: '' }
})
</script>

<style scoped>
.page-header {
  position: relative;
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--space-8);
  padding: var(--space-8) 0 var(--space-7);
}

.page-header::after {
  content: '';
  position: absolute;
  right: 0;
  bottom: 0;
  left: 0;
  height: 1px;
  background: linear-gradient(90deg, var(--color-brand) 0 72px, var(--color-line) 72px 100%);
}

.page-header__copy {
  min-width: 0;
  text-align: left;
}

.page-header__eyebrow {
  margin: 0 0 var(--space-3);
  color: var(--color-brand-strong);
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.18em;
}

.page-header h1 {
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: clamp(32px, 4vw, 46px);
  font-weight: 700;
  letter-spacing: -0.035em;
  line-height: 1.08;
}

.page-header__description {
  max-width: 680px;
  margin: var(--space-3) 0 0;
  color: var(--color-text-muted);
  font-size: 14px;
  line-height: 1.7;
}

.page-header__aside {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--space-3);
  flex: 0 0 auto;
}

@media (max-width: 720px) {
  .page-header {
    align-items: flex-start;
    flex-direction: column;
    gap: var(--space-5);
    padding-top: var(--space-6);
  }

  .page-header__aside {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>
