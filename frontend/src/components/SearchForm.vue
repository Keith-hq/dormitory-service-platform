<template>
  <form
    class="search-form"
    role="search"
    :aria-busy="loading"
    @submit.prevent="submit"
    @reset.prevent="reset"
  >
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
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
  padding: 14px 16px;
  border: 1px solid #e3e8ef;
  border-radius: 10px;
  background: #fbfcfe;
}
.search-fields,
.search-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.search-fields {
  flex: 1;
}
.search-actions {
  justify-content: flex-end;
}
.btn {
  cursor: pointer;
  min-height: 34px;
  border: 1px solid #cfd7e3;
  border-radius: 7px;
  padding: 6px 15px;
  background: #fff;
  color: #344054;
  font-size: 14px;
  transition:
    border-color 0.18s ease,
    box-shadow 0.18s ease,
    transform 0.18s ease;
}
.btn:hover:not(:disabled) {
  border-color: #1677ff;
  color: #0958d9;
}
.btn:focus-visible {
  outline: 2px solid rgba(22, 119, 255, 0.35);
  outline-offset: 2px;
}
.btn:active:not(:disabled) {
  transform: translateY(1px);
}
.btn:disabled {
  cursor: not-allowed;
  opacity: 0.58;
}
.btn-primary {
  background: #1677ff;
  color: #fff;
  border-color: #1677ff;
  box-shadow: 0 4px 10px rgba(22, 119, 255, 0.16);
}
.btn-primary:hover:not(:disabled) {
  border-color: #0958d9;
  background: #0958d9;
  color: #fff;
}

@media (max-width: 640px) {
  .search-form {
    align-items: stretch;
    flex-direction: column;
  }
  .search-actions {
    justify-content: flex-start;
  }
}
</style>
