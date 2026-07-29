// ============================================================
// 全局错误处理 - 统一拦截 LICENSE_READONLY / TRIAL_LIMIT_EXCEEDED 错误并显示独占窗口
// 金戈宿舍管理系统 v2.13.x
// ============================================================

(function () {
    'use strict';

    // 显示注册/试用限制警告弹窗（独占窗口）
    function showLicenseReadOnlyWarning(message) {
        // 检查是否已有警告窗口正在显示
        if (document.getElementById('licenseWarningOverlay')) {
            return; // 避免重复显示
        }

        const overlay = document.createElement('div');
        overlay.className = 'license-warning-overlay';
        overlay.id = 'licenseWarningOverlay';
        overlay.setAttribute('aria-modal', 'true');
        overlay.role = 'dialog';
        overlay.setAttribute('aria-labelledby', 'licenseWarningTitle');

        overlay.innerHTML = `
            <div class="license-warning-modal">
                <div class="license-warning-header">
                    <i class="bi bi-shield-exclamation"></i>
                    <h3 id="licenseWarningTitle">操作受限</h3>
                </div>

                <div class="license-warning-content">
                    <p><strong>症状：</strong>软件注册有效期已过期或试用记录已达上限</p>
                    <p><strong>影响：</strong>当前处于只读模式，所有修改、变更类操作已禁用</p>
                    <p><strong>建议：</strong>请联系信息科进行注册续期或申请增加记录限额，恢复完整功能</p>
                    <p class="small text-muted mt-2">${escapeHtml(message || '软件注册已过期或试用记录已达上限，所有修改类操作已禁用。请联系信息科进行处理。')}</p>
                </div>

                <div class="license-warning-footer">
                    <button class="license-warning-btn license-warning-btn-secondary" onclick="window.location.href='/'">
                        <i class="bi bi-house"></i> 返回首页
                    </button>
                    <button class="license-warning-btn license-warning-btn-primary" onclick="window.history.back()">
                        <i class="bi bi-arrow-left"></i> 返回上一页
                    </button>
                </div>
            </div>
        `;

        document.body.appendChild(overlay);

        // 阻止背景点击穿透
        overlay.addEventListener('click', function (e) {
            e.stopPropagation();
        });

        // ESC 键关闭（返回上一页或首页）
        function handleEsc(e) {
            if (e.key === 'Escape') {
                document.removeEventListener('keydown', handleEsc);
                overlay.remove();
                window.location.href = '/';
            }
        }
        document.addEventListener('keydown', handleEsc);
    }

    // XSS 防护：HTML转义
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // 拦截所有 AJAX/fetch 请求的错误响应
    function setupGlobalErrorHandling() {
        // 拦截 XMLHttpRequest（包括 jQuery、axios 等传统 AJAX）
        const originalOpen = XMLHttpRequest.prototype.open;
        const originalSend = XMLHttpRequest.prototype.send;

        XMLHttpRequest.prototype.open = function (method, url, async) {
            originalOpen.apply(this, arguments);
            this._requestUrl = url;
        };

        XMLHttpRequest.prototype.send = function (body) {
            const xhr = this;
            const originalOnabort = xhr.onabort;
            const originalOnerror = xhr.onerror;
            const originalOnloadend = xhr.onloadend;

            xhr.onloadend = function () {
                if (originalOnloadend) {
                    originalOnloadend.apply(xhr, arguments);
                }

                // 检查是否为只读模式错误（403 + 特定 JSON 响应）
                if (xhr.status === 403) {
                    let message = '操作被禁止，请联系信息科。';

                    // 优先从响应头获取详细消息（中间件设置的 X-License-Message）
                    try {
                        const licenseMessage = xhr.getResponseHeader('X-License-Message');
                        const licenseStatus = xhr.getResponseHeader('X-License-Status');

                        if (licenseMessage) {
                            message = licenseMessage;
                        } else if (licenseStatus) {
                            // 根据状态码设置默认消息
                            message = licenseStatus === 'expired'
                                ? '注册码已过期，软件进入只读模式。请联系信息科进行续期。'
                                : licenseStatus === 'invalid'
                                    ? '注册码校验失败，软件进入只读模式。请联系信息科。'
                                    : '注册状态异常，操作被禁止。';
                        }
                    } catch (e) {
                        // 读取响应头失败，使用默认消息
                    }

                    try {
                        const response = JSON.parse(xhr.responseText || '{}');
                        // 同时处理 LICENSE_READONLY（注册过期）和 TRIAL_LIMIT_EXCEEDED（记录数超限）
                        if ((response && response.code === 'LICENSE_READONLY') ||
                            (response && response.code === 'TRIAL_LIMIT_EXCEEDED')) {
                            // JSON 响应中的 message 优先于响应头
                            const detailedMessage = response && response.message ? response.message : message;
                            showLicenseReadOnlyWarning(detailedMessage);
                        } else {
                            // 没有匹配的代码或使用响应头/默认消息
                            showLicenseReadOnlyWarning(message);
                        }
                    } catch (e2) {
                        // JSON 解析失败，使用响应头或默认消息
                        showLicenseReadOnlyWarning(message);
                    }
                }
            };

            xhr.onerror = function () {
                if (originalOnerror) {
                    originalOnerror.apply(xhr, arguments);
                }
                // 网络错误也显示警告（可能是中间件重定向导致的错误）
                if (xhr.status === 0 && xhr._requestUrl && !xhr._requestUrl.includes('/Error')) {
                    // 可能是重定向失败，尝试显示警告
                    setTimeout(() => {
                        if (!document.getElementById('licenseWarningOverlay')) {
                            showLicenseReadOnlyWarning('网络连接异常，可能因注册过期或试用限制导致操作中断。');
                        }
                    }, 100);
                }
            };

            // 标记是否为表单提交（用于内部处理）
            xhr.customIsFormSubmit = false;

            originalSend.call(this, body);
        };

        // 拦截 fetch 请求
        const originalFetch = window.fetch;
        window.fetch = function (input, init) {
            return originalFetch(input, init).then(function (response) {
                // 检查响应状态码
                if (response.status === 403) {
                    response.json().then(function (data) {
                        // 同时处理 LICENSE_READONLY 和 TRIAL_LIMIT_EXCEEDED
                        if (data && (data.code === 'LICENSE_READONLY' || data.code === 'TRIAL_LIMIT_EXCEEDED')) {
                            showLicenseReadOnlyWarning(data.message || '操作被禁止，请联系信息科。');
                        }
                    }).catch(function () {
                        // JSON 解析失败，也显示警告
                        showLicenseReadOnlyWarning('操作被禁止，请联系信息科。');
                    });
                }
                return response;
            }).catch(function (error) {
                // 网络错误时也可能遇到只读模式
                showLicenseReadOnlyWarning('操作被禁止，请检查网络连接和注册状态。');
                throw error;
            });
        };

        // 拦截所有 form POST 请求
        document.addEventListener('submit', function (event) {
            const form = event.target;
            if (form.method && form.method.toLowerCase() === 'post') {
                // 为表单的 submit 标记特殊属性
                const xhr = new XMLHttpRequest();
                xhr.customIsFormSubmit = true;

                // 短暂延迟以允许后续中间件处理，然后检查响应
                setTimeout(() => {
                    if (!document.getElementById('licenseWarningOverlay')) {
                        // 如果尚未出现警告窗口，可能是传统表单重定向
                        // 这里无法拦截重定向，但 Error.cshtml 会处理
                    }
                }, 500);
            }
        }, true); // 使用 capture phase
    }

    // 初始化
    try {
        setupGlobalErrorHandling();
        console.log('[全局错误处理] 已激活，拦截 LICENSE_READONLY / TRIAL_LIMIT_EXCEEDED 错误');
    } catch (e) {
        console.error('[全局错误处理] 初始化失败:', e);
    }
})();

