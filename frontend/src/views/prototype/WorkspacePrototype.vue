<!--
  THROWAWAY PROTOTYPE — not production UI.
  Complete feature-point prototype for the selected editorial direction.
  Switch roles and pages with ?role= and ?page=; all actions are read-only fixtures.
-->
<script setup>
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import WorkspaceVariantA from './WorkspaceVariantA.vue'
import { findFeature } from './prototypeFeatures'

const route = useRoute()
const router = useRouter()
const validRoles = ['student', 'admin', 'counselor', 'repairman', 'super_admin']

const activeRole = computed(() => {
  const value = String(route.query.role || 'student')
  return validRoles.includes(value) ? value : 'student'
})
const activePage = computed(() => {
  const value = String(route.query.page || 'home')
  return value === 'home' || findFeature(activeRole.value, value) ? value : 'home'
})
const updatePage = (page) => router.replace({ query: { role: activeRole.value, page } })
const updateRole = (role) => router.replace({ query: { role, page: 'home' } })
</script>

<template>
  <WorkspaceVariantA
    :role="activeRole"
    :page="activePage"
    @change-role="updateRole"
    @change-page="updatePage"
  />
</template>
