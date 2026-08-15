<!-- PROTOTYPE A — editorial living dossier. Throw away after design selection. -->
<script setup>
import { computed } from 'vue'
import PrototypeFeaturePage from '@/components/prototype/PrototypeFeaturePage.vue'
import { roleOptions, roleScenes } from './prototypeData'
import { coverageSummary, findFeature, roleCatalog } from './prototypeFeatures'

const props = defineProps({
  role: { type: String, required: true },
  page: { type: String, default: 'home' }
})
defineEmits(['change-role', 'change-page'])
const scene = computed(() => roleScenes[props.role] || roleScenes.student)
const catalog = computed(() => roleCatalog[props.role] || roleCatalog.student)
const activeFeature = computed(() => findFeature(props.role, props.page))
</script>

<template>
  <div class="dossier">
    <aside class="dossier__rail">
      <div class="dossier__brand">
        <span class="dossier__seal">舍</span>
        <div><b>住校誌</b><small>CAMPUS LIVING</small></div>
      </div>

      <nav class="dossier__nav" aria-label="完整功能导航">
        <button :class="{ active: page === 'home' }" @click="$emit('change-page', 'home')">
          <span>00</span>{{ catalog.homeLabel }}
        </button>
        <div v-for="group in catalog.groups" :key="group.label" class="nav-group">
          <p>{{ group.label }}</p>
          <button
            v-for="item in group.items"
            :key="item.key"
            :class="{ active: page === item.key }"
            @click="$emit('change-page', item.key)"
          >
            <span>{{ item.code.replace('FP', '') }}</span
            >{{ item.label }}
          </button>
        </div>
      </nav>

      <div class="coverage-badge">
        <span>功能覆盖</span>
        <b>{{ coverageSummary[role] }}</b>
        <small>已排除订水与快递</small>
      </div>

      <div class="dossier__profile">
        <span>{{ scene.identity.slice(0, 1) }}</span>
        <div>
          <b>{{ scene.identity }}</b
          ><small>{{ scene.location }}</small>
        </div>
      </div>
    </aside>

    <main class="dossier__paper">
      <header class="dossier__masthead">
        <div class="eyebrow">VOL. 08 / AUG 15, 2026 · {{ scene.roleLabel }}</div>
        <div class="role-tabs" aria-label="切换演示角色">
          <button
            v-for="item in roleOptions"
            :key="item.key"
            :class="{ active: role === item.key }"
            @click="$emit('change-role', item.key)"
          >
            {{ item.label }}
          </button>
        </div>
      </header>

      <template v-if="page === 'home'">
        <section class="dossier__overview">
          <div class="dossier__hero">
            <div class="hero-kicker">
              <span>今日生活提要</span>
              <b>{{ scene.stamp }}</b>
            </div>
            <h1>{{ scene.title }}</h1>
            <div class="hero-note"><i></i>{{ scene.subtitle }}</div>
            <div class="quick-actions">
              <button v-for="(action, index) in scene.quickActions" :key="action" type="button">
                <span>0{{ index + 1 }}</span>
                {{ action }}
                <b>↗</b>
              </button>
            </div>
          </div>

          <aside class="notice-board">
            <header>
              <div>
                <span>NEW / {{ scene.notifications.filter((item) => item.unread).length }}</span>
                <h2>通知与提醒</h2>
              </div>
              <button type="button">全部通知</button>
            </header>
            <article
              v-for="notice in scene.notifications"
              :key="notice.title"
              :class="{ unread: notice.unread }"
            >
              <span>{{ notice.type }}</span>
              <div>
                <h3>{{ notice.title }}</h3>
                <time>{{ notice.time }}</time>
              </div>
              <i></i>
            </article>
          </aside>
        </section>

        <section class="dossier__metrics">
          <article v-for="(metric, index) in scene.metrics" :key="metric.label">
            <span>0{{ index + 1 }} / {{ metric.label }}</span>
            <strong>{{ metric.value }}</strong>
            <small>{{ metric.delta }}</small>
          </article>
        </section>

        <div class="dossier__spread">
          <section class="ledger">
            <div class="section-title">
              <span>01</span>
              <h2>今日案头</h2>
              <small>TODAY'S LEDGER</small>
            </div>
            <div class="ledger__rows">
              <article v-for="task in scene.tasks" :key="task.title">
                <time>{{ task.time }}</time>
                <div>
                  <h3>{{ task.title }}</h3>
                  <p>{{ task.meta }}</p>
                </div>
                <span class="ledger__mark" :class="task.level"></span>
                <button type="button">查看记录 ↗</button>
              </article>
            </div>
          </section>

          <aside class="calendar">
            <div class="section-title">
              <span>02</span>
              <h2>近日安排</h2>
            </div>
            <article v-for="item in scene.timeline" :key="item.title">
              <time>{{ item.day }}</time>
              <h3>{{ item.title }}</h3>
              <p>{{ item.note }}</p>
            </article>
            <footer><b>服务台在线</b><span>预计 2 分钟内响应</span></footer>
          </aside>
        </div>
      </template>

      <PrototypeFeaturePage
        v-else-if="activeFeature"
        :feature="activeFeature"
        :role-label="catalog.label"
      />
    </main>
  </div>
