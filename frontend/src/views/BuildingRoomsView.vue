<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { accommodationApi } from '@/api/accommodation'
import { buildingApi } from '@/api/building'
import { StatusTag } from '@/components'

const route = useRoute()
const buildingId = Number(route.params.buildingId) || 0

const loading = ref(true)
const error = ref('')
const buildingName = ref(String(route.query?.name || ''))
const rooms = ref([])
const expandedRoomId = ref(null)
const occupantLoading = ref(false)
const occupantError = ref('')
const occupants = ref([])

const capacityLabel = (room) => `每间限 ${room.capacity ?? 4} 人`

const summary = computed(() => {
  const total = rooms.value.length
  const used = rooms.value.reduce((sum, room) => sum + Number(room.occupancy || 0), 0)
  const capacity = rooms.value.reduce((sum, room) => sum + Number(room.capacity ?? 4), 0)
  return { total, used, free: capacity - used }
})

const fullState = (room) => {
  const used = Number(room.occupancy || 0)
  const capacity = Number(room.capacity ?? 4)
  if (used === 0) return '空房'
  if (used >= capacity) return '满员'
  return '部分入住'
}

const loadRooms = async () => {
  loading.value = true
  error.value = ''
  try {
    const data = await buildingApi.getRooms(buildingId, { page: 1, pageSize: 999 })
    rooms.value = Array.isArray(data?.items) ? data.items : []
    if (!buildingName.value) {
      try {
        const detail = await buildingApi.getById(buildingId)
        buildingName.value = detail?.buildingName || `#${buildingId}`
      } catch {
        buildingName.value = `#${buildingId}`
      }
    }
  } catch (requestError) {
    error.value = requestError?.message || '房间列表暂时无法同步'
  } finally {
    loading.value = false
  }
}

const freeBedNumbers = (room) => {
  const capacity = Number(room.capacity ?? 4)
  const occupied = new Set(occupants.value.map((item) => Number(item.bedNo)))
  const free = []
  for (let bed = 1; bed <= capacity; bed += 1) {
    if (!occupied.has(bed)) free.push(bed)
  }
  return free
}

const toggleRoom = async (room) => {
  if (expandedRoomId.value === room.roomId) {
    expandedRoomId.value = null
    occupants.value = []
    occupantError.value = ''
    return
  }
  expandedRoomId.value = room.roomId
  occupants.value = []
  occupantError.value = ''
  occupantLoading.value = true
  try {
    const data = await accommodationApi.getRoomOccupants(room.roomId)
    occupants.value = Array.isArray(data) ? data : Array.isArray(data?.items) ? data.items : []
  } catch (requestError) {
    occupantError.value = requestError?.message || '住户信息暂时无法同步'
  } finally {
    occupantLoading.value = false
  }
}

onMounted(loadRooms)
</script>

<template>
  <main class="building-rooms-page">
    <header class="rooms-header">
      <router-link class="back-link" to="/building">← 返回楼栋档案</router-link>
      <div v-if="!loading && !error && summary.total" class="rooms-stat">
        <span>{{ summary.total }} 个房间</span>
        <span>已住 {{ summary.used }} 人 / 空位 {{ summary.free }}</span>
      </div>
    </header>

    <div v-if="loading" class="state-line">正在加载房间…</div>
    <div v-else-if="error" class="state-line state-line--error">{{ error }}</div>

    <section v-else-if="rooms.length" class="room-grid">
      <article
        v-for="room in rooms"
        :key="room.roomId"
        class="room-card"
        :class="{ 'room-card--open': expandedRoomId === room.roomId }"
      >
        <button type="button" class="room-card__head" @click="toggleRoom(room)">
          <span class="room-id">{{ room.roomId }}</span>
          <span class="room-no">房间号 {{ room.roomNumber || '—' }}</span>
          <StatusTag
            :label="fullState(room)"
            :tone="
              Number(room.occupancy || 0) === 0
                ? 'neutral'
                : Number(room.occupancy || 0) >= Number(room.capacity ?? 4)
                  ? 'danger'
                  : 'success'
            "
          />
          <span class="room-capacity"
            >{{ capacityLabel(room) }} · 已住 {{ room.occupancy || 0 }}</span
          >
          <span class="room-chevron">{{
            expandedRoomId === room.roomId ? '收起 ▲' : '查看住户 ▼'
          }}</span>
        </button>

        <div v-if="expandedRoomId === room.roomId" class="room-detail">
          <p v-if="occupantLoading" class="state-line">正在核对住户…</p>
          <p v-else-if="occupantError" class="state-line state-line--error">{{ occupantError }}</p>
          <template v-else>
            <ul v-if="occupants.length" class="occupant-list">
              <li v-for="item in occupants" :key="`${item.studentId}-${item.bedNo}`">
                <b>{{ item.bedNo }} 号床</b>
                <strong>{{ item.studentName || item.studentId }}</strong>
                <small
                  >{{ item.studentId }} ·
                  {{ (item.checkInDate || '').slice?.(0, 10) || '入住日期未知' }}</small
                >
              </li>
            </ul>
            <p v-else class="state-line">当前无人入住（空房）。</p>
            <p class="free-line">
              <template v-if="freeBedNumbers(room).length">
                可安排空位：{{ freeBedNumbers(room).join('、') }} 号床
              </template>
              <template v-else>本房已满员，入住分配将提示「房间已满」。</template>
            </p>
          </template>
        </div>
      </article>
    </section>

    <p v-else class="state-line">该楼栋暂无房间记录。</p>
  </main>
