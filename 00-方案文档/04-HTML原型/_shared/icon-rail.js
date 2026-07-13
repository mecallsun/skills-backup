/**
 * icon-rail.js — 紧凑型图标导航条（Tier 2）
 *
 * 配套规范：37-共用页头与Tab页签导航设计规范-v2.12.md
 * 用法：在页面 <body> 内插入 <div id="icon-rail"></div>，引用此脚本即可自动渲染
 *
 * 参数说明：
 *   opts.basePath   - 子页面相对路径前缀（如 '..'）；首页使用 ''
 *   opts.currentModule - 当前页面对应模块 key（如 'dorms'）；用于高亮
 */

function renderIconRail(opts = {}) {
  const basePath = opts.basePath !== undefined ? opts.basePath : '..';
  const currentModule = opts.currentModule || '';
  const userId = getCurrentUserId();

  // 读取 Tab 列表（用于高亮"已打开"模块）
  let openModules = new Set();
  try {
    const tabsRaw = localStorage.getItem(STORAGE_KEYS.TABS(userId));
    if (tabsRaw) {
      const data = JSON.parse(tabsRaw);
      (data.tabs || []).forEach(t => t.module && openModules.add(t.module));
    }
  } catch (e) {}

  const html = ICON_RAIL_MENU.map(item => {
    const cls = item.key === currentModule ? 'active' : (openModules.has(item.key) ? 'has-tab' : '');
    const href = basePath ? `${basePath}/${item.url}` : item.url;
    return `
      <a class="icon-rail-btn ${cls}" data-module="${item.key}"
         href="${href}" title="${item.title}" aria-label="打开${item.title}">
        <i class="bi ${item.icon}"></i>
        <span class="icon-label">${item.title}</span>
      </a>
    `;
  }).join('');

  return `<nav class="icon-rail" role="navigation" aria-label="主菜单">${html}</nav>`;
}

// 页面加载时自动注入
function mountIconRail(opts) {
  const el = document.getElementById('icon-rail');
  if (el) {
    el.outerHTML = renderIconRail(opts);
  }
}