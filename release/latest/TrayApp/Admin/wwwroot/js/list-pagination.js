/*!
 * list-pagination.js — v2.13.132 + v2.13.151 防回归加固
 * 列表统一分页客户端组件（设备记录 / 设备档案 / 任何 AJAX 加载的列表）
 *
 * 设计原则：
 * ① 与 _PaginationPartial 服务端版本完全对齐（视觉/交互/数据格式） — 一行分页器
 * ② pageSize dropdown 选项：10/20/50/100（默认 10）
 * ③ 翻页数字：仅当 totalPages > 1 时显示
 * ④ 统计：共 N 条 · 第 X-Y 条 · 共 Z 页
 * ⑤ 调用方式：window.listPager.update('equipmentreading', page, pageSize, totalCount, fnPage)
 *           或 window.listPager.safeUpdate(...) 防回归包装
 *
 * v2.13.151 防回归加固（用户报告「加载失败，请刷新重试」反复出现）：
 * - safeUpdate(...) wrapper：检查 window.listPager 是否就绪，未就绪则 console.error 输出根因
 * - safeElement(id)：DOM 元素缺失时给出具体哪个 ID 缺失
 * - 所有调用统一经过 safeUpdate 而非裸调 window.listPager.update
 * - 不再 throw（避免被调用方 catch 块显示通用「加载失败」掩盖真因）
 *
 * 兼容性：
 * 旧代码调用的 window.updatePagination(...) 仍保留为兼容入口（与新接口同实现）
 */