</template>

<style scoped>
.building-rooms-page {
  width: min(100% - 48px, var(--content-max, 1200px));
  margin: 0 auto;
  padding-bottom: 72px;
}
.back-link {
  display: inline-flex;
  align-items: center;
  width: fit-content;
  margin: 4px 0 18px;
  padding: 8px 16px;
  border: 1px solid var(--color-line-strong, #d5e0ef);
  border-radius: 999px;
  background: #fff;
  color: var(--color-brand);
  font-size: 14px;
  font-weight: 800;
  text-decoration: none;
  transition:
    background 0.15s ease,
    color 0.15s ease;
}
.back-link:hover {
  background: var(--color-brand-soft, #eaf2ff);
  color: var(--color-brand);
}
.rooms-kicker {
  display: block;
  margin: 0 0 12px;
  color: var(--color-brand, #1f6feb);
  font-size: 14px;
  font-weight: 900;
  letter-spacing: 0;
}
.rooms-header h1 {
  margin: 0;
  color: var(--color-ink, #12233f);
  font-family: var(--font-display, inherit);
  font-size: clamp(30px, 4vw, 44px);
  font-weight: 900;
  line-height: 1.15;
}
.rooms-description {
  max-width: 760px;
  margin: 14px 0 0;
  color: var(--color-text-muted, #5a6b85);
  font-size: 15px;
  line-height: 1.8;
}
.rooms-stat {
  display: flex;
  flex-wrap: wrap;
  gap: 10px 22px;
  margin-top: 18px;
  color: var(--color-text-muted, #5a6b85);
  font-size: 14px;
  font-weight: 800;
}
.state-line {
  padding: 22px 0;
  color: var(--color-text-muted, #5a6b85);
  font-size: 14px;
}
.state-line--error {
  color: var(--color-danger, #c0392b);
}
.room-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 14px;
  margin-top: 22px;
}
.room-card {
  border: 1px solid var(--color-line, #e2e9f3);
  border-radius: var(--radius-lg, 12px);
  background: #fff;
  overflow: hidden;
}
.room-card--open {
  border-color: var(--color-brand, #1f6feb);
}
.room-card__head {
  display: grid;
  grid-template-columns: auto 1fr auto;
  gap: 6px 12px;
  align-items: center;
  width: 100%;
  padding: 14px 16px;
  border: 0;
  background: transparent;
  cursor: pointer;
  text-align: left;
}
.room-id {
  font-family: var(--font-display, inherit);
  font-size: 24px;
  font-weight: 950;
  color: var(--color-brand, #1f6feb);
}
.room-no {
  color: var(--color-text-muted, #5a6b85);
  font-size: 13px;
  font-weight: 700;
}
.room-capacity {
  grid-column: 1 / -1;
  color: var(--color-text-muted, #5a6b85);
  font-size: 13px;
  font-weight: 700;
}
.room-chevron {
  color: var(--color-brand, #1f6feb);
  font-size: 13px;
  font-weight: 800;
}
.room-detail {
  padding: 4px 16px 16px;
  border-top: 1px dashed var(--color-line, #e2e9f3);
}
.occupant-list {
  margin: 12px 0 0;
  padding: 0;
  list-style: none;
}
.occupant-list li {
  display: grid;
  grid-template-columns: 84px 1fr auto;
  gap: 8px;
  align-items: baseline;
  padding: 9px 0;
  border-bottom: 1px solid var(--color-line, #e2e9f3);
}
.occupant-list li b {
  color: var(--color-brand, #1f6feb);
  font-size: 14px;
}
.occupant-list li strong {
  color: var(--color-ink, #12233f);
  font-size: 15px;
}
.occupant-list li small {
  color: var(--color-text-muted, #5a6b85);
  font-size: 13px;
}
.free-line {
  margin: 12px 0 0;
  color: var(--color-text-muted, #5a6b85);
  font-size: 13px;
  font-weight: 700;
}
</style>
