import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import App from './App.vue'
import './style.css'

const app = createApp(App)

// 状态管理
app.use(createPinia())

// 路由
app.use(router)

app.mount('#app')