(function () {
    'use strict';

    // 模块级页大小存储（localStorage 持久化跨刷新）
    var STORAGE_KEY = 'dormmanage.listPager.pageSize';

    function getStoredPageSize() {
        try {
            var v = parseInt(localStorage.getItem(STORAGE_KEY), 10);
            return (v === 10 || v === 20 || v === 50 || v === 100) ? v : 10;
        } catch (e) { return 10; }
    }

    function setStoredPageSize(size) {
        try { localStorage.setItem(STORAGE_KEY, String(size)); } catch (e) { /* 忽略 */ }
    }

    /**
     * 构造 « 1 2 3 … 65 » HTML
     */
    function buildPageLinks(page, totalPages, fnName) {
        var html = '';
        html += '<li class="page-item ' + (page <= 1 ? 'disabled' : '') + '">';
        html += '<a class="page-link" href="javascript:;" data-page="' + (page - 1) + '">&laquo;</a></li>';

        var win = 2;
        var start = Math.max(1, page - win);
        var end = Math.min(totalPages, page + win);

        if (start > 1) {
            html += '<li class="page-item"><a class="page-link" href="javascript:;" data-page="1">1</a></li>';
            if (start > 2) {
                html += '<li class="page-item disabled"><span class="page-link">…</span></li>';
            }
        }
        for (var i = start; i <= end; i++) {
            html += '<li class="page-item ' + (i === page ? 'active' : '') + '">';
            html += '<a class="page-link" href="javascript:;" data-page="' + i + '">' + i + '</a></li>';
        }
        if (end < totalPages) {
            if (end < totalPages - 1) {
                html += '<li class="page-item disabled"><span class="page-link">…</span></li>';
            }
            html += '<li class="page-item"><a class="page-link" href="javascript:;" data-page="' + totalPages + '">' + totalPages + '</a></li>';
        }

        html += '<li class="page-item ' + (page >= totalPages ? 'disabled' : '') + '">';
        html += '<a class="page-link" href="javascript:;" data-page="' + (page + 1) + '">&raquo;</a></li>';

        return html;
    }

    /**
     * 渲染到指定模块的 footer 容器
     * v2.13.151：增加 container 缺失时 console.warn 不 throw（避免触发调用方 catch 块）
     */
    function update(moduleKey, page, pageSize, totalCount, onPageChange) {
        if (typeof moduleKey !== 'string' || !moduleKey) {
            console.error('[v2.13.151 list-pagination] moduleKey 无效:', moduleKey);
            return;
        }
        var container = document.querySelector('.list-pager[data-module="' + moduleKey + '"]');
        if (!container) {
            container = document.querySelector('#pane-' + moduleKey + ' .pagination-footer, .pagination-footer[data-module="' + moduleKey + '"]');
        }
        if (!container) {
            // v2.13.151：仅 console.warn，不 throw（让业务函数能继续渲染表格）
            console.warn('[v2.13.151 list-pagination] 找不到模块 ' + moduleKey + ' 的 footer 容器（页面可能尚未渲染或 Tab 未激活）');
            return;
        }

        // 参数类型校验（防止 undefined 引起 NaN）
        page = Number(page) || 1;
        pageSize = Number(pageSize) || getStoredPageSize();
        totalCount = Number(totalCount) || 0;

        if (typeof pageSize === 'number' && pageSize > 0) {
            setStoredPageSize(pageSize);
        }

        var total = totalCount;
        var start = total === 0 ? 0 : (page - 1) * pageSize + 1;
        var end = Math.min(page * pageSize, total);
        var totalPages = Math.max(1, Math.ceil(total / Math.max(pageSize, 1)));

        var html = ''
            + '<div class="card-footer d-flex justify-content-between align-items-center list-pager-body" style="background: #f8f9fa; border-top: 1px solid #e9ecef; padding: 8px 16px;">'
            + '  <div class="d-flex align-items-center gap-2">'
            + '    <span class="text-muted small">'
            + '      <i class="bi bi-list-ol"></i> 共 <strong>' + total + '</strong> 条 ·'
            + '      第 <strong>' + start + '-' + end + '</strong> 条 ·'
            + '      共 <strong>' + totalPages + '</strong> 页'
            + '    </span>'
            + '    <span class="text-muted small ms-2">|</span>'
            + '    <span class="text-muted small">每页</span>'
            + '    <select class="form-select form-select-sm page-size-select" style="width: 70px;">'
            + '      <option value="10"' + (pageSize === 10 ? ' selected' : '') + '>10</option>'
            + '      <option value="20"' + (pageSize === 20 ? ' selected' : '') + '>20</option>'
            + '      <option value="50"' + (pageSize === 50 ? ' selected' : '') + '>50</option>'
            + '      <option value="100"' + (pageSize === 100 ? ' selected' : '') + '>100</option>'
            + '    </select>'
            + '    <span class="text-muted small">条</span>'
            + '  </div>';

        if (totalPages > 1) {
            html += '  <nav><ul class="pagination pagination-sm mb-0 list-pager-pages"></ul></nav>';
        }
        html += '</div>';

        container.innerHTML = html;

        var pagesUl = container.querySelector('.list-pager-pages');
        if (pagesUl) {
            pagesUl.innerHTML = buildPageLinks(page, totalPages, moduleKey);
            pagesUl.addEventListener('click', function (e) {
                var target = e.target;
                if (target && target.tagName === 'A' && target.hasAttribute('data-page')) {
                    var p = parseInt(target.getAttribute('data-page'), 10);
                    if (!isNaN(p) && p >= 1 && p <= totalPages && typeof onPageChange === 'function') {
                        onPageChange(p, pageSize);
                    }
                }
            });
        }

        var sel = container.querySelector('.page-size-select');
        if (sel) {
            sel.addEventListener('change', function () {
                var newSize = parseInt(this.value, 10);
                if (!isNaN(newSize) && typeof onPageChange === 'function') {
                    onPageChange(1, newSize);
                }
            });
        }
    }

    /**
     * v2.13.151 防回归关键方法：safeUpdate
     * 调用方统一通过此方法调用 update，避免直接调用 window.listPager.update(...) 在脚本未加载完成时抛 TypeError
     */
    function safeUpdate(moduleKey, page, pageSize, totalCount, onPageChange) {
        if (typeof window.listPager === 'undefined' || typeof window.listPager.update !== 'function') {
            console.error('[v2.13.151 list-pagination.safeUpdate] window.listPager 未就绪！可能原因：(1) list-pagination.js 未加载 (2) JS 执行顺序错误 (3) 页面 _Layout 未包含此脚本。moduleKey=' + (moduleKey || 'unknown') + ' 请检查 ~/js/list-pagination.js 是否在页面 <script> 中');
            return false;
        }
        try {
            window.listPager.update(moduleKey, page, pageSize, totalCount, onPageChange);
            return true;
        } catch (err) {
            // 即使 listPager.update 内部抛错也吞掉（避免触发调用方 catch 显示"加载失败"）
            console.error('[v2.13.151 list-pagination.safeUpdate] update 内部错误:', err);
            return false;
        }
    }

    /**
     * v2.13.151 防回归关键方法：safeElement
     * 调用方统一通过此方法获取 DOM 元素，避免在元素不存在时抛 TypeError
     */
    function safeElement(id) {
        var el = document.getElementById(id);
        if (!el) {
            console.error('[v2.13.151 list-pagination.safeElement] 找不到 DOM 元素 #' + id + '（页面可能尚未渲染或 Tab 未激活）');
        }
        return el;
    }

    // 暴露
    window.listPager = {
        update: update,
        safeUpdate: safeUpdate,
        safeElement: safeElement,
        getStoredPageSize: getStoredPageSize,
        setStoredPageSize: setStoredPageSize
    };

    // v2.13.132 兼容入口：旧代码调用的 window.updatePagination(...) 自动映射到新版
    window.updatePagination = function (moduleKey, page, pageSize, totalCount, onPageChange) {
        update(moduleKey, page, pageSize, totalCount, onPageChange);
    };

    // v2.13.151 防回归：标记就绪事件（让其他脚本可以监听）
    try {
        document.dispatchEvent(new CustomEvent('listPagerReady', { detail: { version: 'v2.13.151' } }));
    } catch (e) { /* IE 不支持 CustomEvent，忽略 */ }
})();