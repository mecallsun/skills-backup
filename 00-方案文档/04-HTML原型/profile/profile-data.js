// ============================================================================
// 个人中心 Mock 数据（v2.13.50 原型）
// ============================================================================

const PROFILE_USER = {
    userId: 1,
    userName: 'admin',
    displayName: '系统管理员',
    roles: ['系统管理员'],
    mobile: '13800138000',
    email: 'admin@jinge.local',
    lastLoginAt: '2026-07-21 09:18',
    lastLoginIp: '192.168.1.50',
    isWeChatBound: true,
    weChatOpenId: 'o6_bm1JZq_xxxxxxxxxxxxxxxxxxxxxxxx',
    weChatBindAt: '2026-05-12 14:30'
};

const PROFILE_CACHE_MODULES = [
    { moduleName: 'personnel',     moduleDisplay: '人员清单', updatedAt: '2026-07-19 16:24' },
    { moduleName: 'booking',       moduleDisplay: '办理登记', updatedAt: '2026-07-20 09:15' },
    { moduleName: 'meter',         moduleDisplay: '抄表记录', updatedAt: '2026-07-18 11:02' },
    { moduleName: 'dorms',         moduleDisplay: '宿舍档案', updatedAt: '2026-07-17 14:50' },
    { moduleName: 'billingStandard', moduleDisplay: '费用标准', updatedAt: '2026-07-15 10:30' },
    { moduleName: 'dormBilling',   moduleDisplay: '宿舍账单', updatedAt: '2026-07-16 16:08' },
    { moduleName: 'employeeBilling', moduleDisplay: '员工账单', updatedAt: '2026-07-16 16:09' }
];

const PROFILE_SECURITY_QUESTIONS = [
    { qNo: 1, question: '您母亲的名字是？', answerMasked: '已设置（密文存储）' },
    { qNo: 2, question: '您第一所小学的名称是？', answerMasked: '已设置（密文存储）' }
];

// v2.14 预留：本人操作日志
const PROFILE_OPERATION_LOGS = [
    { time: '2026-07-21 09:18:23', ip: '192.168.1.50', action: 'Login',          target: '登录系统' },
    { time: '2026-07-20 17:42:11', ip: '192.168.1.50', action: 'ChangePassword', target: '修改密码' },
    { time: '2026-07-20 09:15:00', ip: '192.168.1.50', action: 'UpdateProfile',  target: '更新基本资料（手机号）' },
    { time: '2026-07-19 16:24:30', ip: '192.168.1.50', action: 'BindWeChat',     target: '绑定微信 OpenID' },
    { time: '2026-07-15 10:30:45', ip: '192.168.1.50', action: 'SetSecurityQuestions', target: '设置安全问题' }
];

// ============================================================================
// 渲染函数（原型使用）
// ============================================================================

function renderOverview(user) {
    document.getElementById('ovUserName').textContent = user.userName;
    document.getElementById('ovDisplayName').textContent = user.displayName;
    document.getElementById('ovRoles').innerHTML = user.roles.map(r =>
        `<span class="badge bg-primary me-1">${r}</span>`
    ).join('');
    document.getElementById('ovMobile').textContent = user.mobile || '-';
    document.getElementById('ovEmail').textContent = user.email || '-';
    document.getElementById('ovLastLoginAt').textContent = user.lastLoginAt;
    document.getElementById('ovLastLoginIp').textContent = user.lastLoginIp;
}

function renderCacheList(items) {
    const area = document.getElementById('cacheListArea');
    if (!area) return;
    if (!items || items.length === 0) {
        area.innerHTML = '<p class="text-muted small mb-3"><i class="bi bi-info-circle"></i> 暂无已缓存的筛选模块</p>';
        return;
    }
    area.innerHTML = items.map(item => `
        <div class="cache-item">
            <div>
                <i class="bi bi-check-circle-fill text-success me-2"></i>
                <span class="module-name">${item.moduleDisplay}</span>
                <small class="updated-at">${item.updatedAt}</small>
            </div>
            <button class="btn btn-sm btn-outline-danger" onclick="resetFilterCache('${item.moduleName}')">
                <i class="bi bi-trash"></i>
            </button>
        </div>
    `).join('');
}

function renderOpLogs(logs) {
    const tbody = document.getElementById('logsTableBody');
    if (!tbody) return;
    if (!logs || logs.length === 0) {
        tbody.innerHTML = '<tr><td colspan="4" class="text-center text-muted py-3">暂无操作记录</td></tr>';
        return;
    }
    tbody.innerHTML = logs.map(l => `
        <tr class="log-row">
            <td><small>${l.time}</small></td>
            <td><small>${l.ip}</small></td>
            <td><code>${l.action}</code></td>
            <td><small>${l.target}</small></td>
        </tr>
    `).join('');
}

// DOMContentLoaded 时自动渲染
document.addEventListener('DOMContentLoaded', function() {
    if (typeof PROFILE_USER !== 'undefined') renderOverview(PROFILE_USER);
    if (typeof PROFILE_CACHE_MODULES !== 'undefined') renderCacheList(PROFILE_CACHE_MODULES);
    if (typeof PROFILE_OPERATION_LOGS !== 'undefined') renderOpLogs(PROFILE_OPERATION_LOGS);
});