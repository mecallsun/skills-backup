// =====================================================================
// 修改办理登记页面 — 字段数据来源与关系元数据（v2.11.5 增补）
// 与 00-方案文档/07-办理登记需求-v2.11.md §13 完全一致
// 供 booking/edit.html 渲染"数据来源与关系矩阵"面板 / ER 图 / 引用字典
// =====================================================================

const BOOKING_RELS = {
    // 字段元数据：source=数据源类型（'table' | 'join' | 'readonly' | 'dict' | 'auto'）
    //          target=目标表/字典，column=字段名，editable=是否可编辑
    fields: [
        { id: 'employee', label: '员工', source: 'table', target: 'DormBooking', column: 'EmployeeCode + EmployeeName (冗余)',
          fk: 'DormBooking.EmployeeId → SysEmployee.Id', editable: false, note: '冗余字段，避免 N+1 查询' },
        { id: 'attendance', label: '考勤班次', source: 'join', target: 'SysEmployee.AttendanceTypeId → AttendanceType.Name',
          fk: '运行时 JOIN（单一数据源）', editable: false, note: 'v2.11.7 改用 FK 关联（不再存字符串）' },
        { id: 'dorm', label: '宿舍', source: 'table', target: 'DormBooking', column: 'DormCode',
          fk: 'DormBooking.DormCode → Dorm.DormCode', editable: false, note: '与 Dorm 表 DormCode 关联' },
        { id: 'type', label: '类型', source: 'dict', target: 'BOOKING_TYPES',
          column: 'DormBooking.Type (TINYINT)', fk: '1=入住 / 2=退房', editable: false, note: '字典翻译' },
        { id: 'status', label: '状态', source: 'dict', target: 'BOOKING_STATUSES',
          column: 'DormBooking.Status (TINYINT)', fk: '1=预订 / 2=在宿 / 3=已退房 / 4=已取消',
          editable: 'conditional', note: '仅 Status=1 预订时可编辑（切换为在宿/已退房/已取消）' },
        { id: 'bookingDate', label: '入退日期', source: 'table', target: 'DormBooking', column: 'BookingDate (DATE)',
          fk: '—', editable: true, note: '必填，触发一进一出校验 V-EDIT-02~06' },
        { id: 'reason', label: '原因', source: 'dict', target: 'Reason 字典',
          column: 'DormBooking.Reason (string)', fk: '入职/调岗/换房/预订/离职/其他', editable: true, note: '前端硬编码枚举' },
        { id: 'remark', label: '备注', source: 'table', target: 'DormBooking', column: 'Remark (NVARCHAR 500)',
          fk: '—', editable: true, note: '可选，长度 ≤ 500' },
        { id: 'regDate', label: '登记日期', source: 'auto', target: 'DormBooking', column: 'RegistrationDate (DATETIME)',
          fk: '创建时写入 now()', editable: false, note: '系统自动记录，不可修改' },
        { id: 'registrar', label: '登记人', source: 'auto', target: 'DormBooking', column: 'Registrar (NVARCHAR 50)',
          fk: '创建时写入当前登录用户名', editable: false, note: '系统自动记录，不可修改' }
    ],

    // 关联基础资料字典
    dictionaries: [
        { key: 'BOOKING_TYPES', desc: '办理类型', values: '1=入住 / 2=退房', usedBy: '类型字段文案翻译' },
        { key: 'BOOKING_STATUSES', desc: '办理状态', values: '1=预订 / 2=在宿 / 3=已退房 / 4=已取消', usedBy: '状态字段文案翻译' },
        { key: 'BOOKING_STATUS_BADGE', desc: '状态 Badge 配色', values: 'bg-warning / bg-success / bg-secondary / bg-danger', usedBy: '状态 Badge 渲染' },
        { key: 'ATTENDANCE_TYPES', desc: '考勤班次（基础资料）', values: '6 种（DEFAULT/MORNING/MIDDLE/EVENING/NIGHT/OTHER）', usedBy: '运行时 JOIN 翻译考勤班次' },
        { key: 'ATTENDANCE_BADGE', desc: '考勤 Badge 配色', values: 'bg-secondary / bg-warning / bg-primary / bg-dark / bg-info', usedBy: '考勤班次 Badge 渲染' },
        { key: 'DEPARTMENTS', desc: '部门字典', values: '8 个（生产部/技术部/.../其他）', usedBy: '通过员工冗余 Department 间接引用' },
        { key: 'EMPLOYEE_TYPES', desc: '员工类型字典', values: '5 种（合同/临时/外包/实习/驻场）', usedBy: '通过员工冗余 EmployeeType 间接引用' },
        { key: 'EMPLOYMENT_STATUSES', desc: '在职状态字典', values: '3 种（在职/待入职/已离职）', usedBy: '编辑时需校验员工在职状态' },
        { key: 'Reason 字典', desc: '原因枚举（前端硬编码）', values: '入职/调岗/换房/预订/离职/其他', usedBy: '`<select>` 下拉选项' }
    ],

    // ER 关系图节点（用于可视化）
    erNodes: [
        { id: 'sysUser', label: 'SysUser', type: 'table', x: 0, y: 0,
          cols: ['Id (PK)', 'UserName'] },
        { id: 'sysEmployee', label: 'SysEmployee', type: 'table', x: 0, y: 1,
          cols: ['Id (PK)', 'EmployeeCode', 'RealName', 'DepartmentId', 'EmployeeTypeId', 'AttendanceTypeId', 'Status', 'HireDate', 'DormCode'] },
        { id: 'attendanceType', label: 'AttendanceType', type: 'dict', x: -1, y: 2,
          cols: ['Id (PK)', 'Code', 'Name', 'WorkHours'] },
        { id: 'department', label: 'Department', type: 'dict', x: 1, y: 2,
          cols: ['Id (PK)', 'Code', 'Name', 'SortOrder'] },
        { id: 'dorm', label: 'Dorm', type: 'table', x: -1, y: 1,
          cols: ['DormCode (PK)', 'Building', 'Floor', 'Capacity', 'DepartmentId'] },
        { id: 'dormBooking', label: 'DormBooking ★', type: 'highlight', x: 0, y: 2,
          cols: ['Id (PK)', 'EmployeeId (FK)', 'EmployeeCode (冗余)', 'EmployeeName (冗余)', 'DormCode (FK)', 'Type', 'BookingDate ★', 'Status', 'Reason', 'Remark', 'RegistrationDate', 'Registrar'] }
    ],

    // ER 关系图连线
    erLinks: [
        { from: 'sysUser', to: 'dormBooking', label: 'Registrar (写入)' },
        { from: 'sysEmployee', to: 'dormBooking', label: 'EmployeeId' },
        { from: 'sysEmployee', to: 'attendanceType', label: 'AttendanceTypeId' },
        { from: 'sysEmployee', to: 'department', label: 'DepartmentId' },
        { from: 'dorm', to: 'dormBooking', label: 'DormCode' }
    ],

    // 校验联动（提交时执行）
    validations: [
        { id: 'V-EDIT-01', name: '入退日期非空', trigger: '必填', rule: 'BookingDate IS NOT NULL', message: '请选择入退日期' },
        { id: 'V-EDIT-02', name: '入退日期 ≤ 今天', trigger: '任意编辑', rule: 'BookingDate ≤ now()', message: '入退日期不能晚于今天' },
        { id: 'V-EDIT-03', name: '一进一出顺序', trigger: '状态变更时', rule: '入住↔退房交替排列', message: '请检查日期顺序：与上次操作冲突' },
        { id: 'V-EDIT-04', name: '入住日期 ≥ 入职日期', trigger: '类型=1', rule: 'BookingDate ≥ SysEmployee.HireDate', message: '入住日期不能早于入职日期' },
        { id: 'V-EDIT-05', name: '退房日期 ≥ 入住日期', trigger: '类型=2', rule: 'BookingDate ≥ 关联入住记录的 BookingDate', message: '退房日期不能早于入住日期' },
        { id: 'V-EDIT-06', name: '退房日期 ≤ 下次入住', trigger: '类型=2', rule: 'BookingDate < 下次入住.bookingDate - 1天', message: '退房日期不能晚于下次入住' },
        { id: 'V-EDIT-07', name: '房间余量', trigger: '入住记录', rule: 'Dorm.CurrentResidentCount ≤ Dorm.Capacity', message: '房间余量不足' },
        { id: 'V-EDIT-08', name: '同员工同日期唯一', trigger: '任意编辑', rule: '(EmployeeId, BookingDate) 唯一（排除自身）', message: '该员工该日期已存在办理记录' }
    ],

    // 数据写入流程（8 步）
    writeFlow: [
        '① 加载 DormBooking 原始记录',
        '② 校验可编辑字段（BookingDate / Reason / Remark / Status?）',
        '③ 触发校验 V-EDIT-01 ~ V-EDIT-08',
        '④ 若 Status 从 1 改为 2（在宿）：更新 DormBooking.Status + 联动 SysEmployee.DormCode',
        '⑤ 若 Status 从 1 改为 3（已退房）：更新 DormBooking.Status + 联动 SysEmployee.DormCode=NULL',
        '⑥ 若 Status 从 1 改为 4（已取消）：仅更新 DormBooking.Status',
        '⑦ 更新 DormBooking.UpdatedAt = now()',
        '⑧ 返回 ApiResponse { success: true, data: 更新后的记录 }'
    ]
};

