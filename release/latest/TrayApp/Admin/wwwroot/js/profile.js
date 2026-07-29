// v2.13.26 个人中心 - 主交互逻辑
// 涵盖：基本资料/修改密码/安全问题/微信绑定/筛选缓存

(function() {
    'use strict';

    // 公共工具
    function getToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    async function apiCall(url, method, body) {
        const opts = {
            method: method,
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            credentials: 'same-origin'
        };
        if (body) opts.body = JSON.stringify(body);
        const resp = await fetch(url, opts);
        return await resp.json();
    }

    function showError(msg) {
        const html = `<div class="alert alert-danger alert-dismissible py-2 small"><i class="bi bi-exclamation-triangle"></i> ${msg}<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>`;
        alert(html);
    }

    function showSuccess(msg) {
        const html = `<div class="alert alert-success alert-dismissible py-2 small"><i class="bi bi-check-circle"></i> ${msg}<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>`;
        alert(html);
    }

    // ============================================================
    // Tab 1: 基本资料
    // ============================================================
    window.submitProfile = async function(e) {
        e.preventDefault();
        const data = {
            displayName: document.getElementById('profileDisplayName').value.trim(),
            phone: document.getElementById('profilePhone').value.trim() || null,
            email: document.getElementById('profileEmail').value.trim() || null,
            currentPassword: document.getElementById('profileCurrentPassword').value
        };

        if (!data.displayName) {
            showError('显示名不能为空');
            return false;
        }
        if (!data.currentPassword) {
            showError('请输入当前密码');
            return false;
        }

        try {
            const res = await apiCall('/api/v1/account/profile', 'PUT', data);
            if (res.success) {
                showSuccess('资料已更新');
                document.getElementById('profileCurrentPassword').value = '';
                setTimeout(() => location.reload(), 1000);
            } else {
                showError(res.message || '更新失败');
            }
        } catch (err) {
            showError('网络错误：' + err.message);
        }
        return false;
    };

    // ============================================================
    // Tab 2: 修改密码
    // ============================================================
    window.submitPassword = async function(e) {
        e.preventDefault();
        const oldPwd = document.getElementById('pwdOld').value;
        const newPwd = document.getElementById('pwdNew').value;
        const confirmPwd = document.getElementById('pwdConfirm').value;

        if (newPwd !== confirmPwd) {
            showError('两次输入的新密码不一致');
            return false;
        }
        if (newPwd.length < 8) {
            showError('新密码长度至少 8 位');
            return false;
        }
        if (!/[a-zA-Z]/.test(newPwd) || !/\d/.test(newPwd)) {
            showError('新密码必须同时包含字母和数字');
            return false;
        }

        try {
            const res = await apiCall('/api/v1/account/change-password', 'POST', {
                oldPassword: oldPwd,
                newPassword: newPwd,
                confirmPassword: confirmPwd
            });
            if (res.success) {
                showSuccess('密码已修改，请重新登录');
                document.getElementById('pwdOld').value = '';
                document.getElementById('pwdNew').value = '';
                document.getElementById('pwdConfirm').value = '';
                setTimeout(() => location.href = '/Account/Login', 1500);
            } else {
                showError(res.message || '修改失败');
            }
        } catch (err) {
            showError('网络错误：' + err.message);
        }
        return false;
    };

    // ============================================================
    // Tab 2: 安全问题
    // ============================================================
    async function loadSecurityQuestions() {
        try {
            const res = await apiCall('/api/v1/account/security-questions', 'GET');
            const area = document.getElementById('sqListArea');
            if (res.success && res.data && res.data.length > 0) {
                let html = '<ol class="ps-3 mb-2">';
                res.data.forEach(q => {
                    html += `<li>${q.question} <small class="text-muted">（${q.createdAt ? new Date(q.createdAt).toLocaleDateString() : ''}）</small></li>`;
                });
                html += '</ol>';
                area.innerHTML = html;
            } else {
                area.innerHTML = '<p class="text-warning small"><i class="bi bi-exclamation-triangle"></i> 尚未设置安全问题，无法通过安全问题找回密码。</p>';
            }
        } catch (err) {
            document.getElementById('sqListArea').innerHTML = '<p class="text-danger small">加载失败</p>';
        }
    }

    window.openSqModal = function() {
        loadSecurityQuestions();
        const modal = new bootstrap.Modal(document.getElementById('sqModal'));
        modal.show();
    };

    window.submitSq = async function() {
        const q1 = document.getElementById('sqQuestion1').value;
        const a1 = document.getElementById('sqAnswer1').value;
        const q2 = document.getElementById('sqQuestion2').value;
        const a2 = document.getElementById('sqAnswer2').value;
        const pwd = document.getElementById('sqCurrentPassword').value;

        if (!a1 || !a2) {
            showError('请填写所有问题和答案');
            return;
        }
        if (!pwd) {
            showError('请输入当前密码');
            return;
        }

        try {
            const res = await apiCall('/api/v1/account/security-questions', 'POST', {
                questions: [
                    { question: q1, answer: a1 },
                    { question: q2, answer: a2 }
                ],
                currentPassword: pwd
            });
            if (res.success) {
                bootstrap.Modal.getInstance(document.getElementById('sqModal')).hide();
                showSuccess('安全问题已保存');
                loadSecurityQuestions();
            } else {
                showError(res.message || '保存失败');
            }
        } catch (err) {
            showError('网络错误：' + err.message);
        }
    };

    // 加载安全问题列表（页面加载时）
    if (document.getElementById('sqListArea')) {
        loadSecurityQuestions();
    }

    // ============================================================
    // Tab 2: 微信绑定
    // ============================================================
    window.openBindWeChatModal = function() {
        document.getElementById('wechatOpenId').value = '';
        document.getElementById('wechatBindPwd').value = '';
        const modal = new bootstrap.Modal(document.getElementById('wechatBindModal'));
        modal.show();
    };

    window.openUnbindWeChatModal = function() {
        document.getElementById('wechatUnbindPwd').value = '';
        const modal = new bootstrap.Modal(document.getElementById('wechatUnbindModal'));
        modal.show();
    };

    window.submitBindWeChat = async function() {
        const openId = document.getElementById('wechatOpenId').value.trim();
        const pwd = document.getElementById('wechatBindPwd').value;

        if (!/^[A-Za-z0-9_-]{16,64}$/.test(openId)) {
            showError('OpenID 格式不正确（16-64 位字母/数字/下划线/连字符）');
            return;
        }
        if (!pwd) {
            showError('请输入当前密码');
            return;
        }

        try {
            const res = await apiCall('/api/v1/account/wechat/bind', 'POST', {
                openId: openId,
                currentPassword: pwd
            });
            if (res.success) {
                bootstrap.Modal.getInstance(document.getElementById('wechatBindModal')).hide();
                showSuccess('微信绑定成功');
                setTimeout(() => location.reload(), 800);
            } else {
                showError(res.message || '绑定失败');
            }
        } catch (err) {
            showError('网络错误：' + err.message);
        }
    };

    window.submitUnbindWeChat = async function() {
        const pwd = document.getElementById('wechatUnbindPwd').value;
        if (!pwd) {
            showError('请输入当前密码');
            return;
        }

        try {
            const res = await apiCall('/api/v1/account/wechat/unbind', 'POST', {
                currentPassword: pwd
            });
            if (res.success) {
                bootstrap.Modal.getInstance(document.getElementById('wechatUnbindModal')).hide();
                showSuccess('已解绑');
                setTimeout(() => location.reload(), 800);
            } else {
                showError(res.message || '解绑失败');
            }
        } catch (err) {
            showError('网络错误：' + err.message);
        }
    };

    // ============================================================
    // Tab 3: 筛选条件持久化
    // ============================================================
    document.getElementById('storeFilterPreference')?.addEventListener('change', function(e) {
        var enabled = e.target.checked;
        localStorage.setItem('jinge.storeFilterPreference', enabled ? 'true' : 'false');
        if (enabled && window.FilterPersistence && FilterPersistence.syncAllToServer) {
            FilterPersistence.syncAllToServer();
        }
        var msg = enabled ? '已开启云端筛选条件同步' : '已关闭云端同步';
        alert(msg);
    });

    window.resetFilterCache = function(module) {
        if (!confirm('确定要清除该模块的云端筛选缓存吗？')) return;
        fetch('/api/v1/user/filter-cache?module=' + module, { method: 'DELETE' })
            .then(r => r.json())
            .then(res => {
                if (res && res.success) { alert('已清除'); location.reload(); }
                else { alert('清除失败：' + (res?.message || '未知错误')); }
            });
    };

    window.resetAllFilterCache = function() {
        if (!confirm('确定清除所有模块的云端筛选缓存吗？')) return;
        fetch('/api/v1/user/filter-cache/all', { method: 'DELETE' })
            .then(r => r.json())
            .then(res => {
                if (res && res.success) { alert('已清除全部'); location.reload(); }
                else { alert('清除失败'); }
            });
    };

    window.clearLocalStorageCache = function() {
        if (!confirm('确定清除所有模块的本地筛选缓存吗？')) return;
        if (window.FilterPersistence && FilterPersistence.resetAll) {
            FilterPersistence.resetAll();
            alert('已清除本地缓存');
        }
    };

    // URL ?tab= 参数支持
    const urlParams = new URLSearchParams(window.location.search);
    const tab = urlParams.get('tab');
    if (tab === 'security') {
        document.getElementById('tab-security')?.click();
    } else if (tab === 'prefs') {
        document.getElementById('tab-prefs')?.click();
    }
})();