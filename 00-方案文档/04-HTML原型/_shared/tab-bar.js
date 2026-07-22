/**
 * tab-bar.js — Tab 页签栏（Tier 3）[v2.12.2 固定菜单切换模式]
 *
 * 配套规范：37-共用页头与Tab页签导航设计规范-v2.12.md
 *
 * ⚠️ v2.12.2 行为变更（重大）：
 *   - Tab 列表**完全固定**为 10 个，对应 10 个菜单项
 *   - 每个 Tab 的 url 直接对应菜单页面文件路径（如 dorms/list.html）
 *   - **不再有"打开新 Tab"的概念**，TabManager.open() 已废弃
 *   - 任何页面加载时，**自动根据当前 URL 推断激活哪个 Tab**
 *   - 默认激活首页（若 URL 不匹配任何菜单项）
 *   - 禁止关闭（按钮已隐藏，键盘 Ctrl+W 已禁用）
 *
 * Tab 与菜单的对应关系（与 icon-rail.js ICON_RAIL_MENU 一致）：
 *   首页 → index.html
 *   办理登记 → booking/index.html
 *   宿舍管理 → dorms/list.html
 *   人员清单 → personnel/list.html
 *   费用标准 → billing/standards.html
 *   宿舍账单 → billing/dorm-bills.html
 *   员工账单 → billing/employee-bills.html
 *   智能抄表 → meter/index.html
 *   基础资料 → basics/index.html
 *   系统设置 → settings/index.html
 */

// 固定的 10 个 Tab（对应 10 个菜单项）
const FIXED_TABS = [
  { id: 'tab-home',          title: '首页',       module: 'index',          icon: 'bi-speedometer2',      url: 'index.html' },
  { id: 'tab-booking',       title: '办理登记',   module: 'booking',        icon: 'bi-clipboard-check',   url: 'booking/index.html' },
  { id: 'tab-dorms',         title: '宿舍管理',   module: 'dorms',          icon: 'bi-building',          url: 'dorms/list.html' },
  { id: 'tab-personnel',     title: '人员清单',   module: 'personnel',      icon: 'bi-people-fill',       url: 'personnel/list.html' },
  { id: 'tab-billing',       title: '费用标准',   module: 'billing',        icon: 'bi-cash-stack',        url: 'billing/standards.html' },
  { id: 'tab-dorm-bills',    title: '宿舍账单',   module: 'dorm-bills',     icon: 'bi-receipt',           url: 'billing/dorm-bills.html' },
  { id: 'tab-employee-bills',title: '员工账单',   module: 'employee-bills', icon: 'bi-wallet2',           url: 'billing/employee-bills.html' },
  { id: 'tab-meter',         title: '智能抄表',   module: 'meter',          icon: 'bi-clipboard-data',    url: 'meter/index.html' },
  { id: 'tab-basics',        title: '基础资料',   module: 'basics',         icon: 'bi-database',          url: 'basics/index.html' },
  { id: 'tab-settings',      title: '系统设置',   module: 'settings',       icon: 'bi-gear',              url: 'settings/index.html' }
];

/**
 * 根据当前 URL 推断应该激活哪个 Tab
 * 规则：URL 包含某 Tab 的模块路径前缀则匹配该 Tab
 * 例如：dorms/details.html → tab-dorms（因为包含 /dorms/）
 *       booking/check-in.html → tab-booking
 * @param {string} currentUrl - 当前页面 URL（相对路径，如 dorms/details.html）
 * @param {string} basePath - 基础路径前缀（首页 ''，子页面 '..'）
 * @returns {string} 激活的 Tab ID
 */
function inferActiveTabId(currentUrl, basePath) {
  if (!currentUrl) return 'tab-home';

  // 移除 basePath 前缀以简化匹配
  let url = currentUrl;
  if (basePath && url.startsWith(basePath + '/')) {
    url = url.substring(basePath.length + 1);
  }

  // 按路径前缀匹配（精确优先级，先精确后通配）
  if (url === 'index.html')                        return 'tab-home';
  if (url.startsWith('booking/'))                  return 'tab-booking';
  if (url.startsWith('dorms/'))                    return 'tab-dorms';
  if (url.startsWith('personnel/'))                return 'tab-personnel';
  // billing 子模块：standards → billing，dorm-bills → dorm-bills，employee-bills → employee-bills
  if (url.startsWith('billing/employee-bills'))    return 'tab-employee-bills';
  if (url.startsWith('billing/dorm-bills'))        return 'tab-dorm-bills';
  if (url.startsWith('billing/standards'))         return 'tab-billing';
  if (url.startsWith('billing/standard-form'))     return 'tab-billing';  // 表单页归属"费用标准"Tab
  if (url.startsWith('meter/'))                    return 'tab-meter';
  if (url.startsWith('basics/'))                   return 'tab-basics';
  if (url.startsWith('settings/'))                 return 'tab-settings';

  // 默认首页
  return 'tab-home';
}

