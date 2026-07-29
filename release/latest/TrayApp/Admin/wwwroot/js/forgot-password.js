// v2.13.26 密码找回 - 3 步向导
(function() {
    'use strict';

    let state = {
        username: '',
        questions: [],
        token: '',
        expiresAt: null
    };

    function showError(msg) {
        document.getElementById('errorArea').innerHTML =
            `<div class="error-message"><i class="bi bi-exclamation-triangle"></i> ${msg}</div>`;
    }

    function clearError() {
        document.getElementById('errorArea').innerHTML = '';
    }

    async function apiCall(url, method, body) {
        const opts = {
            method: method,
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin'
        };
        if (body) opts.body = JSON.stringify(body);
        const resp = await fetch(url, opts);
        return await resp.json();
    }

    window.gotoStep = function(n) {
        clearError();
        for (let i = 1; i <= 4; i++) {
            document.getElementById('step' + i).classList.remove('active');
            document.getElementById('stepInd' + i).classList.remove('active', 'done');
        }
        for (let i = 1; i < n; i++) {
            document.getElementById('stepInd' + i).classList.add('done');
        }
        document.getElementById('stepInd' + n).classList.add('active');
        document.getElementById('step' + n).classList.add('active');
    };

    window.step1Next = async function() {
        clearError();
        const username = document.getElementById('forgotUsername').value.trim();
        if (!username) {
            showError('请输入用户名');
            return;
        }
        state.username = username;

        try {
            const res = await apiCall('/api/v1/account/forgot/get-questions', 'POST', { userName: username });
            if (!res.success) {
                showError(res.message || '查询失败');
                return;
            }
            if (!res.data.userExists || !res.data.questions || res.data.questions.length === 0) {
                showError('该用户未设置安全问题，请联系管理员重置密码');
                return;
            }
            state.questions = res.data.questions;
            renderQuestions();
            gotoStep(2);
        } catch (err) {
            showError('网络错误：' + err.message);
        }
    };

    function renderQuestions() {
        let html = '';
        state.questions.forEach((q, idx) => {
            html += `<div class="mb-3">
                <label class="form-label"><strong>问题 ${idx + 1}：</strong>${q.question}</label>
                <input type="text" class="form-control" id="answer${q.index}" placeholder="请输入答案" required>
                <small class="text-muted">答案不区分大小写</small>
            </div>`;
        });
        document.getElementById('questionsArea').innerHTML = html;
    }

    window.step2Next = async function() {
        clearError();
        const answers = {};
        let allFilled = true;
        state.questions.forEach(q => {
            const v = document.getElementById('answer' + q.index).value.trim();
            if (!v) allFilled = false;
            answers[q.index] = v;
        });
        if (!allFilled) {
            showError('请回答所有问题');
            return;
        }

        try {
            const res = await apiCall('/api/v1/account/forgot/verify', 'POST', {
                userName: state.username,
                answers: answers
            });
            if (!res.success) {
                showError(res.message || '答案不正确');
                return;
            }
            state.token = res.data.token;
            state.expiresAt = res.data.expiresAt;
            gotoStep(3);
        } catch (err) {
            showError('网络错误：' + err.message);
        }
    };

    // 密码强度条
    document.getElementById('newPwd')?.addEventListener('input', function() {
        const pwd = this.value;
        let score = 0;
        if (pwd.length >= 8) score += 25;
        if (pwd.length >= 12) score += 15;
        if (/[a-z]/.test(pwd) && /[A-Z]/.test(pwd)) score += 20;
        if (/\d/.test(pwd)) score += 20;
        if (/[^A-Za-z0-9]/.test(pwd)) score += 20;

        const bar = document.getElementById('pwdStrengthBar');
        const text = document.getElementById('pwdStrengthText');
        bar.style.width = score + '%';
        let label = '', color = '#f44336';
        if (score < 30) { label = '弱'; color = '#f44336'; }
        else if (score < 60) { label = '中'; color = '#ff9800'; }
        else if (score < 80) { label = '强'; color = '#4caf50'; }
        else { label = '很强'; color = '#2e7d32'; }
        bar.style.background = color;
        text.textContent = pwd.length > 0 ? `强度：${label}（${score}分）` : '';
    });

    window.step3Submit = async function() {
        clearError();
        const pwd = document.getElementById('newPwd').value;
        const confirm = document.getElementById('confirmPwd').value;

        if (pwd.length < 8) {
            showError('密码长度至少 8 位');
            return;
        }
        if (!/[a-zA-Z]/.test(pwd) || !/\d/.test(pwd)) {
            showError('密码必须同时包含字母和数字');
            return;
        }
        if (pwd !== confirm) {
            showError('两次输入的密码不一致');
            return;
        }

        try {
            const res = await apiCall('/api/v1/account/forgot/reset', 'POST', {
                token: state.token,
                newPassword: pwd,
                confirmPassword: confirm
            });
            if (!res.success) {
                showError(res.message || '重置失败');
                return;
            }
            gotoStep(4);
        } catch (err) {
            showError('网络错误：' + err.message);
        }
    };
})();