// v2.13.169 注册状态徽章（30s 轮询 /api/v1/system/license-status）
// 4 态映射（与 LicenseGuard.GetLicenseBanner 保持一致）：
//   success / info / warning / danger → bg-success / bg-info / bg-warning / bg-danger
//   + 图标 / 文本 / 客户端日期<->RegDate 二次校验（用户原话：服务端+浏览器日期任一超过即过期）
(function () {
    'use strict';

    var STYLES = {
        success: { cls: 'bg-success', icon: 'bi-check-circle-fill' },
        info:    { cls: 'bg-info',    icon: 'bi-info-circle-fill' },
        warning: { cls: 'bg-warning', icon: 'bi-exclamation-triangle-fill' },
        danger:  { cls: 'bg-danger',  icon: 'bi-shield-exclamation' }
    };

    var FALLBACK_TEXT = '授权服务不可用';
    var POLL_INTERVAL_MS = 30 * 1000;

    function getBadgeEl() {
        return document.getElementById('licenseIndicator') ||
               document.getElementById('licenseIndicatorText') ?
               { badge: document.getElementById('licenseIndicator'),
                 text: document.getElementById('licenseIndicatorText') } :
               null;
    }

    function paintBadge(el, level, text, message, status, regDate) {
        if (!el || !el.badge || !el.text) return;
        var st = STYLES[level] || STYLES.info;
        el.badge.className = 'badge ' + st.cls +
            ' me-2 license-badge-' + level +
            ' title="双击查看详情" style="cursor:pointer;font-size:0.75rem;font-weight:500;padding:0.35rem 0.65rem;';
        // 移除所有旧 class 然后加新的
        el.badge.classList.remove('bg-success','bg-info','bg-warning','bg-danger');
        el.badge.classList.add(st.cls);
        el.badge.innerHTML = '<i class="bi ' + st.icon + '"></i> <span id="licenseIndicatorText">' + escapeHtml(text) + '</span>';

        // title = 详细提示
        var dateInfo = regDate ? '（服务端有效期 ' + new Date(regDate).toLocaleDateString() + '）' : '';
        el.badge.title = message + dateInfo + '\n\n状态码: ' + status;

        // 客户端日期二次校验（用户原话：服务端+浏览器日期任一超过即过期）
        var clientExpired = false;
        if (regDate && level === 'success') {
            // v2.13.180 修复：必须只比较日期部分（YYYY-MM-DD），不能比较完整 DateTime
            // 原 bug：regDate = "2026-07-26T00:00:00" 解析为当天午夜 00:00:00，
            // 客户端当前时间 clientNow 任何时间（哪怕当天 00:00:01）都 > reg → 误触发 clientExpired
            // 修复：删除时分秒，只比较日期
            var clientNow = new Date();
            var reg = new Date(regDate);
            // 关键：把 DateTime 截断到日期（重置为当天 00:00:00）
            var clientDate = new Date(clientNow.getFullYear(), clientNow.getMonth(), clientNow.getDate());
            var regDateOnly = new Date(reg.getFullYear(), reg.getMonth(), reg.getDate());
            if (clientDate > regDateOnly) {
                clientExpired = true;
                // 强化徽章为 warning
                el.badge.classList.remove('bg-success');
                el.badge.classList.add('bg-warning');
                el.badge.classList.add('license-badge-client-expired');
                el.badge.title = '⚠ 您的浏览器本地日期（' + clientNow.toLocaleDateString() + '）已超过服务端有效期（' + reg.toLocaleDateString() + '），已禁用写操作。\n请联系信息科进行续期。';
            }
        }

        // best-effort 禁用写按钮（仅在前端控制，服务端仍权威拦截）
        document.querySelectorAll('button[type=submit], .btn-primary, .btn-danger.action-save').forEach(function (btn) {
            // 排除顶部用户胶囊 / Settings 自身 action / Tab 切换按钮（toast 等）
            if (level === 'warning' || level === 'danger' || clientExpired) {
                if (btn.disabled && btn.dataset.__licenseDisabled === '1') return;
                if (btn.dataset.__licenseDisabled === '1') return;
                btn.dataset.__licenseDisabled = '1';
                btn.disabled = true;
                btn.classList.add('disabled');
                btn.title = btn.title ? btn.title + '（许可受限）' : '许可受限，写操作已禁用';
            } else {
                if (btn.dataset.__licenseDisabled === '1') {
                    delete btn.dataset.__licenseDisabled;
                    btn.disabled = false;
                    btn.classList.remove('disabled');
                    btn.title = '';
                }
            }
        });
    }

    function escapeHtml(s) {
        if (!s) return '';
        return String(s).replace(/[&<>"']/g, function (c) {
            return { '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;' }[c];
        });
    }

    function renderLevel(payload) {
        var level = payload.level;
        var text, status = payload.status;

        if (status === -2)        { text = '授权不可用'; level = 'danger'; }
        else if (status === -1)    { text = '试用模式'; level = 'info'; }
        else if (status === 1)     { text = '已注册'; level = 'success'; }
        else if (status === 2)     { text = '已过期(只读)'; level = 'warning'; }
        else if (status === 3)     { text = '校验失败(只读)'; level = 'danger'; }
        else                       { text = '未知状态'; level = 'info'; }

        var el = getBadgeEl();
        paintBadge(el, level, text, payload.message, status, payload.regDate);
    }

    function poll() {
        fetch('/api/v1/system/license-status')
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (j) {
                if (j && j.success && j.data) renderLevel(j.data);
            })
            .catch(function (e) { console.warn('[license-badge] poll failed:', e); });
    }

    document.addEventListener('DOMContentLoaded', function () {
        poll();
        setInterval(poll, POLL_INTERVAL_MS);
    });
})();
