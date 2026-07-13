/**
 * filter-persistence.js — 筛选条件 localStorage 持久化通用模块
 *
 * 配套规范：35-列表页面统一UI设计规范-v2.11.4.md §7.7
 *
 * 行为：
 * 1. 页面加载时自动恢复 localStorage 中的筛选值到 DOM
 * 2. 筛选字段变化时自动写入 localStorage
 * 3. 提供 resetFilters() 函数供"重置"按钮调用
 * 4. 提供 clearAll() 函数供"清除全部缓存"按钮调用
 *
 * 使用方式：
 *   在每个列表页的 </body> 前引入：<script src="../_shared/filter-persistence.js"></script>
 *   重置按钮：<button onclick="FilterPersistence.resetFilters('moduleKey')">
 */

(function() {
  'use strict';

  // 模块页面路径 → ModuleKey 映射（与 storage-keys.js MODULE_ICON_MAP 一致）
  var MODULE_KEY_MAP = {
    'dorms/list.html':           'dorms',
    'personnel/list.html':       'personnel',
    'booking/index.html':        'booking',
    'billing/dorm-bills.html':   'dormBills',
    'billing/employee-bills.html':'employeeBills',
    'billing/standards.html':    'billing',
    'meter/index.html':          'meter'
  };

  /**
   * 获取当前页面的 ModuleKey
   * 从 URL 中提取相对路径（如 dorms/list.html），映射到 ModuleKey
   */
  function getModuleKey() {
    var pathname = window.location.pathname;
    // 匹配最后一个 / 和 .html 之间的完整路径（如 booking/index.html）
    // 原正则 /([^/?#]+\.html)/ 只匹配最后一个 / 后的部分（如 index.html），导致子目录页面失效
    var match = pathname.match(/([^/?#]+\/[^/?#]+\.html)/);
    if (!match) return null;
    return MODULE_KEY_MAP[match[1]] || null;
  }

  /**
   * 获取 localStorage key
   */
  function getStorageKey(moduleKey) {
    var userId = 'admin'; // 当前登录用户（与 tab-bar.js 一致）
    return 'dormmanage:filter:v1:' + moduleKey + ':' + userId;
  }

  /**
   * 从 localStorage 恢复筛选值到 DOM
   */
  function restoreFilters(moduleKey) {
    var key = getStorageKey(moduleKey);
    try {
      var stored = localStorage.getItem(key);
      if (!stored) return;
      var values = JSON.parse(stored);
      Object.keys(values).forEach(function(fieldId) {
        var el = document.getElementById(fieldId);
        if (el) {
          el.value = values[fieldId];
        }
      });
    } catch (e) {
      console.warn('[filter-persistence] 恢复筛选值失败:', e);
    }
  }

  /**
   * 将当前 DOM 所有筛选值写入 localStorage
   */
  function saveFilters(moduleKey) {
    var key = getStorageKey(moduleKey);
    var values = {};
    // 收集所有筛选区内的 input/select 值
    var filterCard = document.querySelector('.filter-card, .filter-row, form.filter-row');
    if (!filterCard) return;
    var inputs = filterCard.querySelectorAll('input, select');
    inputs.forEach(function(el) {
      if (el.id) {
        values[el.id] = el.value;
      }
    });
    try {
      localStorage.setItem(key, JSON.stringify(values));
    } catch (e) {
      console.warn('[filter-persistence] 保存筛选值失败:', e);
    }
  }

  /**
   * 重置筛选条件（清空 localStorage + 重置 DOM）
   */
  function resetFilters(moduleKey) {
    var key = getStorageKey(moduleKey);
    localStorage.removeItem(key);
    // 重置所有筛选字段为空
    var filterCard = document.querySelector('.filter-card, .filter-row, form.filter-row');
    if (!filterCard) return;
    filterCard.querySelectorAll('input, select').forEach(function(el) {
      if (el.id) el.value = '';
    });
  }

  /**
   * 绑定筛选字段变化监听
   */
  function bindFilterEvents(moduleKey) {
    var filterCard = document.querySelector('.filter-card, .filter-row, form.filter-row');
    if (!filterCard) return;
    // input 事件：文本输入时实时保存
    filterCard.addEventListener('input', function() { saveFilters(moduleKey); });
    // change 事件：select/date 等选择变化时保存
    filterCard.addEventListener('change', function() { saveFilters(moduleKey); });
  }

  /**
   * 初始化：恢复 + 监听
   */
  function init() {
    var moduleKey = getModuleKey();
    if (!moduleKey) {
      FilterPersistence._initialized = true;
      return;
    }
    restoreFilters(moduleKey);
    bindFilterEvents(moduleKey);
    // v2.11.22 新增：暴露初始化标志 + 重渲染钩子
    FilterPersistence._initialized = true;
    FilterPersistence._moduleKey = moduleKey;
    // 触发 onRestore 回调（供页面在恢复筛选后重新渲染）
    if (typeof FilterPersistence.onRestore === 'function') {
      try { FilterPersistence.onRestore(); } catch(e) { console.warn('[filter-persistence] onRestore error:', e); }
    }
  }

  // 暴露全局 API
  window.FilterPersistence = {
    init: init,
    _initialized: false,    // v2.11.22 新增：初始化完成标志
    _moduleKey: null,       // v2.11.22 新增：当前模块 key
    onRestore: null,        // v2.11.22 新增：恢复完成后回调
    resetFilters: resetFilters,
    clearAll: function() {
      // 清除所有模块的筛选缓存
      Object.keys(MODULE_KEY_MAP).forEach(function(pagePath) {
        var mk = MODULE_KEY_MAP[pagePath];
        var key = getStorageKey(mk);
        localStorage.removeItem(key);
      });
    }
  };

  // 页面加载完成后自动初始化
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
