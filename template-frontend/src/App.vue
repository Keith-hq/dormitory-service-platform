<script setup></script>

<template>
  <div id="app-container">
    <header class="app-header">
      <h1>🏠 宿舍服务管理平台</h1>
      <nav>
        <router-link to="/building">楼栋管理</router-link>
      </nav>
    </header>
    <main>
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.app-header {
  background: #001529;
  color: #fff;
  padding: 12px 24px;
  display: flex;
  align-items: center;
  gap: 32px;
}
.app-header h1 {
  font-size: 18px;
  margin: 0;
}
.app-header a {
  color: rgba(255, 255, 255, 0.75);
  text-decoration: none;
  font-size: 14px;
}
.app-header a:hover,
.app-header a.router-link-active {
  color: #fff;
}
main {
  min-height: calc(100vh - 52px);
  background: #f0f2f5;
}
</style>
