// 筛选条件持久化模块 v2.13.11
// 设计目标：
//   1. 列表页加载时从 localStorage 恢复筛选值
//   2. 表单提交前自动保存当前筛选值
//   3. 提供"清除筛选"按钮一键清空 localStorage
//   4. 按模块名（personnel/dorms/booking/meter/billing/dormBilling/employeeBilling/basics）隔离存储
//
// 用法：
//   <form data-filter-form="personnel"> ... </form>
//   <button type="button" onclick="FilterPersistence.reset('personnel')">清除筛选</button>
//   <script src="~/js/filter-persistence.js"></script>

(function (global) {
    'use strict';

    var PREFIX = 'jinge.filter.';

    function getKey(module) {
        return PREFIX + (module || 'default');
    }

    /**
     * 从 URL 收集当前筛选值（GET 参数）
     */
    function collectFromForm(form) {
        var data = {};
        var elements = form.querySelectorAll('input[name], select[name], textarea[name]');
        for (var i = 0; i < elements.length; i++) {
            var el = elements[i];
            // 跳过密码、文件、分页等非筛选字段
            if (el.type === 'password' || el.type === 'file' || el.type === 'submit' || el.type === 'button') continue;
            if (el.name === 'pageIndex' || el.name === '__RequestVerificationToken') continue;
            if (el.type === 'checkbox' || el.type === 'radio') {
                data[el.name] = el.checked;
            } else {
                data[el.name] = el.value;
            }
        }
        return data;
    }

    /**
     * 把筛选值回填到表单
     */
    function applyToForm(form, data) {
        if (!data) return;
        var keys = Object.keys(data);
        for (var i = 0; i < keys.length; i++) {
            var name = keys[i];
            var value = data[name];
            var el = form.querySelector('[name="' + name + '"]');
            if (!el) continue;
            if (el.type === 'checkbox' || el.type === 'radio') {
                el.checked = !!value;
            } else {
                el.value = value == null ? '' : value;
            }
        }
    }

    /**
     * 保存筛选到 localStorage
     */
    function save(module, data) {
        try {
            localStorage.setItem(getKey(module), JSON.stringify(data));
        } catch (e) {
            console.warn('[FilterPersistence] 保存失败：' + e.message);
        }
    }

    /**
     * 从 localStorage 加载筛选
     */
    function load(module) {
        try {
            var raw = localStorage.getItem(getKey(module));
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null;
        }
    }

    /**
     * 清除指定模块的筛选缓存
     */
    function reset(module) {
        try {
            localStorage.removeItem(getKey(module));
        } catch (e) {
            console.warn('[FilterPersistence] 清除失败：' + e.message);
        }
        // 刷新到无筛选状态（不带任何查询参数）
        var url = window.location.pathname;
        window.location.href = url;
    }

    /**
     * 清除所有 jinge.filter.* 缓存
     */
    function resetAll() {
        try {
            var keys = Object.keys(localStorage);
            for (var i = 0; i < keys.length; i++) {
                if (keys[i].indexOf(PREFIX) === 0) localStorage.removeItem(keys[i]);
            }
        } catch (e) {
            console.warn('[FilterPersistence] 全清失败：' + e.message);
        }
    }

    /**
     * 自动初始化：DOMContentLoaded 后扫描所有 data-filter-form 的表单
     */
    function autoInit() {
        var forms = document.querySelectorAll('[data-filter-form]');
        for (var i = 0; i < forms.length; i++) {
            (function (form) {
                var module = form.getAttribute('data-filter-form');
                if (!module) return;
                // 加载已保存的筛选值回填表单（仅在 URL 没有显式参数时）
                var hasUrlParams = window.location.search.length > 0;
                if (!hasUrlParams) {
                    var saved = load(module);
                    if (saved) applyToForm(form, saved);
                }
                // 表单提交前保存当前值
                form.addEventListener('submit', function () {
                    var data = collectFromForm(form);
                    save(module, data);
                });
            })(forms[i]);
        }
    }

    // DOM 就绪后初始化
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', autoInit);
    } else {
        autoInit();
    }

    // 暴露 API
    global.FilterPersistence = {
        save: save,
        load: load,
        reset: reset,
        resetAll: resetAll,
        resetFilters: reset, // 别名兼容原型 FilterPersistence.resetFilters
        applyToForm: applyToForm,
        collectFromForm: collectFromForm
    };
})(window);