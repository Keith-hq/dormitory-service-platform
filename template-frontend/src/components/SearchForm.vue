<template>
  <div class="search-form">
    <slot></slot>
    <button class="btn btn-primary" @click="$emit('search')">查询</button>
    <button class="btn" @click="reset">重置</button>
  </div>
</template>

<script setup>
const emit = defineEmits(['search', 'reset'])

const reset = () => {
  emit('reset')
  emit('search')
}
</script>

<style scoped>
.search-form {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}
.btn {
  cursor: pointer;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  padding: 6px 14px;
  background: #fff;
  font-size: 14px;
}
.btn-primary {
  background: #1890ff;
  color: #fff;
  border-color: #1890ff;
}
</style>
