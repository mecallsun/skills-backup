/**
 * storage-keys.js — localStorage Key 常量定义
 *
 * 配套规范：37-共用页头与Tab页签导航设计规范-v2.12.md
 * 作用：统一所有 localStorage key 命名，避免冲突
 * 跨账号隔离规则：包含 userId 后缀
 */

const STORAGE_KEYS = {
  // Tab 列表（共用页头 + Tab 页签切换模式）
  TABS: (userId) => `dormmanage:tabs:v1:${userId || 'guest'}`,

  // 激活 Tab ID
  ACTIVE_TAB: (userId) => `dormmanage:activeTab:v1:${userId || 'guest'}`,

  // 筛选条件缓存（v2.11.5 引入，v2.12.10 增强）
  FILTER_CACHE: (module, userId) => `dormmanage:filter:v1:${module}:${userId || 'guest'}`,

  // 模块页面路径 → ModuleKey 映射（v2.12.10 新增，与 filter-persistence.js 一致）
  FILTER_MODULE_MAP: {
    'dorms/list.html':           'dorms',
    'personnel/list.html':       'personnel',
    'booking/index.html':        'booking',
    'billing/dorm-bills.html':   'dormBills',
    'billing/employee-bills.html':'employeeBills',
    'billing/standards.html':    'billing',
    'meter/index.html':          'meter'
  },

  // 用户偏好设置（存储筛选条件开关等）
  USER_PREFS: (userId) => `dormmanage:prefs:v1:${userId || 'guest'}`,

  // 当前登录用户信息（演示用）
  CURRENT_USER: () => `dormmanage:currentUser:v1`
};

// 当前登录用户 ID（演示用，固定为 admin）
function getCurrentUserId() {
  try {
    const u = localStorage.getItem(STORAGE_KEYS.CURRENT_USER());
    if (u) return JSON.parse(u).id || 'admin';
  } catch (e) {}
  return 'admin';
}

// Tab 数量上限
const TAB_MAX_COUNT = 15;

// 模块到 Tab 默认图标的映射
const MODULE_ICON_MAP = {
  'index':      { icon: 'bi-house-door-fill', color: '#1976d2', title: '首页' },
  'booking':    { icon: 'bi-clipboard-check', color: '#1976d2', title: '办理登记' },
  'dorms':      { icon: 'bi-building',        color: '#1976d2', title: '宿舍管理' },
  'personnel':  { icon: 'bi-people-fill',     color: '#2e7d32', title: '人员清单' },
  'billing':    { icon: 'bi-cash-stack',      color: '#e65100', title: '费用' },
  'meter':      { icon: 'bi-clipboard-data',  color: '#00838f', title: '智能抄表' },
  'basics':     { icon: 'bi-database',        color: '#546e7a', title: '基础资料' },
  'settings':   { icon: 'bi-gear',            color: '#546e7a', title: '系统设置' },
  'profile':    { icon: 'bi-person-circle',   color: '#7b1fa2', title: '个人中心' }
};

// 一级菜单（紧凑型图标导航条）
const ICON_RAIL_MENU = [
  { key: 'index',     icon: 'bi-speedometer2',      title: '首页',       url: 'index.html' },
  { key: 'booking',   icon: 'bi-clipboard-check',   title: '办理登记',   url: 'booking/index.html' },
  { key: 'dorms',     icon: 'bi-building',          title: '宿舍管理',   url: 'dorms/list.html' },
  { key: 'personnel', icon: 'bi-people-fill',       title: '人员清单',   url: 'personnel/list.html' },
  { key: 'billing',   icon: 'bi-cash-stack',        title: '费用标准',   url: 'billing/standards.html' },
  { key: 'dorm-bills',icon: 'bi-receipt',           title: '宿舍账单',   url: 'billing/dorm-bills.html' },
  { key: 'employee-bills', icon: 'bi-wallet2',     title: '员工账单',   url: 'billing/employee-bills.html' },
  { key: 'meter',     icon: 'bi-clipboard-data',    title: '智能抄表',   url: 'meter/index.html' },
  { key: 'basics',    icon: 'bi-database',          title: '基础资料',   url: 'basics/index.html' },
  { key: 'settings',  icon: 'bi-gear',              title: '系统设置',   url: 'settings/index.html' }
];