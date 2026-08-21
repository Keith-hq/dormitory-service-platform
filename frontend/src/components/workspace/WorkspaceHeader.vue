<script setup>
defineProps({
  eyebrow: { type: String, required: true },
  title: { type: String, required: true },
  description: { type: String, default: '' }
})
</script>

<template>
  <header class="workspace-header">
    <div class="workspace-header__copy">
      <span class="workspace-header__eyebrow">{{ eyebrow }}</span>
      <h1>{{ title }}</h1>
      <p v-if="description" class="workspace-header__description">{{ description }}</p>
    </div>
    <aside v-if="$slots.default" class="workspace-header__aside"><slot /></aside>
  </header>
</template>

<style scoped>
.workspace-header {
  position: relative;
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  overflow: hidden;
  min-height: 218px;
  padding: 54px 0 44px;
  border-bottom: 0;
  gap: 28px;
}
.workspace-header::before {
  position: absolute;
  inset: 0 calc(50% - 50vw);
  z-index: -2;
  background:
    radial-gradient(circle at 16% 10%, rgba(45, 139, 255, 0.18), transparent 30%),
    linear-gradient(180deg, rgba(233, 246, 255, 0.96), rgba(247, 251, 255, 0.9));
  content: '';
}
.workspace-header__copy {
  min-width: 0;
  max-width: 920px;
}
.workspace-header__eyebrow {
  display: block;
  margin: 0 0 15px;
  color: var(--color-brand);
  font-family: 'Segoe UI Black', 'Arial Black', 'Inter', sans-serif;
  font-size: 14px;
  font-weight: 900;
  line-height: 1;
  letter-spacing: 0;
}
.workspace-header h1 {
  position: relative;
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 60px;
  font-weight: 900;
  line-height: 1.12;
  letter-spacing: 0;
  -webkit-text-stroke: 0.45px currentColor;
}
.workspace-header__description {
  display: block;
  max-width: 880px;
  margin: 20px 0 0;
  color: var(--color-text-muted);
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 800;
  line-height: 1.65;
  letter-spacing: 0;
  -webkit-text-stroke: 0.12px currentColor;
}
.workspace-header__aside {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 9px;
  flex: 0 0 auto;
}
@media (max-width: 720px) {
  .workspace-header {
    align-items: start;
    flex-direction: column;
    min-height: 0;
    padding: 36px 0 38px;
  }
  .workspace-header h1 {
    font-size: 42px;
  }
  .workspace-header__eyebrow {
    font-size: 12px;
  }
  .workspace-header__aside {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>