// 数据源类型渲染辅助（v2.11.5.b 规范化）
function renderSourceBadge(source) {
    const map = {
        table:   { text: 'DormBooking', cls: 'bg-primary', icon: 'bi-database' },
        join:    { text: '运行时 JOIN', cls: 'bg-info text-dark', icon: 'bi-link-45deg' },
        dict:    { text: '字典翻译',   cls: 'bg-success', icon: 'bi-bookmark-star' },
        readonly:{ text: '只读字段',   cls: 'bg-secondary', icon: 'bi-lock' },
        auto:    { text: '系统自动',   cls: 'bg-warning text-dark', icon: 'bi-magic' }
    };
    const m = map[source] || map.readonly;
    return `<span class="badge src-badge ${m.cls}"><i class="${m.icon}"></i> ${m.text}</span>`;
}

// 字段源信息悬浮提示（用于表单 label 后的图标按钮）
function renderSourceHint(field) {
    const sourceMap = {
        table:   { icon: 'bi-database',         color: '#1976d2', label: 'DormBooking 直接存储' },
        join:    { icon: 'bi-link-45deg',       color: '#00838f', label: '运行时 JOIN' },
        dict:    { icon: 'bi-bookmark-star',    color: '#2e7d32', label: '字典翻译' },
        readonly:{ icon: 'bi-lock',             color: '#5f6368', label: '只读字段' },
        auto:    { icon: 'bi-magic',            color: '#e65100', label: '系统自动写入' }
    };
    const s = sourceMap[field.source] || sourceMap.readonly;
    const tooltip = [
        `<strong>${field.label}</strong>`,
        `数据源：${s.label}`,
        field.target ? `目标：<code>${field.target}</code>` : '',
        field.column ? `字段：<code>${field.column}</code>` : '',
        field.fk ? `FK：<code>${field.fk}</code>` : '',
        field.note ? `<br>说明：${field.note}` : ''
    ].filter(Boolean).join('<br>');
    return `<i class="bi ${s.icon} src-hint-icon" style="color:${s.color};" data-bs-toggle="tooltip" data-bs-html="true" title='${tooltip}'></i>`;
}