</template>

<style scoped>
* {
  box-sizing: border-box;
}
button {
  font: inherit;
}
.dossier {
  min-height: 100svh;
  background: #eee8dc;
  color: #1c2520;
  font-family: 'Avenir Next', 'PingFang SC', sans-serif;
}
.dossier__rail {
  position: fixed;
  inset: 0 auto 0 0;
  display: flex;
  width: 270px;
  overflow: hidden;
  flex-direction: column;
  padding: 34px 24px 28px;
  background: #17231e;
  color: #f5efe2;
}
.dossier__brand {
  display: flex;
  align-items: center;
  gap: 13px;
}
.dossier__brand div {
  display: grid;
  gap: 1px;
}
.dossier__brand b {
  font-family: 'Songti SC', STSong, serif;
  font-size: 21px;
  letter-spacing: 0.12em;
}
.dossier__brand small {
  color: #aeb9b1;
  font-size: 8px;
  letter-spacing: 0.2em;
}
.dossier__seal {
  display: grid;
  width: 42px;
  height: 42px;
  place-items: center;
  border: 1px solid #d36245;
  color: #e47a5b;
  font-family: 'Songti SC', serif;
  font-size: 21px;
  transform: rotate(-4deg);
}
.dossier__nav {
  display: grid;
  flex: 1;
  margin-top: 32px;
  overflow-y: auto;
  padding-right: 5px;
  gap: 4px;
}
.dossier__nav::-webkit-scrollbar {
  width: 3px;
}
.dossier__nav::-webkit-scrollbar-thumb {
  background: #4a5a51;
}
.nav-group {
  display: grid;
  gap: 2px;
}
.nav-group p {
  margin: 15px 10px 5px;
  color: #5f7066;
  font-size: 8px;
  letter-spacing: 0.17em;
}
.dossier__nav button {
  display: grid;
  grid-template-columns: 44px 1fr;
  padding: 9px 10px;
  border: 0;
  border-left: 2px solid transparent;
  background: none;
  color: #96a099;
  text-align: left;
  cursor: pointer;
}
.dossier__nav button span {
  color: #5f6d64;
  font:
    9px 'SFMono-Regular',
    monospace;
}
.dossier__nav button.active {
  border-color: #dc6d4f;
  color: #fff8ea;
}
.coverage-badge {
  display: grid;
  margin-top: 14px;
  padding: 13px 12px;
  border: 1px solid #3e5047;
  gap: 3px;
}
.coverage-badge span {
  color: #7f9187;
  font-size: 8px;
  letter-spacing: 0.14em;
}
.coverage-badge b {
  color: #e5a17e;
  font-family: 'Songti SC', serif;
  font-size: 15px;
  font-weight: 500;
}
.coverage-badge small {
  color: #6f8177;
  font-size: 8px;
}
.dossier__profile {
  display: flex;
  align-items: center;
  margin-top: 14px;
  gap: 10px;
  border-top: 1px solid #34423a;
  padding-top: 22px;
}
.dossier__profile > span {
  display: grid;
  width: 34px;
  height: 34px;
  place-items: center;
  border-radius: 50%;
  background: #e9dcc6;
  color: #17231e;
  font-family: 'Songti SC', serif;
}
.dossier__profile div {
  display: grid;
  gap: 3px;
}
.dossier__profile b {
  font-size: 12px;
}
.dossier__profile small {
  color: #89968d;
  font-size: 9px;
}
.dossier__paper {
  min-height: 100svh;
  margin-left: 270px;
  padding: 30px clamp(28px, 5vw, 76px) 100px;
  background-color: #f5f0e5;
  background-image: linear-gradient(rgba(38, 45, 40, 0.028) 1px, transparent 1px);
  background-size: 100% 28px;
}
.dossier__masthead {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid #1c2520;
  padding-bottom: 14px;
}
.eyebrow {
  font:
    10px 'SFMono-Regular',
    monospace;
  letter-spacing: 0.14em;
}
.role-tabs {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 6px;
}
.role-tabs button {
  padding: 7px 12px;
  border: 1px solid #c5bcad;
  background: transparent;
  color: #72766f;
  font-size: 11px;
  cursor: pointer;
}
.role-tabs button.active {
  border-color: #1c2520;
  background: #1c2520;
  color: #fff8ea;
}
.dossier__overview {
  display: grid;
  grid-template-columns: minmax(0, 1.35fr) minmax(310px, 0.75fr);
  padding: 32px 0 28px;
  gap: clamp(28px, 4vw, 58px);
}
.hero-kicker {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}
.hero-kicker span {
  color: #c6523a;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.18em;
}
.hero-kicker b {
  padding: 5px 9px;
  border: 1px solid #bd6a56;
  color: #b8503d;
  font-family: 'Songti SC', serif;
  font-size: 10px;
  font-weight: 500;
  letter-spacing: 0.08em;
  transform: rotate(1deg);
}
.dossier__hero h1 {
  max-width: 720px;
  margin: 0;
  font-family: 'Songti SC', STSong, serif;
  font-size: clamp(32px, 3.5vw, 50px);
  font-weight: 600;
  line-height: 1.12;
  letter-spacing: -0.04em;
}
.hero-note {
  display: flex;
  align-items: center;
  margin-top: 16px;
  color: #60665f;
  font-size: 13px;
  gap: 10px;
}
.hero-note i {
  display: block;
  width: 28px;
  height: 1px;
  background: #d06247;
}
.quick-actions {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  margin-top: 25px;
  border-block: 1px solid #b9b4a9;
}
.quick-actions button {
  display: grid;
  grid-template-columns: 1fr auto;
  padding: 13px 11px;
  border: 0;
  border-right: 1px solid #cfcbc1;
  background: transparent;
  color: #28372f;
  font-size: 11px;
  text-align: left;
  cursor: pointer;
}
.quick-actions button:last-child {
  border-right: 0;
}
.quick-actions button:hover {
  background: #e4e7dc;
}
.quick-actions button span {
  grid-column: 1 / -1;
  margin-bottom: 7px;
  color: #b75a45;
  font:
    8px 'SFMono-Regular',
    monospace;
}
.quick-actions button b {
  color: #788078;
  font-weight: 400;
}
.notice-board {
  border: 1px solid #aeb1a8;
  background: rgba(236, 232, 220, 0.72);
}
.notice-board > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  padding: 15px 17px;
  border-bottom: 1px solid #b9b8ae;
}
.notice-board header span {
  color: #c15a43;
  font:
    8px 'SFMono-Regular',
    monospace;
  letter-spacing: 0.12em;
}
.notice-board header h2 {
  margin: 4px 0 0;
  font-family: 'Songti SC', serif;
  font-size: 18px;
}
.notice-board header button {
  border: 0;
  background: none;
  color: #657067;
  font-size: 9px;
  cursor: pointer;
}
.notice-board article {
  display: grid;
  grid-template-columns: 37px 1fr 7px;
  align-items: center;
  min-height: 62px;
  padding: 11px 15px;
  border-bottom: 1px solid #d0cdc4;
  gap: 10px;
}
.notice-board article:last-child {
  border-bottom: 0;
}
.notice-board article > span {
  padding: 4px 0;
  border: 1px solid #9ca79e;
  color: #58665d;
  font-size: 8px;
  text-align: center;
}
.notice-board article h3 {
  margin: 0;
  font-family: 'Songti SC', serif;
  font-size: 12px;
  font-weight: 600;
}
.notice-board article time {
  display: block;
  margin-top: 4px;
  color: #868980;
  font:
    8px 'SFMono-Regular',
    monospace;
}
.notice-board article i {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: transparent;
}
.notice-board article.unread i {
  background: #cd6048;
}
.dossier__metrics {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  border-block: 1px solid #92978f;
}
.dossier__metrics article {
  display: grid;
  grid-template-columns: 1fr auto;
  padding: 20px 24px;
  border-right: 1px solid #c5c4bb;
  gap: 5px;
}
.dossier__metrics article:last-child {
  border-right: 0;
}
.dossier__metrics span {
  grid-column: 1/-1;
  font:
    9px 'SFMono-Regular',
    monospace;
  letter-spacing: 0.1em;
}
.dossier__metrics strong {
  font-family: 'Songti SC', serif;
  font-size: 30px;
  font-weight: 500;
}
.dossier__metrics small {
  align-self: end;
  color: #6e766e;
  font-size: 10px;
}
.dossier__spread {
  display: grid;
  grid-template-columns: minmax(0, 1.65fr) minmax(250px, 0.7fr);
  margin-top: 32px;
  gap: clamp(36px, 5vw, 76px);
}
.section-title {
  display: grid;
  grid-template-columns: 28px auto 1fr;
  align-items: end;
  padding-bottom: 12px;
  border-bottom: 1px solid #a6a59d;
  gap: 7px;
}
.section-title > span {
  color: #d15d44;
  font: 10px monospace;
}
.section-title h2 {
  margin: 0;
  font-family: 'Songti SC', serif;
  font-size: 22px;
}
.section-title small {
  justify-self: end;
  color: #868981;
  font-size: 8px;
  letter-spacing: 0.15em;
}
.ledger__rows article {
  display: grid;
  grid-template-columns: 58px 1fr 12px auto;
  align-items: center;
  padding: 20px 2px;
  border-bottom: 1px solid #d0cbc0;
  gap: 14px;
}
.ledger time {
  font:
    11px 'SFMono-Regular',
    monospace;
}
.ledger h3,
.calendar h3 {
  margin: 0;
  font-family: 'Songti SC', serif;
  font-size: 15px;
}
.ledger p,
.calendar p {
  margin: 5px 0 0;
  color: #747872;
  font-size: 11px;
}
.ledger__mark {
  width: 7px;
  height: 7px;
  border-radius: 50%;
}
.ledger__mark.amber {
  background: #d99b37;
}
.ledger__mark.green {
  background: #56836c;
}
.ledger__mark.blue {
  background: #4d7583;
}
.ledger__mark.red {
  background: #c9523d;
}
.ledger article button {
  border: 0;
  background: none;
  color: #36463d;
  font-size: 10px;
  cursor: pointer;
}
.calendar > article {
  position: relative;
  padding: 17px 0 17px 22px;
  border-bottom: 1px solid #d0cbc0;
}
.calendar > article::before {
  position: absolute;
  top: 23px;
  left: 0;
  width: 7px;
  height: 7px;
  border: 1px solid #c9553d;
  border-radius: 50%;
  content: '';
}
.calendar time {
  color: #c6543c;
  font: 9px monospace;
}
.calendar h3 {
  margin-top: 6px;
}
.calendar footer {
  display: grid;
  margin-top: 22px;
  padding: 16px;
  background: #dce2d9;
  gap: 4px;
}
.calendar footer b {
  font-family: 'Songti SC', serif;
}
.calendar footer span {
  color: #667067;
  font-size: 10px;
}

