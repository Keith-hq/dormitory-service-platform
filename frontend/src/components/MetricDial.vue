<script setup>
import { computed } from 'vue'

const props = defineProps({
  label: { type: String, required: true },
  value: { type: [String, Number], required: true },
  suffix: { type: String, default: '' },
  progress: { type: Number, default: 0 },
  caption: { type: String, default: '' },
  tone: {
    type: String,
    default: 'jade',
    validator: (value) => ['jade', 'amber', 'blue'].includes(value)
  }
})

const safeProgress = computed(() => Math.min(100, Math.max(0, Number(props.progress) || 0)))
const accessibleLabel = computed(() =>
  `${props.label}：${props.value}${props.suffix}。${props.caption}`.trim()
)
</script>

<template>
  <article
    class="metric-dial"
    :class="`metric-dial--${tone}`"
    :style="{ '--dial-progress': `${safeProgress * 3.6}deg` }"
    role="img"
    :aria-label="accessibleLabel"
  >
    <div class="metric-dial__ring" aria-hidden="true">
      <div>
        <strong>{{ value }}</strong>
        <small>{{ suffix }}</small>
      </div>
    </div>
    <div class="metric-dial__copy">
      <span>{{ label }}</span>
      <p>{{ caption }}</p>
    </div>
  </article>
</template>

<style scoped>
.metric-dial {
  --dial-color: #55b9a6;
  display: grid;
  grid-template-columns: 104px minmax(0, 1fr);
  align-items: center;
  gap: var(--space-4);
  min-width: 0;
}

.metric-dial--amber {
  --dial-color: #dfaa5a;
}

.metric-dial--blue {
  --dial-color: #70a9b7;
}

.metric-dial__ring {
  display: grid;
  width: 104px;
  height: 104px;
  place-items: center;
  border-radius: 50%;
  background: conic-gradient(
    var(--dial-color) 0 var(--dial-progress),
    rgba(255, 255, 255, 0.12) var(--dial-progress) 360deg
  );
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.12);
}

.metric-dial__ring::before {
  content: '';
  grid-area: 1 / 1;
  width: 78px;
  height: 78px;
  border-radius: 50%;
  background: #18383c;
}

.metric-dial__ring > div {
  z-index: 1;
  grid-area: 1 / 1;
  text-align: center;
}

.metric-dial__ring strong {
  color: #fffdf8;
  font-family: var(--font-display);
  font-size: 23px;
  font-variant-numeric: tabular-nums;
}

.metric-dial__ring small {
  margin-left: 2px;
  color: rgba(255, 255, 255, 0.62);
  font-size: 9px;
}

.metric-dial__copy span {
  color: #fffdf8;
  font-family: var(--font-display);
  font-size: 17px;
  font-weight: 700;
}

.metric-dial__copy p {
  margin: 6px 0 0;
  color: rgba(255, 255, 255, 0.56);
  font-size: 11px;
  line-height: 1.6;
}

@media (max-width: 720px) {
  .metric-dial {
    grid-template-columns: 88px minmax(0, 1fr);
  }

  .metric-dial__ring {
    width: 88px;
    height: 88px;
  }

  .metric-dial__ring::before {
    width: 66px;
    height: 66px;
  }

  .metric-dial__ring strong {
    font-size: 19px;
  }
}
</style>
