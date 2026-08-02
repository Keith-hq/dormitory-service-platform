import axios from 'axios'
import { useUserStore } from '@/store/user'

const request = axios.create({
  baseURL: '/api',
  timeout: 10000
})

// 请求拦截器：携带 JWT Token
request.interceptors.request.use(
  config => {
    const userStore = useUserStore()
    if (userStore.token) {
      config.headers.Authorization = `Bearer ${userStore.token}`
    }
    return config
  },
  error => Promise.reject(error)
)

// 响应拦截器：统一错误处理
request.interceptors.response.use(
  response => {
    const { code, message, data } = response.data
    if (code === 200 || code === 201) {
      return data
    }
    console.error(`API Error [${code}]: ${message}`)
    return Promise.reject(new Error(message || '请求失败'))
  },
  error => {
    console.error('Network Error:', error.message)
    return Promise.reject(error)
  }
)

export default request