@media (max-width: 900px) {
  .dossier__rail {
    position: static;
    width: 100%;
    padding: 18px 22px;
  }
  .dossier__nav {
    display: flex;
    margin-top: 14px;
    overflow-x: auto;
    padding-bottom: 3px;
  }
  .nav-group {
    display: contents;
  }
  .nav-group p {
    display: none;
  }
  .dossier__nav button {
    min-width: 116px;
    grid-template-columns: 30px 1fr;
    border-left: 0;
    border-bottom: 2px solid transparent;
  }
  .dossier__nav button.active {
    border-bottom-color: #dc6d4f;
  }
  .dossier__profile {
    display: none;
  }
  .coverage-badge {
    display: none;
  }
  .dossier__paper {
    margin-left: 0;
    padding: 20px 20px 100px;
  }
  .dossier__hero {
    min-width: 0;
  }
  .dossier__overview {
    grid-template-columns: 1fr;
  }
  .quick-actions {
    grid-template-columns: repeat(2, 1fr);
  }
  .dossier__metrics {
    grid-template-columns: 1fr;
  }
  .dossier__metrics article {
    border-right: 0;
    border-bottom: 1px solid #c5c4bb;
  }
  .dossier__spread {
    grid-template-columns: 1fr;
  }
  .role-tabs {
    overflow-x: auto;
  }
}
</style>