// ============================================================
// LicenseStatusBadge 扩展：添加只读模式通知
// ============================================================

(function () {
    'use strict';

    // 检查并显示只读模式警告
    function checkLicenseReadOnlyState() {
        // 从 LicenseGuard 获取状态（通过 API）- 使用正确的端点路径
        fetch('/api/v1/system/license-status')
            .then(function (response) {
                if (!response.ok) return;
                return response.json();
            })
            .then(function (data) {
                // ApiResponse 格式: { Success, Code, Message, Data }
                if (data && (data.Data || data.data)) {
                    // 兼容 PascalCase (Data) 和 camelCase (data) 两种命名
                    const state = data.Data || data.data;
                    // 如果注册已过期或无效（regInt !== 1，包括 0=过期/无效, -1=未注册）
                    if (state.regInt !== 1) {
                        // 只在当前会话显示一次（避免重复弹窗）
                        if (!sessionStorage.getItem('licenseWarningShown')) {
                            const displayName = state.ltdName || '软件';
                            const regDate = state.regDate ? new Date(state.regDate).toLocaleDateString() : '未知';
                            showLicenseReadOnlyWarning(
                                `${displayName} 注册已过期（有效期：${regDate}）。\n\n` +
                                '当前处于只读模式，所有修改、变更类操作已禁用。\n\n' +
                                '请联系信息科进行注册续期，恢复完整功能。'
                            );
                            sessionStorage.setItem('licenseWarningShown', '1');
                            // 30分钟后清除标记
                            setTimeout(() => {
                                sessionStorage.removeItem('licenseWarningShown');
                            }, 30 * 60 * 1000);
                        }
                    }
                }
            })
            .catch(function () {
                // IPC 不可用，显示警告
                if (!sessionStorage.getItem('licenseWarningShown')) {
                    showLicenseReadOnlyWarning('无法连接注册服务，可能已进入只读模式。请联系信息科。');
                    sessionStorage.setItem('licenseWarningShown', '1');
                    setTimeout(() => {
                        sessionStorage.removeItem('licenseWarningShown');
                    }, 30 * 60 * 1000);
                }
            });
    }

    // 页面加载后定期检查（每30秒）
    document.addEventListener('DOMContentLoaded', function () {
        // 首次检查
        checkLicenseReadOnlyState();

        // 定期轮询（每30秒一次）
        setInterval(checkLicenseReadOnlyState, 30000);
    });
})();
