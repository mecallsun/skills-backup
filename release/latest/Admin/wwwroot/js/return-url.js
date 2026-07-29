// v2.13.88 列表 → 详情/编辑 返回路径记忆模块
// 设计目标：
//   1. 用户在列表页（带分页/筛选条件）点击 "详情/编辑/修正/修改" 等按钮时，
//      自动在 href 上追加 ?returnUrl=<encoded 当前完整 URL（含 query）>
//   2. 详情/编辑页"返回列表"按钮读取 returnUrl 并跳回原列表原状态
//   3. 如果无 returnUrl 参数，则按 href.data-list 属性回退到默认列表 URL
//   4. 仅对标记 data-return-list 的链接生效，避免误改
//
// 用法：
//   列表页：<script src="~/js/return-url.js"></script>
//          ReturnUrl.attachAll('[data-return-list]');  // 自动为所有链接附加 returnUrl
//   详情/编辑页：<a href="#" data-back-to-list="/Booking">返回列表</a>
//          <script>document.querySelector('[data-back-to-list]').addEventListener('click',
//                   ReturnUrl.handleBack);</script>

(function (global) {
    'use strict';

    // 读取当前 URL 的 query string（不含 hash）
    function getCurrentQuery() {
        return window.location.search || '';
    }

    // 在指定链接 href 上附加 returnUrl 参数（如果尚未包含）
    function attach(link) {
        if (!link || !link.getAttribute) return;
        var href = link.getAttribute('href');
        if (!href || href.startsWith('#') || href.startsWith('javascript:')) return;
        // 跳过已经是锚点或外部链接
        if (/^https?:\/\//i.test(href) && !href.startsWith(window.location.origin)) return;
        // 跳过已是 POST/表单提交/JS 函数
        if (link.dataset.returnSkip === 'true') return;

        var currentQuery = getCurrentQuery();
        if (!currentQuery || currentQuery === '?') return;

        // 解析当前 URL 拼接 returnUrl
        var separator = href.indexOf('?') === -1 ? '?' : '&';
        var returnUrlParam = 'returnUrl=' + encodeURIComponent(window.location.pathname + currentQuery);

        // 避免重复添加
        if (href.indexOf('returnUrl=') !== -1) return;

        link.setAttribute('href', href + separator + returnUrlParam);
    }

    // 自动为所有 data-return-list 链接附加 returnUrl
    function attachAll(selector) {
        var sel = selector || '[data-return-list]';
        var links = document.querySelectorAll(sel);
        links.forEach(attach);
    }

    // 处理"返回列表"按钮点击：从 returnUrl 参数读取，或回退到默认列表
    function handleBack(e) {
        if (e) e.preventDefault();
        var target = e && e.currentTarget ? e.currentTarget : (this || null);
        var defaultList = '/';
        if (target && target.dataset && target.dataset.backToList) {
            defaultList = target.dataset.backToList;
        }

        // 1. 优先从 URL 参数 returnUrl 读取
        var params = new URLSearchParams(window.location.search);
        var returnUrl = params.get('returnUrl');
        if (returnUrl) {
            // 解码 + 安全校验：仅允许站内相对路径
            try {
                var decoded = decodeURIComponent(returnUrl);
                if (decoded.startsWith('/') && !decoded.startsWith('//')) {
                    window.location.href = decoded;
                    return;
                }
            } catch (err) {
                // 编码错误，回退到 defaultList
            }
        }

        // 2. 回退到默认列表
        window.location.href = defaultList;
    }

    global.ReturnUrl = {
        attach: attach,
        attachAll: attachAll,
        handleBack: handleBack,
        getCurrentQuery: getCurrentQuery
    };

    // v2.13.88 自动执行：在 DOMContentLoaded 时为所有 data-return-list 链接附加 returnUrl
    // 由 _Layout.cshtml 全局加载，列表页 0 改动即可生效
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { attachAll(); });
    } else {
        attachAll();
    }
})(window);