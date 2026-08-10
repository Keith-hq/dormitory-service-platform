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
  --tag-color: #475467;
  --tag-bg: #f2f4f7;
  --tag-border: #dfe3e8;

  display: inline-flex;
  align-items: center;
  width: fit-content;
  border: 1px solid var(--tag-border);
  border-radius: 999px;
  background: var(--tag-bg);
  color: var(--tag-color);
  font-weight: 650;
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
  --tag-color: #175cd3;
  --tag-bg: #eff8ff;
  --tag-border: #b2ddff;
}
.status-tag--success {
  --tag-color: #067647;
  --tag-bg: #ecfdf3;
  --tag-border: #abefc6;
}
.status-tag--warning {
  --tag-color: #b54708;
  --tag-bg: #fffaeb;
  --tag-border: #fedf89;
}
.status-tag--danger {
  --tag-color: #b42318;
  --tag-bg: #fef3f2;
  --tag-border: #fecdca;
}
.status-tag--rose {
  --tag-color: #c11574;
  --tag-bg: #fdf2fa;
  --tag-border: #fcceee;
}
</style>
