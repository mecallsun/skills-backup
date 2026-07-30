/**
 * v2.13.199 注册状态统一保护 - 操作时弹窗提示
 * 所有写操作前检查注册状态，受限则弹出确认对话框，阻止操作。
 * 移除之前页面顶部 Banner 显示方式，改为触发式弹窗。
 */
(function () {
    'use strict';

    // 全局缓存的注册状态
    let __licenseState = null;

    // 预加载注册状态（页面加载时异步获取）
    function initLicenseState() {
        fetch('/api/v1/system/license-status')
            .then(response => response.ok ? response.json() : null)
            .then(data => {
                if (data && data.success && data.data) {
                    __licenseState = data.data;
                } else {
                    __licenseState = { regInt: 0, code: 'error', message: '状态获取失败', level: 'danger' };
                }
            })
            .catch(err => {
                console.warn('许可证状态加载失败:', err);
                __licenseState = { regInt: 0, code: 'error', message: '授权服务不可用', level: 'danger' };
            });
    }

    initLicenseState();

    // 根据注册状态生成统一弹窗消息（符合统一规范格式）
    function getLicensePopupMessage(state) {
        if (!state) return {
            title: '授权服务不可用',
            message: '无法连接到注册验证服务，请联系系统管理员。\n\n影响：所有写操作已被禁用。\n建议：请检查托盘程序是否正常启动，或联系信息科。',
            level: 'danger'
        };

        const { regInt, regStatus, regDate, ltdName, trialExceeded, usedCount, limit } = state;

        // v2.13.199 统一注册状态提示格式
        // 已注册：RegInt=1 → 不弹窗
        if (regInt === 1) return null;

        // ============ 优先级 1：已过期/未注册状态的精确判定 ============
        // 场景 A：regStatus=2（v2.13.169 新枚举：明确已过期）
        if (regStatus === 2) {
            const expiryText = regDate ? `（${regDate.toLocaleDateString()}）` : '';
            return {
                title: '注册码已过期',
                message: `软件注册有效期已过期${expiryText}\n\n当前处于只读模式，所有修改、变更类操作已禁用。\n\n影响：住宿登记、住宿档案、人员清单等所有写入操作受限。\n建议：请联系信息科进行注册续期，恢复完整功能。`,
                level: 'warning'
            };
        }

        // 场景 B：regInt=0（v2.13.170 旧格式：已过期/无效 → Expired(2)）
        // 这是用户实际遇到的场景：regInt=0 + regStatus=0（未填写）
        if (regInt === 0) {
            // 检查 regDate 是否提供有效日期
            const hasExpiredDate = regDate && new Date(regDate) <= new Date();
            const expiryText = hasExpiredDate ? `（${new Date(regDate).toLocaleDateString()}）` : '';
            return {
                title: regDate ? '注册码已过期' : '软件未注册或注册已过期',
                message: (regDate
                    ? `软件注册有效期已过期${expiryText}\n\n当前处于只读模式，所有修改、变更类操作已禁用。\n\n影响：住宿登记、住宿档案、人员清单等所有写入操作受限。\n建议：请联系信息科进行注册续期，恢复完整功能。`
                    : `软件当前未注册或注册信息缺失\n\n当前处于只读模式，所有修改、变更类操作已禁用。\n\n影响：所有新增、编辑、删除操作受限。\n建议：请联系信息科进行注册授权，恢复完整功能。`),
                level: 'warning'
            };
        }

        // ============ 优先级 2：校验失败 ============
        if (regStatus === 3 || regInt === 3) {
            return {
                title: '注册校验失败',
                message: '注册码校验失败（机器码/公司名不匹配）。软件进入只读模式，所有写操作受限。\n\n影响：无法进行新增、编辑、删除等操作。\n建议：请联系信息科重新注册，确保机器码与公司名匹配。',
                level: 'danger'
            };
        }

        // ============ 优先级 3：试用模式（未注册或试用超限） ============
        if (regStatus === -1 || regInt === -1) {
            if (trialExceeded && usedCount !== undefined && limit !== undefined) {
                return {
                    title: '试用次数已达上限',
                    message: `试用次数已用尽（当前 ${usedCount} 条记录 / 上限 ${limit} 条）\n\n当前处于试用受限模式，仅允许查看操作，无法新增或修改数据。\n\n影响：住宿登记（上限500）、住宿档案（上限5）、人员清单（上限5）三大模块记录数超出试用上限。\n建议：请联系信息科完成正式注册或申请增加记录限额，恢复完整功能。`,
                    level: 'warning'
                };
            }
            // 正常试用中，不弹窗（静默通过）
            return null;
        }

        // ============ 兜底：未知状态（实际应该很少遇到） ============
        // 如果走到这里，说明 regInt 不是 0/1/-1/3，且 regStatus 不是 -1/2/3
        // 这通常是 IPC 数据格式错误或托盘端版本不兼容
        // 但仍然友好提示用户：当前为受限状态，需要处理
        return {
            title: '⚠ 软件未注册或注册已过期',
            message: `软件当前注册状态异常，无法验证授权信息。\n\n当前处于只读模式，所有修改、变更类操作已禁用。\n\n影响：所有新增、编辑、删除操作受限。\n建议：请联系信息科进行注册授权或续期，恢复完整功能。`,
            level: 'warning'
        };
    }

    // 统一显示注册状态弹窗
    function showLicensePopup(message) {
        // 防止重复弹窗
        if (document.getElementById('licenseStatusModal')) {
            return;
        }

        const modal = document.createElement('div');
        modal.className = 'modal fade';
        modal.id = 'licenseStatusModal';
        modal.setAttribute('tabindex', '-1');
        modal.setAttribute('role', 'dialog');
        modal.setAttribute('aria-labelledby', 'licenseModalTitle');
        modal.innerHTML = `
            <div class="modal-dialog modal-dialog-centered">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title" id="licenseModalTitle">${message.title}</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <div class="alert alert-${message.level}">
                            <p><strong>症状：</strong>${message.title}</p>
                            <p><strong>影响：</strong>当前操作受限，请继续阅读。</p>
                            <p><strong>建议：</strong>请按照下方说明处理。</p>
                            <hr>
                            <p class="mb-3" style="white-space: pre-line;">${message.message}</p>
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">确定取消操作</button>
                    </div>
                </div>
            </div>
        `;

        document.body.appendChild(modal);
        const modalInstance = new bootstrap.Modal(modal);
        modalInstance.show();

        // ESC 键关闭即取消操作
        modal.addEventListener('hidden.bs.modal', () => {
            document.body.removeChild(modal);
        });
    }

    // 操作前检查：返回 true 可执行，false 应阻止
    async function checkLicenseBeforeOperation() {
        // 如果状态尚未加载，先等待
        if (__licenseState === null) {
            return new Promise((resolve) => {
                const checkInterval = setInterval(() => {
                    if (__licenseState !== null) {
                        clearInterval(checkInterval);
                        resolve(ensureOperationAllowed());
                    }
                }, 50);
            });
        }
        return ensureOperationAllowed();
    }

    function ensureOperationAllowed() {
        const message = getLicensePopupMessage(__licenseState);
        if (message) {
            showLicensePopup(message);
            return false;
        }
        return true;
    }

    // 全局暴露检查函数
    window.checkLicenseBeforeOperation = checkLicenseBeforeOperation;

    // ==================== 拦截策略 ====================

    // 拦截一：所有表单 POST 提交
    document.addEventListener('submit', function (e) {
        const form = e.target;
        if (form.tagName !== 'FORM') return;
        if (form.method && form.method.toLowerCase() !== 'post') return;

        // 跳过登录等公开表单
        if (form.action?.includes('/Account') || form.id?.includes('login')) return;

        // 防止递归调用：如果已经被检查过，直接允许
        if (form.dataset.__licenseChecked === 'true') {
            delete form.dataset.__licenseChecked;
            return;
        }

        e.preventDefault();
        form.dataset.__licenseChecked = '标记';

        checkLicenseBeforeOperation().then(allowed => {
            delete form.dataset.__licenseChecked;
            if (allowed) form.requestSubmit();
        });
    }, true);

    // 拦截二：带有 data-license-protect 属性的按钮/链接
    document.addEventListener('click', function (e) {
        const target = e.target;
        if (target.closest('a') && target.closest('a').hasAttribute('data-license-protect')) {
            e.preventDefault();
            checkLicenseBeforeOperation().then(allowed => {
                if (allowed) target.closest('a').click();
            });
        }
        if (target.closest('button') && target.closest('button').hasAttribute('data-license-protect')) {
            e.preventDefault();
            checkLicenseBeforeOperation().then(allowed => {
                if (allowed) target.closest('button').click();
            });
        }
    }, true);

    // 包装函数：用于包裹特定的操作函数（如退房、删除等）
    window.wrapWithLicenseCheck = function (originalFn) {
        return async function (...args) {
            const allowed = await checkLicenseBeforeOperation();
            if (!allowed) return null;
            return originalFn.apply(this, args);
        };
    };

    // 拦截三：v2.13.210 全局 fetch 拦截（覆盖所有写操作的 fetch 调用）
    // 解决 _FieldPermissionPanel 等使用 fetch PUT/DELETE 但没走表单的页面绕过注册检查的 BUG
    // 凡是 POST/PUT/DELETE/PATCH 的 fetch 请求都会被拦截并触发统一弹窗
    if (!window.__fetchPatched) {
        window.__fetchPatched = true;
        const originalFetch = window.fetch;
        window.fetch = async function patchedFetch(input, init) {
            const method = ((init && init.method) || (typeof input === 'object' ? input?.method : null) || 'GET').toUpperCase();
            // 仅拦截写操作（GET/HEAD/OPTIONS 放行）
            if (['POST', 'PUT', 'DELETE', 'PATCH'].includes(method)) {
                // 白名单：注册状态查询端点不应被拦截
                const url = typeof input === 'string' ? input : (input?.url || '');
                if (!url.includes('/api/v1/system/license-status')) {
                    const allowed = await checkLicenseBeforeOperation();
                    if (!allowed) {
                        // 阻止请求，返回模拟的 403 响应（与后端 LICENSE_READONLY 一致）
                        return new Response(JSON.stringify({
                            success: false,
                            code: 'LICENSE_READONLY',
                            message: '注册状态受限，操作已被前端拦截'
                        }), {
                            status: 403,
                            headers: { 'Content-Type': 'application/json' }
                        });
                    }
                }
            }
            return originalFetch.apply(this, arguments);
        };
    }

    console.log('[LicenseProtect v2.210] 已初始化，注册状态监控:', __licenseState);
})();
