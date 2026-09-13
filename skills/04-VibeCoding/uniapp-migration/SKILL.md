---
name: uniapp-migration
description: ZEEHUA 宿舍管理系统 uni-app 移动端 1:1 迁移技能，将 v3.0.8.0 旧版移动端迁移到 v3.3.7+ 新版 uni-app 3.x + Vue 3，包含页面骨架、API 层、状态管理、路由配置。
license: MIT
ai_tier: tier-2
domain: mobile-development
language: typescript,javascript
stage: migration
output: skill-document
complexity: medium
tags:
  - uni-app
  - vue3
  - mobile
  - migration
  - zeehua
version: "1.0"
created: "2026-08-14"
---

# uni-app 移动端迁移技能

## 概述

本技能用于 ZEEHUA 宿舍管理系统移动端的迁移和开发，涵盖 uni-app 3.x + Vue 3 的完整迁移流程。

## 项目结构

```
ZEEHUA.UniApp-Vue/
├── src/
│   ├── App.vue                 # 应用入口
│   ├── main.ts                 # 主入口（Pinia + Vue Router）
│   ├── pages.json              # 路由配置（48 个路由）
│   ├── api/
│   │   └── modules.ts         # 集中 API 层（100+ API）
│   ├── pages/                  # 页面目录
│   │   ├── personnel/           # 人员清单
│   │   ├── dorms/             # 宿舍管理
│   │   ├── meter/             # 智能抄表
│   │   ├── booking/            # 办理登记
│   │   ├── mybills/           # 我的账单
│   │   ├── dormbills/         # 宿舍账单
│   │   ├── dashboard/          # 首页看板
│   │   └── pda/               # PDA 配置
│   ├── static/                 # 静态资源
│   ├── store/                  # Pinia 状态管理
│   │   ├── auth.ts            # 认证状态
│   │   └── ...
│   └── utils/
│       ├── request.ts         # HTTP 封装（JWT + 自动刷新）
│       └── storage.ts         # 本地存储（zh_ 前缀）
└── package.json
```

## 路由配置（pages.json）

```json
{
  "pages": [
    { "path": "pages/personnel/index" },
    { "path": "pages/dorms/index" },
    { "path": "pages/meter/index" },
    { "path": "pages/booking/index" },
    { "path": "pages/mybills/index" },
    { "path": "pages/dormbills/index" },
    { "path": "pages/dashboard/index" },
    { "path": "pages/pda/index" }
  ],
  "subPackages": [
    { "root": "pages/personnel", "pages": ["detail"] },
    { "root": "pages/dorms", "pages": ["detail"] },
    { "root": "pages/meter", "pages": ["detail", "entry"] }
  ],
  "globalStyle": {
    "navigationBarTitleText": "ZEEHUA 宿舍管理"
  }
}
```

## API 层规范

### 请求封装（request.ts）

```typescript
// JWT + 自动刷新 + L1 发现机制
const request = (options: UniApp.RequestOptions) => {
  return new Promise((resolve, reject) => {
    const token = uni.getStorageSync('accessToken')

    uni.request({
      ...options,
      header: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
        ...options.header
      },
      success: (res) => {
        if (res.statusCode === 401) {
          // 自动刷新 Token
          refreshToken().then(() => {
            request(options).then(resolve).catch(reject)
          })
          return
        }
        resolve(res.data)
      },
      fail: reject
    })
  })
}
```

### API 模块（modules.ts）

```typescript
// 集中 API 层，按模块分组
export const personnelApi = {
  list: (params: any) => request({ url: '/api/v2/personnel', data: params }),
  detail: (id: number) => request({ url: `/api/v2/personnel/${id}` }),
  create: (data: any) => request({ url: '/api/v2/personnel', method: 'POST', data }),
  update: (id: number, data: any) => request({ url: `/api/v2/personnel/${id}`, method: 'PUT', data }),
  delete: (id: number) => request({ url: `/api/v2/personnel/${id}`, method: 'DELETE' }),
}

export const dormsApi = {
  list: (params: any) => request({ url: '/api/v2/dorms', data: params }),
  detail: (id: number) => request({ url: `/api/v2/dorms/${id}` }),
}

export const meterApi = {
  list: (params: any) => request({ url: '/api/v2/meter', data: params }),
  submit: (data: any) => request({ url: '/api/v2/meter', method: 'POST', data }),
}

export const bookingApi = {
  list: (params: any) => request({ url: '/api/v2/bookings', data: params }),
  create: (data: any) => request({ url: '/api/v2/bookings', method: 'POST', data }),
  checkout: (id: number) => request({ url: `/api/v2/bookings/${id}/checkout`, method: 'POST' }),
}
```