const TabManager = {
  // 加载激活 Tab ID
  loadActiveId() {
    const userId = getCurrentUserId();
    try {
      const raw = localStorage.getItem(STORAGE_KEYS.ACTIVE_TAB(userId));
      if (raw) {
        const data = JSON.parse(raw);
        return data.activeTabId || 'tab-home';
      }
    } catch (e) {}
    return 'tab-home';
  },

  // 保存激活 Tab ID
  saveActiveId(activeTabId) {
    const userId = getCurrentUserId();
    localStorage.setItem(STORAGE_KEYS.ACTIVE_TAB(userId), JSON.stringify({ activeTabId }));
  },

  // ⚠️ v2.12.2 open() 已废弃 — Tab 固定为 10 个菜单项
  // 保留方法签名以兼容旧代码，但操作不生效（仅保存激活 Tab ID）
  open(tab) {
    console.warn('[TabManager.open] v2.12.2 起 Tab 固定为 10 个菜单项，不再支持打开新 Tab');
    // 仅根据 URL 推断并保存激活 Tab
    if (tab && tab.url) {
      const inferredId = inferActiveTabId(tab.url, '');
      this.saveActiveId(inferredId);
      return inferredId;
    }
    return 'tab-home';
  },

  // ⚠️ v2.12.2 close() 已禁用
  close(tabId) {
    console.warn('[TabManager.close] v2.12.2 起 Tab 禁止关闭（固定菜单模式）');
    return null;
  },

  closeOthers(tabId) {
    console.warn('[TabManager.closeOthers] v2.12.2 起 Tab 禁止关闭');
    return null;
  },

  // 切换激活 Tab（仅保存，不修改 Tab 列表）
  activate(tabId) {
    if (FIXED_TABS.find(t => t.id === tabId)) {
      this.saveActiveId(tabId);
    }
    return { activeTabId: tabId, tabs: FIXED_TABS };
  },

  // 清除 Tab 缓存（重置为首页激活）
  clear() {
    const userId = getCurrentUserId();
    localStorage.removeItem(STORAGE_KEYS.TABS(userId));
    localStorage.removeItem(STORAGE_KEYS.ACTIVE_TAB(userId));
  }
};

// HTML 转义辅助
function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, c => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  }[c]));
}

// 渲染 Tab 栏（固定 10 个 Tab，禁止关闭）
function renderTabBar(opts = {}) {
  const basePath = opts.basePath !== undefined ? opts.basePath : '..';
  const currentUrl = opts.currentUrl || '';
  const currentModule = opts.currentModule || '';

  // 推断激活 Tab ID（优先级：显式传入 currentModule > URL 推断）
  let activeId;
  if (currentModule) {
    const matched = FIXED_TABS.find(t => t.module === currentModule);
    activeId = matched ? matched.id : inferActiveTabId(currentUrl, basePath);
  } else {
    activeId = inferActiveTabId(currentUrl, basePath);
  }

  // 保存激活 Tab ID（持久化）
  TabManager.saveActiveId(activeId);

  const itemsHtml = FIXED_TABS.map(tab => {
    const cls = tab.id === activeId ? 'active' : '';
    // 生成完整 URL（带 basePath 前缀）
    const fullUrl = basePath ? `${basePath}/${tab.url}` : tab.url;
    return `
      <div class="tab-item ${cls}" data-tab-id="${tab.id}" data-module="${tab.module}" data-url="${fullUrl}"
           role="tab" aria-selected="${tab.id === activeId}" tabindex="0" title="${escapeHtml(tab.title)}">
        <i class="bi ${tab.icon} tab-icon"></i>
        <span class="tab-title">${escapeHtml(tab.title)}</span>
      </div>
    `;
  }).join('');

  return `
    <div class="tab-bar" role="tablist" aria-label="菜单项切换">
      ${itemsHtml}
    </div>
  `;
}

// 绑定 Tab 栏事件（仅保留切换功能）
function bindTabBarEvents(opts = {}) {
  const basePath = opts.basePath !== undefined ? opts.basePath : '..';

  // 点击 Tab（切换到对应菜单页面）
  document.querySelectorAll('.tab-item').forEach(el => {
    el.addEventListener('click', (e) => {
      const tabId = el.dataset.tabId;
      const url = el.dataset.url;
      TabManager.activate(tabId);
      window.location.href = url;
    });
  });

  // 键盘快捷键：Ctrl+1~9 / Ctrl+0 切换前 10 个 Tab
  document.addEventListener('keydown', (e) => {
    if (!(e.ctrlKey || e.metaKey)) return;

    // Ctrl+1~9 对应 FIXED_TABS[0..8]，Ctrl+0 对应 FIXED_TABS[9]
    let idx = -1;
    if (e.key >= '1' && e.key <= '9') {
      idx = parseInt(e.key) - 1;
    } else if (e.key === '0') {
      idx = 9;
    }

    if (idx >= 0 && idx < FIXED_TABS.length) {
      e.preventDefault();
      const tab = FIXED_TABS[idx];
      TabManager.activate(tab.id);
      const url = basePath ? `${basePath}/${tab.url}` : tab.url;
      window.location.href = url;
    }

    // ⚠️ v2.12.2 移除 Ctrl+W 关闭快捷键（Tab 禁止关闭）
  });
}

// 挂载 Tab 栏
function mountTabBar(opts) {
  const el = document.getElementById('tab-bar');
  if (!el) return;

  // ⚠️ v2.13.52 设计决策：所有页面（包括 /profile/）统一渲染 Tab 栏
  // 个人中心页面保留顶部品牌栏 + 10 主菜单 Tab 导航，与全站 26 个 Razor/HTML 保持一致
  // 详见 105-Profile原型保留主菜单导航设计-v2.13.52.md
  el.outerHTML = renderTabBar(opts);
  bindTabBarEvents(opts);
}