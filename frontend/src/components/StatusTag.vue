<template>
  <span class="status-tag" :class="[`status-tag--${tone}`, `status-tag--${size}`]">
    <span v-if="dot" class="status-dot" aria-hidden="true"></span>
    <slot>{{ label }}</slot>
  </span>
</template>

<script setup>
defineProps({
  label: { type: String, default: '' },
  tone: {
    type: String,
    default: 'neutral',
    validator: (value) =>
      ['neutral', 'info', 'success', 'warning', 'danger', 'rose'].includes(value)
  },
  size: {
    type: String,
    default: 'medium',
    validator: (value) => ['small', 'medium'].includes(value)
  },
  dot: { type: Boolean, default: true }
})
</script>

<style scoped>
.status-tag {
  --tag-color: var(--color-text-muted);
  --tag-bg: var(--color-surface-muted);
  --tag-border: var(--color-line);

  display: inline-flex;
  align-items: center;
  width: fit-content;
  border: 1px solid var(--tag-border);
  border-radius: 999px;
  background: var(--tag-bg);
  color: var(--tag-color);
  font-weight: 750;
  letter-spacing: 0.015em;
  line-height: 1;
  white-space: nowrap;
}
.status-tag--small {
  gap: 5px;
  padding: 4px 7px;
  font-size: 11px;
}
.status-tag--medium {
  gap: 6px;
  padding: 5px 9px;
  font-size: 12px;
}
.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
  box-shadow: 0 0 0 3px color-mix(in srgb, currentColor 12%, transparent);
}
.status-tag--info {
  --tag-color: #256a78;
  --tag-bg: #eaf5f5;
  --tag-border: #badcde;
}
.status-tag--success {
  --tag-color: #246b4b;
  --tag-bg: #eaf5ed;
  --tag-border: #bdddc7;
}
.status-tag--warning {
  --tag-color: #9a5d22;
  --tag-bg: #fff3dc;
  --tag-border: #efd29e;
}
.status-tag--danger {
  --tag-color: #a34239;
  --tag-bg: #fbeae6;
  --tag-border: #efc4bc;
}
.status-tag--rose {
  --tag-color: #9b5265;
  --tag-bg: #f9edf0;
  --tag-border: #e9c9d1;
}
</style>