## 页面模板

### 标准列表页

```vue
<template>
  <view class="container">
    <!-- 筛选区 -->
    <view class="filter-bar">
      <input v-model="keyword" placeholder="搜索..." @confirm="search" />
      <button @click="search">查询</button>
      <button @click="reset">重置</button>
    </view>

    <!-- 列表 -->
    <scroll-view scroll-y @scrolltolower="loadMore">
      <view v-for="item in list" :key="item.id" class="list-item">
        <text>{{ item.name }}</text>
        <text class="status">{{ item.statusText }}</text>
      </view>

      <view v-if="loading" class="loading">加载中...</view>
      <view v-if="noMore" class="no-more">没有更多了</view>
    </scroll-view>

    <!-- 操作按钮 -->
    <view class="action-bar">
      <button type="primary" @click="onAdd">新增</button>
    </view>
  </view>
</template>

<script setup lang="ts">
import { ref, onLoad } from 'vue'
import { personnelApi } from '@/api/modules'

const list = ref<any[]>([])
const keyword = ref('')
const page = ref(1)
const pageSize = ref(10)
const loading = ref(false)
const noMore = ref(false)

const search = async () => {
  page.value = 1
  noMore.value = false
  await fetchList()
}

const fetchList = async () => {
  loading.value = true
  try {
    const res = await personnelApi.list({
      keyword: keyword.value,
      page: page.value,
      pageSize: pageSize.value
    })
    if (page.value === 1) {
      list.value = res.data
    } else {
      list.value.push(...res.data)
    }
    noMore.value = res.data.length < pageSize.value
  } finally {
    loading.value = false
  }
}

const loadMore = () => {
  if (!noMore.value && !loading.value) {
    page.value++
    fetchList()
  }
}

const onAdd = () => {
  uni.navigateTo({ url: '/pages/personnel/detail' })
}

onLoad(() => {
  search()
})
</script>
```

## 认证流程

### 登录

```typescript
// store/auth.ts
export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: uni.getStorageSync('accessToken') || '',
    userInfo: null
  }),

  actions: {
    async login(username: string, password: string) {
      const res = await request({
        url: '/api/v2/auth/login',
        method: 'POST',
        data: { username, password }
      })
      this.token = res.accessToken
      uni.setStorageSync('accessToken', res.accessToken)
      await this.fetchUserInfo()
    },

    async fetchUserInfo() {
      const res = await request({ url: '/api/v2/auth/me' })
      this.userInfo = res.data
    },

    logout() {
      this.token = ''
      this.userInfo = null
      uni.removeStorageSync('accessToken')
    }
  }
})
```

## 编译命令

```bash
# H5 编译
npm run build:h5

# 微信小程序编译
npm run build:mp-weixin

# Android APK（Capacitor）
npx cap sync android
cd android && ./gradlew assembleDebug
```

## 发布目录

| 平台 | 产物位置 |
|------|---------|
| H5 | `publish/latest/UniApp-H5/` |
| APK | `publish/latest/UniApp/ZEEHUA-v{version}.apk` |
| 小程序 | `publish/latest/uniapp-mp-weixin/` |

## 注意事项

1. **iOS 永久禁止**：未经用户明确指令禁止编译 iOS/IPA/macOS
2. **API 版本**：统一使用 `/api/v2/` 前缀
3. **Token 处理**：JWT Bearer 认证，自动刷新机制
4. **存储前缀**：统一使用 `zh_` 前缀
5. **图标**：使用 Unicode emoji + uni-icons，不使用 element-icons

## 角色权限

| 角色 | 可访问模块 |
|------|-----------|
| meter_reader | 智能抄表、人员清单、个人中心 |
| resident | 宿舍账单、个人中心、办理登记 |
| applicant | 申请住宿、个人中心 |

## 常见问题

### Q: 如何新增页面？
1. 在 `pages.json` 中添加路由
2. 创建对应的 `.vue` 文件
3. 在 `api/modules.ts` 中添加 API
4. 编译验证

### Q: API 返回 401 怎么处理？
A: 已实现自动刷新 Token 机制，无需手动处理

### Q: 如何调试？
A: 使用 H5 模式 `npm run dev:h5`，微信开发者工具导入 `dist/build/mp-weixin`
