<template>
  <header class="page-header">
    <div class="page-header__copy">
      <span class="page-header__eyebrow">{{ eyebrow }}</span>
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
  overflow: hidden;
  min-height: 210px;
  padding: 52px 0 42px;
}

.page-header::before {
  position: absolute;
  inset: 0 calc(50% - 50vw);
  z-index: -2;
  background:
    radial-gradient(circle at 12% 4%, rgba(45, 139, 255, 0.17), transparent 30%),
    linear-gradient(180deg, rgba(233, 246, 255, 0.96), rgba(247, 251, 255, 0.9));
  content: '';
}

.page-header__copy {
  min-width: 0;
  max-width: 920px;
  text-align: left;
}
.page-header__eyebrow {
  display: block;
  margin: 0 0 15px;
  color: var(--color-brand);
  font-family: 'Segoe UI Black', 'Arial Black', 'Inter', sans-serif;
  font-size: 14px;
  font-weight: 900;
  line-height: 1;
  letter-spacing: 0;
}

.page-header h1 {
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 60px;
  font-weight: 900;
  letter-spacing: 0;
  line-height: 1.12;
  -webkit-text-stroke: 0.45px currentColor;
}

.page-header__description {
  max-width: 820px;
  margin: 20px 0 0;
  color: var(--color-text-muted);
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 800;
  line-height: 1.65;
  letter-spacing: 0;
  -webkit-text-stroke: 0.12px currentColor;
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
    min-height: 0;
    padding: 36px 0 38px;
  }

  .page-header h1 {
    font-size: 42px;
  }

  .page-header__eyebrow {
    font-size: 12px;
  }

  .page-header__aside {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>
