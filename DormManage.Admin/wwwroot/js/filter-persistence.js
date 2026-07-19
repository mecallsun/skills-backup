// 筛选条件持久化模块 v2.13.12
// 设计目标：
//   1. 列表页加载时从 localStorage 恢复筛选值
//   2. 表单提交前自动保存当前筛选值到 localStorage
//   3. 用户在个人中心勾选"存储筛选条件"后，自动同步到服务端（SysUserFilterCache）
//   4. 提供"清除筛选"按钮一键清空 localStorage
//   5. 历史快照：保存最近 5 次筛选组合，用于一键回退
//   6. 按模块名（personnel/dorms/booking/meter/billing/dormBilling/employeeBilling）隔离存储
//
// 用法：
//   <form data-filter-form="personnel" data-history-key="personnel"> ... </form>
//   <button type="button" onclick="FilterPersistence.reset('personnel')">清除筛选</button>
//   <button type="button" onclick="FilterPersistence.rollback('personnel')">回退</button>
//   <script src="~/js/filter-persistence.js"></script>

(function (global) {
    'use strict';

    var PREFIX = 'jinge.filter.';
    var SNAPSHOT_PREFIX = 'jinge.filter.snapshot.';
    var SNAPSHOT_MAX = 5;
    var PREF_KEY = 'jinge.storeFilterPreference';

    function getKey(module) {
        return PREFIX + (module || 'default');
    }

    function getSnapshotKey(module) {
        return SNAPSHOT_PREFIX + (module || 'default');
    }

    /**
     * 从 URL 收集当前筛选值（GET 参数）
     */
    function collectFromForm(form) {
        var data = {};
        var elements = form.querySelectorAll('input[name], select[name], textarea[name]');
        for (var i = 0; i < elements.length; i++) {
            var el = elements[i];
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
     * 智能判断筛选值是否有意义（全部为空则不保存）
     */
    function isEmptyFilter(data) {
        if (!data) return true;
        var keys = Object.keys(data);
        if (keys.length === 0) return true;
        for (var i = 0; i < keys.length; i++) {
            var v = data[keys[i]];
            if (v === '' || v === null || v === undefined) continue;
            if (typeof v === 'boolean' && v === false) continue;
            return false;
        }
        return true;
    }

    /**
     * 比较两个筛选字典是否相等
     */
    function filterEquals(a, b) {
        if (!a || !b) return false;
        var aKeys = Object.keys(a);
        var bKeys = Object.keys(b);
        if (aKeys.length !== bKeys.length) return false;
        for (var i = 0; i < aKeys.length; i++) {
            if (a[aKeys[i]] !== b[aKeys[i]]) return false;
        }
        return true;
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
     * 保存历史快照（最近 SNAPSHOT_MAX 次）
     */
    function saveSnapshot(module, data) {
        if (isEmptyFilter(data)) return;
        try {
            var snapshots = loadSnapshots(module);
            // 去重：如果最新一条与新数据相同，不重复保存
            if (snapshots.length > 0 && filterEquals(snapshots[0].data, data)) return;
            snapshots.unshift({ data: data, time: Date.now() });
            if (snapshots.length > SNAPSHOT_MAX) snapshots = snapshots.slice(0, SNAPSHOT_MAX);
            localStorage.setItem(getSnapshotKey(module), JSON.stringify(snapshots));
        } catch (e) {
            console.warn('[FilterPersistence] 历史快照保存失败：' + e.message);
        }
    }

    /**
     * 读取历史快照
     */
    function loadSnapshots(module) {
        try {
            var raw = localStorage.getItem(getSnapshotKey(module));
            return raw ? JSON.parse(raw) : [];
        } catch (e) {
            return [];
        }
    }

    /**
     * 回退到指定快照
     */
    function rollback(module, idx) {
        var snapshots = loadSnapshots(module);
        if (idx < 0 || idx >= snapshots.length) return;
        var snap = snapshots[idx];
        save(module, snap.data);
        // 重新应用并提交表单以刷新
        var form = document.querySelector('[data-filter-form="' + module + '"]');
        if (form) {
            applyToForm(form, snap.data);
            form.submit();
        }
    }

    /**
     * 清除指定模块的筛选缓存
     */
    function reset(module) {
        try {
            localStorage.removeItem(getKey(module));
            localStorage.removeItem(getSnapshotKey(module));
        } catch (e) {
            console.warn('[FilterPersistence] 清除失败：' + e.message);
        }
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
                if (keys[i].indexOf(PREFIX) === 0 || keys[i].indexOf(SNAPSHOT_PREFIX) === 0) {
                    localStorage.removeItem(keys[i]);
                }
            }
        } catch (e) {
            console.warn('[FilterPersistence] 全清失败：' + e.message);
        }
    }

    /**
     * 用户是否勾选了"存储筛选条件到云端"
     */
    function isCloudEnabled() {
        try {
            // 同时检查 localStorage 和 cookie（个人中心 cookie 是单一真理源）
            var ls = localStorage.getItem(PREF_KEY);
            if (ls === 'true') return true;
            // 退化：检查 cookie
            var cookie = document.cookie.match(/jinge\.storeFilterPreference=([^;]+)/);
            if (cookie && cookie[1] === 'true') return true;
            return false;
        } catch (e) {
            return false;
        }
    }

    /**
     * 异步：保存筛选到服务端
     */
    function saveToServer(module, data) {
        if (!isCloudEnabled()) return Promise.resolve(false);
        return fetch('/api/v1/user/filter-cache/save', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ module: module, filter: data })
        })
        .then(function (r) { return r.ok; })
        .catch(function (e) {
            console.warn('[FilterPersistence] 服务端保存失败：' + e.message);
            return false;
        });
    }

    /**
     * 异步：从服务端加载筛选
     */
    function loadFromServer(module) {
        if (!isCloudEnabled()) return Promise.resolve(null);
        return fetch('/api/v1/user/filter-cache?module=' + encodeURIComponent(module))
            .then(function (r) { return r.json(); })
            .then(function (res) { return res && res.success ? res.data : null; })
            .catch(function (e) {
                console.warn('[FilterPersistence] 服务端加载失败：' + e.message);
                return null;
            });
    }

    /**
     * 异步：把当前 localStorage 所有模块缓存推送到服务端
     */
    function syncAllToServer() {
        if (!isCloudEnabled()) return Promise.resolve(false);
        var modules = ['personnel', 'dorms', 'booking', 'meter', 'billingStandard', 'dormBilling', 'employeeBilling'];
        var promises = modules.map(function (m) {
            var data = load(m);
            if (!isEmptyFilter(data)) return saveToServer(m, data);
            return Promise.resolve(true);
        });
        return Promise.all(promises).then(function () { return true; });
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
                    if (saved) {
                        applyToForm(form, saved);
                    } else if (isCloudEnabled()) {
                        // v2.13.12 启用云端缓存时，从服务端获取
                        loadFromServer(module).then(function (remote) {
                            if (remote && !isEmptyFilter(remote)) {
                                applyToForm(form, remote);
                                save(module, remote); // 同步到本地
                            }
                        });
                    }
                }

                // 表单提交前保存当前值
                form.addEventListener('submit', function () {
                    var data = collectFromForm(form);
                    var prev = load(module);
                    // 只有当筛选值有变化时才保存历史快照
                    if (!filterEquals(prev, data)) {
                        saveSnapshot(module, prev);
                    }
                    save(module, data);
                    // v2.13.12 启用云端时同步到服务端（异步，不阻塞提交）
                    if (isCloudEnabled()) saveToServer(module, data);
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
        resetFilters: reset, // 别名兼容原型
        rollback: rollback,
        loadSnapshots: loadSnapshots,
        applyToForm: applyToForm,
        collectFromForm: collectFromForm,
        saveToServer: saveToServer,
        loadFromServer: loadFromServer,
        syncAllToServer: syncAllToServer,
        isCloudEnabled: isCloudEnabled,
        isEmptyFilter: isEmptyFilter,
        filterEquals: filterEquals
    };
})(window);