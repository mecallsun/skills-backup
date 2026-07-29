/**
 * v2.13.199 注册状态统一检查工具
 * 所有写操作前调用此函数，若注册状态受限则弹出提示，阻止操作。
 */
(function () {
    'use strict';

    // 统一注册状态弹窗函数
    function showLicenseAlert(code, message, level) {
        const title = getAlertTitle(code, level);
        const html = `
            <div class="alert alert-${level}">
                <h5 class="alert-heading">${title}</h5>
                <p>${message.replace(/\n/g, '<br>')}</p>
                <hr>
                <p class="mb-3">请检查您的注册状态，联系信息科处理。</p>
            </div>
        `;

        // 创建模态对话框
        const modalId = 'licenseStatusModal';
        const modal = document.createElement('div');
        modal.className = 'modal fade';
        modal.id = modalId;
        modal.setAttribute('tabindex', '-1');
        modal.setAttribute('role', 'dialog');
        modal.innerHTML = `
            <div class="modal-dialog modal-dialog-centered">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">${title}</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        ${html}
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">确定取消</button>
                    </div>
                </div>
            </div>
        `;

        document.body.appendChild(modal);
        const modalInstance = new bootstrap.Modal(modal);
        modalInstance.show();

        // 点击确定取消时移除模态框
        modal.addEventListener('hidden.bs.modal', () => {
            document.body.removeChild(modal);
        });
    }

    function getAlertTitle(code, level) {
        switch (level) {
            case 'warning': return '⚠ 注册已过期';
            case 'danger': return '❌ 注册校验失败';
            case 'info': return '🟦 试用模式受限';
            default: return '注册状态检查';
        }
    }

    // 统一检查函数：操作前调用，返回 true 可继续操作，false 应取消
    async function checkLicenseForOperation() {
        try {
            const response = await fetch('/api/v1/system/license-status');
            if (!response.ok) throw new Error('授权服务不可用');

            const data = await response.json();
            if (!data.success || !data.data) throw new Error('无法获取注册状态');

            const status = data.data;
            const code = status.status;
            const message = status.message;
            const level = getStatusLevel(code);

            // 根据状态码决定处理方式
            if (code === 1) {
                // 已注册，正常
                return true;
            } else if (code === 2) {
                // 已过期 - 弹窗并阻止操作
                showLicenseAlert(code, message, 'level');
                return false;
            } else if (code === 3) {
                // 校验失败 - 弹窗并阻止操作
                showLicenseAlert(code, message, 'danger');
                return false;
            } else if (code === -1) {
                // 试用模式 - 如果是试用次数超限，弹窗阻止；如果是正常试用，允许但提示
                if (status.trialExceeded) {
                    const trialMsg = `试用次数已达上限（${status.usedCount}/${status.limit}）。\n\n${message}`;
                    showLicenseAlert(code, trialMsg, 'info');
                } else {
                    // 正常试用，允许操作，但不弹窗（静默通过）
                    return true;
                }
                return false;
            }

            return true; // 未知状态，默认允许
        } catch (err) {
            showLicenseAlert('error', err.message || '注册服务不可用', 'danger');
            return false;
        }
    }

    function getStatusLevel(code) {
        if (code === 2) return 'warning';
        if (code === 3) return 'danger';
        if (code === -1) return 'info';
        return 'success';
    }

    // 注册全局检查函数（供页面调用）
    window.checkLicenseForOperation = checkLicenseForOperation;

    // 为所有保存按钮自动添加检查（可选：在需要时手动添加）
    // 例如：document.querySelectorAll('button[type="submit"]').forEach(btn => {
    //     const originalClick = btn.onclick;
    //     btn.onclick = async function() {
    //         if (!await checkLicenseForOperation()) return false;
    //         if (originalClick) originalClick.apply(this, arguments);
    //     };
    // });

})();