// 渲染字段元数据行
function renderFieldMeta(field) {
    const editableBadge = field.editable === true
        ? '<span class="badge bg-success" style="font-size:10px;">可编辑</span>'
        : field.editable === 'conditional'
            ? '<span class="badge bg-warning text-dark" style="font-size:10px;">条件可编辑</span>'
            : '<span class="badge bg-secondary" style="font-size:10px;">只读</span>';
    return `
        <tr>
            <td><strong>${field.label}</strong></td>
            <td>${renderSourceBadge(field.source)}</td>
            <td><code style="font-size:12px;">${field.target}</code></td>
            <td><small>${field.column || '-'}</small></td>
            <td>${editableBadge}</td>
        </tr>
    `;
}

// 渲染 ER 关系图（HTML/SVG 简化版）
// v2.11.5.b 规范化：使用稳定布局 + 清晰连线
function renderERDiagram(nodes, links) {
    const positions = {
        sysUser:         { x: 60,  y: 40 },
        sysEmployee:     { x: 60,  y: 200 },
        dorm:            { x: 60,  y: 360 },
        dormBooking:     { x: 460, y: 200 },
        attendanceType:  { x: 760, y: 40 },
        department:      { x: 760, y: 360 }
    };
    const w = 220, h = 110;
    let svg = `<svg viewBox="0 0 980 480" style="width:100%;max-width:980px;height:auto;font-family:inherit;" xmlns="http://www.w3.org/2000/svg">`;
    svg += `<defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="#5f6368"/></marker></defs>`;
    // 连线
    links.forEach(l => {
        const a = positions[l.from], b = positions[l.to];
        if (!a || !b) return;
        const ax = a.x + (b.x > a.x ? w : 0);
        const ay = a.y + h / 2;
        const bx = b.x + (b.x < a.x ? w : 0);
        const by = b.y + h / 2;
        const mx = (ax + bx) / 2;
        svg += `<path d="M${ax},${ay} C${mx},${ay} ${mx},${by} ${bx},${by}" fill="none" stroke="#5f6368" stroke-width="1.5" marker-end="url(#arrow)"/>`;
        svg += `<text x="${mx}" y="${(ay + by) / 2}" text-anchor="middle" font-size="11" fill="#5f6368" font-weight="500">${l.label}</text>`;
    });
    // 节点
    nodes.forEach(n => {
        const p = positions[n.id];
        if (!p) return;
        const fill = n.type === 'highlight' ? '#fff3cd' : (n.type === 'dict' ? '#e8f5e9' : '#e3f2fd');
        const stroke = n.type === 'highlight' ? '#f57c00' : (n.type === 'dict' ? '#2e7d32' : '#1976d2');
        svg += `<rect x="${p.x}" y="${p.y}" width="${w}" height="${h}" rx="6" fill="${fill}" stroke="${stroke}" stroke-width="${n.type === 'highlight' ? 2.5 : 1.5}"/>`;
        svg += `<text x="${p.x + 10}" y="${p.y + 20}" font-size="13" font-weight="700" fill="${stroke}">${n.label}</text>`;
        n.cols.slice(0, 6).forEach((c, i) => {
            svg += `<text x="${p.x + 10}" y="${p.y + 38 + i * 12}" font-size="10" fill="#3c4043">${c}</text>`;
        });
    });
    svg += `</svg>`;
    return svg;
}

// 渲染信息卡（用于只读字段区域）
function renderInfoItem(field, value, options = {}) {
    const hint = renderSourceHint(field);
    return `
        <div class="info-item">
            <div class="info-label">
                ${hint}
                <span class="info-label-text">${field.label}</span>
                ${options.required ? '<span class="text-danger">*</span>' : ''}
            </div>
            <div class="info-value">${value}</div>
        </div>
    `;
}