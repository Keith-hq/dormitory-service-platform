import axios from 'axios'
import { useUserStore } from '@/store/user'

const request = axios.create({
<<<<<<< HEAD
  baseURL: '/api',
=======
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
  timeout: 10000
})

// 请求拦截器：携带 JWT Token
request.interceptors.request.use(
<<<<<<< HEAD
  config => {
=======
  (config) => {
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
    const userStore = useUserStore()
    if (userStore.token) {
      config.headers.Authorization = `Bearer ${userStore.token}`
    }
    return config
  },
<<<<<<< HEAD
  error => Promise.reject(error)
=======
  (error) => Promise.reject(error)
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
)

// 响应拦截器：统一错误处理
request.interceptors.response.use(
<<<<<<< HEAD
  response => {
=======
  (response) => {
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
    const { code, message, data } = response.data
    if (code === 200 || code === 201) {
      return data
    }
    console.error(`API Error [${code}]: ${message}`)
    return Promise.reject(new Error(message || '请求失败'))
  },
<<<<<<< HEAD
  error => {
=======
  (error) => {
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
    console.error('Network Error:', error.message)
    return Promise.reject(error)
  }
)

export default request
