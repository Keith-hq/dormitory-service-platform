<template>
  <form
    class="search-form"
    role="search"
    :aria-busy="loading"
    @submit.prevent="submit"
    @reset.prevent="reset"
  >
    <div class="search-intro" aria-hidden="true">
      <span class="search-intro__mark">筛</span>
      <span>
        <strong>筛选与检索</strong>
        <small>快速定位目标记录</small>
      </span>
    </div>
    <div class="search-fields">
      <slot :disabled="controlsDisabled"></slot>
    </div>
    <div class="search-actions">
      <button type="submit" class="btn btn-primary" :disabled="controlsDisabled">
        {{ loading ? loadingText : searchText }}
      </button>
      <button v-if="showReset" type="reset" class="btn" :disabled="controlsDisabled">
        {{ resetText }}
      </button>
      <slot name="actions" :disabled="controlsDisabled"></slot>
    </div>
  </form>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  loading: { type: Boolean, default: false },
  disabled: { type: Boolean, default: false },
  showReset: { type: Boolean, default: true },
  searchText: { type: String, default: '查询' },
  resetText: { type: String, default: '重置' },
  loadingText: { type: String, default: '查询中...' }
})

const emit = defineEmits(['search', 'reset'])

const controlsDisabled = computed(() => props.loading || props.disabled)

const submit = () => {
  if (controlsDisabled.value) return
  emit('search')
}

const reset = () => {
  if (controlsDisabled.value) return
  emit('reset')
  emit('search')
}
</script>

<style scoped>
.search-form {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--space-6);
  padding: 24px 30px;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: 0 14px 34px rgba(23, 65, 120, 0.08);
}

.search-intro {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding-right: var(--space-5);
}

.search-intro__mark {
  display: grid;
  width: 40px;
  height: 40px;
  place-items: center;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-family: var(--font-display);
  font-size: 15px;
  font-weight: 900;
}

.search-intro > span:last-child {
  display: grid;
  gap: 1px;
  white-space: nowrap;
}

.search-intro strong {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 900;
}

.search-intro small {
  color: var(--color-text-muted);
  font-size: 14px;
  font-weight: 700;
}

.search-fields,
.search-actions {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  flex-wrap: wrap;
}

.search-fields {
  flex: 1;
}

.search-actions {
  justify-content: flex-end;
}

@media (max-width: 860px) {
  .search-form {
    align-items: stretch;
    grid-template-columns: 1fr auto;
  }

  .search-intro {
    display: none;
  }
}

@media (max-width: 640px) {
  .search-form {
    grid-template-columns: 1fr;
    gap: var(--space-4);
    padding: var(--space-4);
  }

  .search-actions {
    justify-content: stretch;
  }

  .search-actions .btn {
    flex: 1;
  }
}
</style>
