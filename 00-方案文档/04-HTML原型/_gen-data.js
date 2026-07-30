// =====================================================================
// 一次性数据生成器 — v2.11.1 大规模数据扩展
// 输出：6 个数据集 + 1 个聚合统计（JSON 格式到 stdout）
// 运行：node _gen-data.js > _gen-output.json
// 可复现：使用固定种子 mulberry32(20260711)
// =====================================================================

// --- 固定种子伪随机（保证每次运行结果一致） ---
function mulberry32(seed) {
    return function() {
        seed |= 0; seed = seed + 0x6D2B79F5 | 0;
        let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
        t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
        return ((t ^ t >>> 14) >>> 0) / 4294967296;
    };
}
const rnd = mulberry32(20260711);
const pick = (arr) => arr[Math.floor(rnd() * arr.length)];
const randInt = (min, max) => Math.floor(rnd() * (max - min + 1)) + min;
const randFloat = (min, max, dec = 2) => +(rnd() * (max - min) + min).toFixed(dec);

// --- 字典 ---
const DEPARTMENTS = ["生产部", "技术部", "行政部", "财务部", "销售部", "后勤部", "仓储部", "其他"];
const EMPLOYEE_TYPES = ["合同工", "临时工", "外包", "实习生", "驻场"];
const BUILDINGS = ["1号楼", "2号楼", "3号楼", "4号楼", "5号楼"];
// v2.11.2 考勤类型枚举（按部门加权分配）
const ATTENDANCE_TYPES = ["DEFAULT", "MORNING", "MIDDLE", "EVENING", "NIGHT", "OTHER"];
const ATTENDANCE_WEIGHT_BY_DEPT = {
    "生产部":   [["MORNING", 45], ["MIDDLE", 20], ["EVENING", 20], ["NIGHT", 10], ["DEFAULT", 5],  ["OTHER", 0]],
    "技术部":   [["DEFAULT", 55], ["MORNING", 10], ["MIDDLE", 15], ["EVENING", 15], ["NIGHT", 0],  ["OTHER", 5]],
    "行政部":   [["DEFAULT", 80], ["MORNING", 5],  ["MIDDLE", 5],  ["EVENING", 5],  ["NIGHT", 0],  ["OTHER", 5]],
    "财务部":   [["DEFAULT", 90], ["MORNING", 0],  ["MIDDLE", 5],  ["EVENING", 5],  ["NIGHT", 0],  ["OTHER", 0]],
    "销售部":   [["DEFAULT", 40], ["MORNING", 10], ["MIDDLE", 15], ["EVENING", 25], ["NIGHT", 0],  ["OTHER", 10]],
    "后勤部":   [["MORNING", 30], ["MIDDLE", 30], ["EVENING", 15], ["NIGHT", 10], ["DEFAULT", 10], ["OTHER", 5]],
    "仓储部":   [["MORNING", 35], ["MIDDLE", 25], ["EVENING", 15], ["NIGHT", 10], ["DEFAULT", 10], ["OTHER", 5]],
    "其他":     [["DEFAULT", 50], ["MORNING", 10], ["MIDDLE", 10], ["EVENING", 15], ["NIGHT", 5],  ["OTHER", 10]]
};
const ATTENDANCE_NAME = { DEFAULT: "默认", MORNING: "早", MIDDLE: "中", EVENING: "晚", NIGHT: "夜", OTHER: "其他" };
const SURNAMES = ["张", "李", "王", "赵", "刘", "陈", "杨", "黄", "周", "吴", "徐", "孙", "胡", "朱", "高", "林", "何", "郭", "马", "罗", "梁", "宋", "郑", "谢", "韩", "唐", "冯", "于", "董", "萧", "程", "曹", "袁", "邓", "许", "傅", "沈", "曾", "彭", "吕", "苏", "卢", "蒋", "蔡", "贾", "丁", "魏", "薛", "叶", "阎", "余", "潘", "杜", "戴", "夏", "钟", "汪", "田", "任", "姜", "范", "方", "石", "姚", "谭", "廖", "邹", "熊", "金", "陆", "郝", "孔", "白", "崔", "康", "毛", "邱", "秦", "江", "史", "顾", "侯", "邵", "孟", "龙", "万", "段", "曹", "钱", "汤", "尹", "黎", "易", "常", "武", "乔", "贺", "赖", "龚", "文"];
const GIVEN_NAMES = ["伟", "芳", "娜", "敏", "静", "丽", "强", "磊", "军", "洋", "勇", "艳", "杰", "娟", "涛", "明", "超", "秀英", "霞", "平", "刚", "桂英", "玲", "桂兰", "玉兰", "建华", "志强", "小红", "永生", "建国", "红", "辉", "亮", "刚", "婷", "颖", "雪", "倩", "洁", "浩", "鹏", "宇", "欣", "怡", "晨", "睿", "轩", "泽", "昊", "思", "睿", "梓", "宇航", "梓豪", "梓萱", "梓涵", "一鸣", "思源", "思远", "志强", "志远", "志豪", "志成"];
const REMARKS_POOL = ["", "", "", "", "班长", "副班长", "组长", "党员", "团员", "工会代表", "先进工作者", "新员工", "老员工", "技术骨干", "业务能手", "高潜人才", "", "", "", "", "住宿长", "团支书"];

// =====================================================================
// 1. PERSONNEL — 600 人（在职 500 + 待入职 30 + 已离职 70）
// v2.11.19 规范：与基础资料-在职状态表 EmploymentStatus 一致
// employmentStatusId: 1=在职 / 2=待入职 / 3=已离职
// =====================================================================
function genPersonnel() {
    const out = [];
    const statusPlan = []; // 0..499 在职(1), 500..529 待入职(2), 530..599 已离职(3)
    for (let i = 0; i < 500; i++) statusPlan.push(1);
    for (let i = 0; i < 30; i++) statusPlan.push(2);
    for (let i = 0; i < 70; i++) statusPlan.push(3);
    // shuffle
    for (let i = statusPlan.length - 1; i > 0; i--) {
        const j = Math.floor(rnd() * (i + 1));
        [statusPlan[i], statusPlan[j]] = [statusPlan[j], statusPlan[i]];
    }

    // 部门 × 类型网格权重（按真实分布偏置）
    // 生产部 30%, 技术部 18%, 销售部 12%, 后勤部 12%, 行政部 8%, 财务部 6%, 仓储部 8%, 其他 6%
    // 合同工 50%, 临时工 18%, 外包 14%, 实习生 8%, 驻场 10%
    const deptWeight = [["生产部", 30], ["技术部", 18], ["销售部", 12], ["后勤部", 12], ["行政部", 8], ["财务部", 6], ["仓储部", 8], ["其他", 6]];
    const typeWeight = [["合同工", 50], ["临时工", 18], ["外包", 14], ["实习生", 8], ["驻场", 10]];

    function weightedPick(pairs) {
        const total = pairs.reduce((s, p) => s + p[1], 0);
        let r = rnd() * total;
        for (const [v, w] of pairs) {
            if ((r -= w) < 0) return v;
        }
        return pairs[0][0];
    }

    for (let i = 0; i < 600; i++) {
        const status = statusPlan[i];
        const gender = rnd() < 0.55 ? "M" : "F"; // 略偏男
        const surname = pick(SURNAMES);
        const given = pick(GIVEN_NAMES);
        const name = gender === "M" ? `${surname}${pick(["伟", "强", "磊", "军", "洋", "勇", "杰", "涛", "明", "超", "刚", "辉", "亮", "浩", "鹏", "宇", "欣", "晨", "睿", "轩", "泽", "昊", "思", "梓", "宇航", "志远", "建国", "永生"])}` : `${surname}${pick(["芳", "娜", "敏", "静", "丽", "艳", "娟", "霞", "平", "玲", "婷", "颖", "雪", "倩", "洁", "欣", "怡", "梓萱", "梓涵", "秀英", "桂英", "桂兰", "玉兰", "小红"])}`;

        // 入职日期
        const hireYear = randInt(2019, 2026);
        const hireMonth = String(randInt(1, 12)).padStart(2, "0");
        const hireDay = String(randInt(1, 28)).padStart(2, "0");
        const hireDate = `${hireYear}-${hireMonth}-${hireDay}`;

        // 离职日期（仅已离职员工有 leaveDate，v2.11.19 规范：status=3 才是已离职）
        let leaveDate = null;
        if (status === 3) {
            const leaveYear = randInt(2024, 2026);
            const leaveMonth = String(randInt(1, 12)).padStart(2, "0");
            const leaveDay = String(randInt(1, 28)).padStart(2, "0");
            leaveDate = `${leaveYear}-${leaveMonth}-${leaveDay}`;
        }

        const p = {
            id: i + 1,
            employeeCode: `EMP-2026-${String(i + 1).padStart(3, "0")}`,
            realName: name,
            department: weightedPick(deptWeight),
            employeeType: weightedPick(typeWeight),
            phone: `138${String(randInt(10000000, 99999999))}`,
            hireDate,
            leaveDate,
            dormCode: null, // 先留空，分配住宿阶段处理
            status,
            remark: pick(REMARKS_POOL),
            // v2.11.2：考勤类型（按部门加权分配，见 ATTENDANCE_WEIGHT_BY_DEPT）
            attendanceType: "DEFAULT"
        };
        // 按部门权重分配考勤类型
        const attWeights = ATTENDANCE_WEIGHT_BY_DEPT[p.department] || ATTENDANCE_WEIGHT_BY_DEPT["其他"];
        p.attendanceType = weightedPick(attWeights);

        // KPI 4-A 演示：让约 30% 离职员工保留 dormCode（已离职仍入住异常）
        if (status === 2 && rnd() < 0.30) {
            p.dormCode = `D-${String(randInt(1, 200)).padStart(3, "0")}`;
        }
        // KPI 4-B 演示：让约 5% 在职员工的 hireDate 设为未来日期（提前入住）
        if (status === 1 && rnd() < 0.04) {
            // 入职日期在 2026-08~10 之间
            const futureMonth = String(randInt(8, 10)).padStart(2, "0");
            const futureDay = String(randInt(1, 28)).padStart(2, "0");
            p.hireDate = `2026-${futureMonth}-${futureDay}`;
        }

        out.push(p);
    }
    return out;
}

// =====================================================================
// 2. DORMS — 200 间（1人/2人/4人/6人/8人）
// =====================================================================
function genDorms() {
    const out = [];
    // 房型配置：[数量, capacity, roomCount, remark]
    const types = [
        [10, 1, 1, "1人间（干部房）"],
        [30, 2, 1, "2人间"],
        [120, 4, 1, "4人间"],
        [30, 6, 1, "6人间"],
        [10, 8, 2, "合租房（2户×4人）"]
    ];

    let idx = 0;
    let floor = 1;
    let inFloor = 0;
    const perFloor = 6;
    let buildingIdx = 0;

    for (const [count, cap, rooms, remark] of types) {
        for (let i = 0; i < count; i++) {
            idx++;
            if (inFloor >= perFloor) { floor++; inFloor = 0; }
            if (floor > 6) { buildingIdx++; floor = 1; inFloor = 0; }
            const building = BUILDINGS[Math.min(buildingIdx, BUILDINGS.length - 1)];
            const dormCode = `D-${String(idx).padStart(3, "0")}`;
            out.push({
                id: idx,
                dormCode,
                address: `${building}${floor}${String(100 + (inFloor + 1) * 10 + (i % 10)).slice(-2)}室`,
                building,
                floor,
                roomCount: rooms,
                capacity: cap,
                bedCount: cap,
                remark,
                isActive: rnd() < 0.97 // 97% 启用
            });
            inFloor++;
        }
    }
    return out;
}

// =====================================================================
// 3. 住宿分配 — 为 500 在职员工分配 dormCode
// =====================================================================
function assignDorms(personnel, dorms) {
    // 启用住宿按 capacity 累加
    const activeDorms = dorms.filter(d => d.isActive);

    // 当前在宿人数 + 考勤类型分布（按住宿记录）
    const dormOccupied = {};
    const dormAttendance = {};  // { dormCode: { DEFAULT: 0, MORNING: 0, ... } }
    activeDorms.forEach(d => {
        dormOccupied[d.dormCode] = 0;
        dormAttendance[d.dormCode] = {};
    });

    const active = personnel.filter(p => p.status === 1);
    // shuffle
    for (let i = active.length - 1; i > 0; i--) {
        const j = Math.floor(rnd() * (i + 1));
        [active[i], active[j]] = [active[j], active[i]];
    }

    for (const p of active) {
        // v2.11.2 智能分配：按考勤类型一致性优先级
        // 1) 找同考勤类型 + 有空位的住宿
        let candidates = activeDorms.filter(d =>
            dormOccupied[d.dormCode] < d.capacity &&
            dormAttendance[d.dormCode][p.attendanceType] > 0  // 该住宿已有同类型员工
        );
        // 2) 兜底：任意有空位的住宿
        if (candidates.length === 0) {
            candidates = activeDorms.filter(d => dormOccupied[d.dormCode] < d.capacity);
        }

        let assigned = null;
        if (candidates.length > 0) {
            // 优先选择考勤一致性最高（占比最高）的住宿
            candidates.sort((a, b) => {
                const aCount = dormAttendance[a.dormCode][p.attendanceType] || 0;
                const bCount = dormAttendance[b.dormCode][p.attendanceType] || 0;
                if (bCount !== aCount) return bCount - aCount;
                // 同分时按随机性打破
                return rnd() - 0.5;
            });
            assigned = candidates[0].dormCode;
            dormOccupied[assigned]++;
            dormAttendance[assigned][p.attendanceType] = (dormAttendance[assigned][p.attendanceType] || 0) + 1;
        } else {
            // 完全无空床位时记录异常
            assigned = `D-${String(randInt(1, 200)).padStart(3, "0")}`;
        }
        p.dormCode = assigned;
    }
    return dormOccupied;
}

// =====================================================================
// 4. RESIDENCIES — 在宿 500 + 历史 50
// =====================================================================
function genResidencies(personnel) {
    const out = [];
    let id = 1;

    // 当前在宿：500 个在职员工
    const active = personnel.filter(p => p.status === 1 && p.dormCode);
    for (const p of active) {
        const ymd = p.hireDate;
        const checkInDate = ymd;
        // 微调 createdAt 时间
        const hh = String(randInt(8, 17)).padStart(2, "0");
        const mm = String(randInt(0, 59)).padStart(2, "0");
        const ss = String(randInt(0, 59)).padStart(2, "0");
        out.push({
            id: id++, employeeId: p.id, dormCode: p.dormCode,
            checkInDate, leaveDate: null, status: 1, reason: "入职",
            remark: "", operatorId: 1, createdAt: `${checkInDate}T${hh}:${mm}:${ss}`
        });
    }

    // 历史记录 50 条：从已离职/有 leaveDate 的员工
    // v2.11.19 规范：仅已离职(status=3)员工有历史住宿记录
    const left = personnel.filter(p => p.status === 3);
    for (let i = 0; i < Math.min(50, left.length); i++) {
        const p = left[i];
        if (!p.leaveDate) continue;
        out.push({
            id: id++, employeeId: p.id, dormCode: p.dormCode || `D-${String(randInt(1, 200)).padStart(3, "0")}`,
            checkInDate: p.hireDate, leaveDate: p.leaveDate, status: 2,
            reason: "已离职",
            remark: "已退宿", operatorId: 1,
            createdAt: `${p.hireDate}T${String(randInt(8, 17)).padStart(2, "0")}:${String(randInt(0, 59)).padStart(2, "0")}:${String(randInt(0, 59)).padStart(2, "0")}`
        });
    }
    return out;
}

// =====================================================================
// 5. BOOKINGS — 在宿 500 + 退房 50 + 预订 20 + 异常 10
// =====================================================================
function genBookings(personnel, dorms) {
    const out = [];
    let id = 1;

    // 当前在宿：500 个在职员工，type=1, status=2
    const active = personnel.filter(p => p.status === 1 && p.dormCode);
    // v2.11.2 增补 k：约 30 名员工有调宿记录（标记 usedTransfer）
    const shuffled = active.slice();
    for (let i = shuffled.length - 1; i > 0; i--) {
        const j = Math.floor(rnd() * (i + 1));
        [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
    }
    const transferSet = new Set(shuffled.slice(0, 30).map(p => p.id));
    const allDorms = dorms.filter(d => d.isActive).map(d => d.dormCode);
    for (const p of active) {
        const hh = String(randInt(8, 17)).padStart(2, "0");
        const mm = String(randInt(0, 59)).padStart(2, "0");
        const ss = String(randInt(0, 59)).padStart(2, "0");
        out.push({
            id: id++,
            employeeId: p.id, employeeCode: p.employeeCode, employeeName: p.realName,
            phone: p.phone, department: p.department,
            dormCode: p.dormCode, type: 1, bookingDate: p.hireDate, status: 2,
            reason: "入职", remark: "",
            registrationDate: `${p.hireDate}T${hh}:${mm}:${ss}`,
            registrar: "admin"
        });
        // 调宿记录：原住宿退房 + 新住宿入住
        if (transferSet.has(p.id)) {
            const transferDate = `2026-07-${String(randInt(10, 20)).padStart(2, "0")}`;
            // 找一个不同的住宿
            let newDorm = p.dormCode;
            while (newDorm === p.dormCode) {
                newDorm = allDorms[randInt(0, allDorms.length - 1)];
            }
            // 1) 原住宿退房
            out.push({
                id: id++,
                employeeId: p.id, employeeCode: p.employeeCode, employeeName: p.realName,
                phone: p.phone, department: p.department,
                dormCode: p.dormCode, type: 2, bookingDate: transferDate, status: 3,
                reason: "调宿", remark: `调至 ${newDorm}`, registrationDate: `${transferDate}T${String(randInt(9, 17)).padStart(2, "0")}:${String(randInt(0, 59)).padStart(2, "0")}:${ss}`,
                registrar: "admin"
            });
            // 2) 新住宿入住
            const nextDay = `2026-07-${String(parseInt(transferDate.slice(-2)) + 1).padStart(2, "0")}`;
            out.push({
                id: id++,
                employeeId: p.id, employeeCode: p.employeeCode, employeeName: p.realName,
                phone: p.phone, department: p.department,
                dormCode: newDorm, type: 1, bookingDate: nextDay, status: 2,
                reason: "调宿", remark: `从 ${p.dormCode} 调入`, registrationDate: `${nextDay}T${String(randInt(9, 17)).padStart(2, "0")}:${String(randInt(0, 59)).padStart(2, "0")}:${ss}`,
                registrar: "admin"
            });
        }
    }

    // 已退房：50 条
    const left = personnel.filter(p => p.status === 2).slice(0, 50);
    for (const p of left) {
        if (!p.leaveDate) continue;
        // 入住
        out.push({
            id: id++, employeeId: p.id, employeeCode: p.employeeCode, employeeName: p.realName,
            phone: p.phone, department: p.department,
            dormCode: p.dormCode || `D-${String(randInt(1, 200)).padStart(3, "0")}`,
            type: 1, bookingDate: p.hireDate, status: 3,
            reason: "入职", remark: "历史入住", registrationDate: `${p.hireDate}T08:00:00`,
            registrar: "admin"
        });
        // 退房
        out.push({
            id: id++, employeeId: p.id, employeeCode: p.employeeCode, employeeName: p.realName,
            phone: p.phone, department: p.department,
            dormCode: p.dormCode || `D-${String(randInt(1, 200)).padStart(3, "0")}`,
            type: 2, bookingDate: p.leaveDate, status: 3,
            reason: "离职", remark: "已退房", registrationDate: `${p.leaveDate}T14:00:00`,
            registrar: "admin"
        });
    }

    // 预订（未来日期）：20 条
    for (let i = 0; i < 20; i++) {
        const month = randInt(8, 9);
        const day = randInt(1, 28);
        const date = `2026-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
        const p = active[randInt(0, active.length - 1)];
        out.push({
            id: id++, employeeId: p.id + 1000, employeeCode: `EMP-2027-${String(randInt(1, 999)).padStart(3, "0")}`,
            employeeName: pick(SURNAMES) + pick(GIVEN_NAMES), phone: `138${String(randInt(10000000, 99999999))}`,
            department: pick(DEPARTMENTS), dormCode: `D-${String(randInt(1, 200)).padStart(3, "0")}`,
            type: 1, bookingDate: date, status: 1, reason: "新员工入职",
            remark: "提前预订", registrationDate: `2026-07-${String(randInt(1, 10)).padStart(2, "0")}T09:00:00`,
            registrar: "admin"
        });
    }

    // 异常：10 条
    const abnormalReasons = [
        "已离职仍入住（异常）",
        "未到入职日期提前入住（异常）",
        "超期未办理入住（异常）",
        "调宿后未释放原床位（异常）",
        "工号重复登记（异常）"
    ];
    for (let i = 0; i < 10; i++) {
        const p = left[randInt(0, left.length - 1)] || active[randInt(0, active.length - 1)];
        const date = `2026-07-${String(randInt(1, 10)).padStart(2, "0")}`;
        out.push({
            id: id++, employeeId: p.id, employeeCode: p.employeeCode, employeeName: p.realName,
            phone: p.phone, department: p.department,
            dormCode: `D-${String(randInt(100, 200)).padStart(3, "0")}`,
            type: 1, bookingDate: date, status: 1, reason: "异常登记",
            remark: abnormalReasons[i % abnormalReasons.length],
            registrationDate: `${date}T${String(randInt(8, 17)).padStart(2, "0")}:${String(randInt(0, 59)).padStart(2, "0")}:00`,
            registrar: "admin"
        });
    }

    return out;
}

// =====================================================================
// 6. METER_RECORDS — 每间启用住宿 2 条（上月 + 本月）
// =====================================================================
function genMeters(dorms) {
    const out = [];
    let id = 1;
    const activeDorms = dorms.filter(d => d.isActive);
    // 基线读数（每间住宿累积基数）
    const baseline = {};
    activeDorms.forEach(d => {
        baseline[d.dormCode] = {
            cold: randFloat(500, 5000),
            hot: randFloat(100, 1500),
            electric: randFloat(2000, 10000)
        };
    });

    // 上月 2026-06
    for (const d of activeDorms) {
        const b = baseline[d.dormCode];
        out.push({
            id: id++, dormCode: d.dormCode, dormAddress: d.address, readMonth: "2026-06",
            coldMeter: +b.cold.toFixed(2), hotMeter: +b.hot.toFixed(2), electricMeter: +b.electric.toFixed(2),
            operator: rnd() < 0.5 ? "陈师傅" : "刘师傅",
            deviceSn: rnd() < 0.5 ? "PDA-SN-001" : "PDA-SN-002",
            status: 1, remark: "",
            createdAt: `2026-06-08 ${String(randInt(9, 11)).padStart(2, "0")}:${String(randInt(0, 59)).padStart(2, "0")}:${String(randInt(0, 59)).padStart(2, "0")}`
        });
        // 用量（累积）
        baseline[d.dormCode].cold += randFloat(2, 12);
        baseline[d.dormCode].hot += randFloat(1, 6);
        baseline[d.dormCode].electric += randFloat(20, 150);
    }

    // 本月 2026-07
    for (const d of activeDorms) {
        const b = baseline[d.dormCode];
        const r = rnd();
        let status = 1, remark = "";
        if (r < 0.04) { status = 2; remark = "热水读数录错，已修正"; }
        else if (r < 0.06) { status = 3; remark = "重复上传已作废"; }
        out.push({
            id: id++, dormCode: d.dormCode, dormAddress: d.address, readMonth: "2026-07",
            coldMeter: +b.cold.toFixed(2), hotMeter: +b.hot.toFixed(2), electricMeter: +b.electric.toFixed(2),
            operator: rnd() < 0.5 ? "陈师傅" : "刘师傅",
            deviceSn: rnd() < 0.5 ? "PDA-SN-001" : "PDA-SN-002",
            status, remark,
            createdAt: `2026-07-08 ${String(randInt(9, 11)).padStart(2, "0")}:${String(randInt(0, 59)).padStart(2, "0")}:${String(randInt(0, 59)).padStart(2, "0")}`
        });
    }
    return out;
}

// =====================================================================
// 7. DORM_BILLS_202607 — 190 条（启用住宿 200 中排除 ~10 条）
// =====================================================================
function genDormBills(dorms, meters) {
    const out = [];
    let id = 1;
    // 取本月有效抄表记录（status !== 3）
    const thisMonth = {};
    meters.filter(m => m.readMonth === "2026-07" && m.status !== 3).forEach(m => {
        thisMonth[m.dormCode] = m;
    });

    // 取上月记录计算用量
    const prevMonth = {};
    meters.filter(m => m.readMonth === "2026-06").forEach(m => {
        prevMonth[m.dormCode] = m;
    });

    // v2.11：5 种员工类型各自单价（每标准适用 1 种类型）
    const prices = {
        "合同工": { cold: 4.50, hot: 25.00, elec: 0.80, stdId: 1 },
        "临时工": { cold: 4.20, hot: 23.00, elec: 0.75, stdId: 5 },
        "外包":   { cold: 5.00, hot: 28.00, elec: 0.90, stdId: 2 },
        "实习生": { cold: 3.80, hot: 20.00, elec: 0.65, stdId: 6 },
        "驻场":   { cold: 4.80, hot: 26.00, elec: 0.85, stdId: 7 }
    };

    // 计算有在住员工的启用住宿集合（确保 dorm_bills 与 employee_bills 数据守恒）
    const dormsWithResidents = new Set(
        personnel.filter(p => p.status === 1 && p.dormCode).map(p => p.dormCode)
    );

    let dormIdx = 0;
    for (const d of dorms.filter(x => x.isActive)) {
        dormIdx++;
        // 跳过约 5%（每 25 间跳 1）→ 期望 ~182
        if (dormIdx % 25 === 0) continue;
        // 仅对有在住员工的住宿生成账单
        if (!dormsWithResidents.has(d.dormCode)) continue;
        const cur = thisMonth[d.dormCode];
        const prev = prevMonth[d.dormCode];
        if (!cur || !prev) continue;
        const coldUsage = +(cur.coldMeter - prev.coldMeter).toFixed(2);
        const hotUsage = +(cur.hotMeter - prev.hotMeter).toFixed(2);
        const elecUsage = +(cur.electricMeter - prev.electricMeter).toFixed(2);
        if (coldUsage < 0 || hotUsage < 0 || elecUsage < 0) continue;

        // 用住宿内主要员工类型决定标准
        out.push({
            id: id++, dormCode: d.dormCode, dormAddress: d.address, building: d.building,
            billingMonth: "2026-07",
            coldUsage, hotUsage, electricityUsage: elecUsage,
            coldAmount: 0, hotAmount: 0, electricityAmount: 0, // 将在分摊阶段按类型填充
            totalAmount: 0,
            residentCount: 0,
            standardId: 1,
            standardName: "2026年下半年-按类型",
            isPublished: rnd() < 0.7
        });
    }
    return out;
}

// =====================================================================
// 8. EMPLOYEE_BILLS_202607 — 按入住天数 × 日均费用 × 占比 精确计算（v2.11.2 增补 k）
//
// 计算规则（核心）：
//   每人某表项分摊 = (该住宿该表项总费用 / 当月天数) × 该员工在该住宿的入住天数 / ∑(同住宿入住天数)
//
// 调宿人员：生成 2 条记录（按 BOOKINGS 中该员工的每段入住分别计算）
// 空床位：按"实际入住人天数"加权分摊
// =====================================================================
function genEmployeeBills(personnel, dormBills, meters, bookings) {
    const out = [];
    let id = 1;
    const MONTH_DAYS = 31; // 2026-07 有 31 天

    // 统一单价（按合同工基准）
    const PRICE = { cold: 4.50, hot: 25.00, elec: 0.80 };

    // 计算每个住宿的当月用量
    const thisMonth = {};
    meters.filter(m => m.readMonth === "2026-07" && m.status !== 3).forEach(m => {
        thisMonth[m.dormCode] = m;
    });
    const prevMonth = {};
    meters.filter(m => m.readMonth === "2026-06").forEach(m => {
        prevMonth[m.dormCode] = m;
    });

    // 填充 dormBill 的三表金额（按统一单价）
    dormBills.forEach(b => {
        const cur = thisMonth[b.dormCode], prev = prevMonth[b.dormCode];
        if (!cur || !prev) return;
        const coldUsage = Math.max(0, +(cur.coldMeter - prev.coldMeter).toFixed(2));
        const hotUsage = Math.max(0, +(cur.hotMeter - prev.hotMeter).toFixed(2));
        const elecUsage = Math.max(0, +(cur.electricMeter - prev.electricMeter).toFixed(2));
        b.coldAmount = +(coldUsage * PRICE.cold).toFixed(2);
        b.hotAmount = +(hotUsage * PRICE.hot).toFixed(2);
        b.electricityAmount = +(elecUsage * PRICE.elec).toFixed(2);
        b.totalAmount = +(b.coldAmount + b.hotAmount + b.electricityAmount).toFixed(2);
        b.residentCount = (personnel.filter(p => p.status === 1 && p.dormCode === b.dormCode)).length;
    });

    // 收集所有员工的入住段（按 BOOKINGS）
    // 段结构：{ employeeId, dormCode, stayDays, type: LIVING/TRANSFER }
    const empSegments = collectStaySegments(bookings, personnel);

    // 按 dormCode 聚合所有员工段（入住段）
    const segByDorm = {};
    empSegments.forEach(seg => {
        if (!segByDorm[seg.dormCode]) segByDorm[seg.dormCode] = [];
        segByDorm[seg.dormCode].push(seg);
    });

    // 按 dormCode 计算入住总人天数
    const totalStayDaysByDorm = {};
    Object.keys(segByDorm).forEach(code => {
        totalStayDaysByDorm[code] = segByDorm[code].reduce((s, seg) => s + seg.stayDays, 0);
    });

    // 按 dormCode 遍历账单
    for (const b of dormBills) {
        const segs = segByDorm[b.dormCode] || [];
        const totalDays = totalStayDaysByDorm[b.dormCode] || 0;
        if (segs.length === 0 || totalDays === 0) continue;

        // 日均费用
        const dailyCold = b.coldAmount / MONTH_DAYS;
        const dailyHot = b.hotAmount / MONTH_DAYS;
        const dailyElec = b.electricityAmount / MONTH_DAYS;

        // 每个员工段按其在总人天数的占比分摊
        segs.forEach(seg => {
            const ratio = seg.stayDays / totalDays;
            const empCold = +(dailyCold * MONTH_DAYS * ratio).toFixed(2);
            const empHot = +(dailyHot * MONTH_DAYS * ratio).toFixed(2);
            const empElec = +(dailyElec * MONTH_DAYS * ratio).toFixed(2);
            out.push({
                id: id++,
                employeeId: seg.employeeId,
                employeeCode: seg.employeeCode,
                employeeName: seg.realName,
                department: seg.department,
                employeeType: seg.employeeType,
                dormCode: seg.dormCode,
                billingMonth: "2026-07",
                stayDays: seg.stayDays,
                stayStartDate: seg.stayStartDate,
                stayEndDate: seg.stayEndDate,
                segmentType: seg.segmentType, // LIVING / TRANSFER_OUT / TRANSFER_IN
                shareRatio: +ratio.toFixed(4),
                residentCount: segs.length,
                coldShareAmount: empCold,
                hotShareAmount: empHot,
                electricityShareAmount: empElec,
                totalShareAmount: +(empCold + empHot + empElec).toFixed(2),
                isPublished: b.isPublished
            });
        });
    }
    return out;

    // 辅助：收集所有员工的当月入住段
    function collectStaySegments(bookings, personnel) {
        const segs = [];
        const empById = {};
        personnel.forEach(p => empById[p.id] = p);

        // 按员工+住宿聚合 BOOKINGS，找出每段的入住起止
        // 段结构：type=1 入住 + type=2 退房 配对
        const records = {};
        bookings.forEach(b => {
            const key = `${b.employeeId}#${b.dormCode}`;
            if (!records[key]) records[key] = { employeeId: b.employeeId, dormCode: b.dormCode, ins: [], outs: [] };
            if (b.type === 1) records[key].ins.push(b);
            if (b.type === 2) records[key].outs.push(b);
        });

        // 对每个 (employee, dorm) 计算入住段
        Object.values(records).forEach(rec => {
            const emp = empById[rec.employeeId];
            if (!emp) return;
            // 按 bookingDate 排序
            rec.ins.sort((a, b) => a.bookingDate.localeCompare(b.bookingDate));
            rec.outs.sort((a, b) => a.bookingDate.localeCompare(b.bookingDate));
            // 配对：第 i 个入住 对应 第 i 个退房
            for (let i = 0; i < rec.ins.length; i++) {
                const inDate = rec.ins[i].bookingDate;
                const outDate = rec.outs[i] ? rec.outs[i].bookingDate : "2026-07-31";
                // 计算当月 2026-07 内的入住天数
                const startD = inDate < "2026-07-01" ? "2026-07-01" : inDate;
                const endD = outDate > "2026-07-31" ? "2026-07-31" : outDate;
                if (startD > endD) continue;
                const stayDays = daysBetween(startD, endD);
                if (stayDays <= 0) continue;
                const segType = rec.outs[i] ? "TRANSFER_OUT" : "LIVING";
                segs.push({
                    employeeId: rec.employeeId,
                    employeeCode: emp.employeeCode,
                    realName: emp.realName,
                    department: emp.department,
                    employeeType: emp.employeeType,
                    dormCode: rec.dormCode,
                    stayDays,
                    stayStartDate: startD,
                    stayEndDate: endD,
                    segmentType: segType
                });
            }
        });
        return segs;
    }

    // 辅助：日期差（包含两端）
    function daysBetween(d1, d2) {
        const a = new Date(d1);
        const b = new Date(d2);
        return Math.round((b - a) / 86400000) + 1;
    }
}

// =====================================================================
// 9. MONTHLY_COST_TREND — 重新计算 12 月（含 200 间）
// =====================================================================
function genCostTrend(dormBills) {
    const totalNow = dormBills.reduce((s, b) => s + b.totalAmount, 0);
    // 反推 12 月序列（让 2026-07 等于实际值）
    const ratios = [0.78, 0.85, 0.92, 0.95, 1.02, 0.88, 0.96, 1.10, 0.94, 0.98, 1.05, 1.00];
    const months = ["2025-08", "2025-09", "2025-10", "2025-11", "2025-12", "2026-01", "2026-02", "2026-03", "2026-04", "2026-05", "2026-06", "2026-07"];
    return months.map((m, i) => ({ month: m, totalAmount: +(totalNow * ratios[i]).toFixed(2) }));
}

// =====================================================================
// Main
// =====================================================================
const personnel = genPersonnel();
const dorms = genDorms();
const dormOccupied = assignDorms(personnel, dorms);
const residencies = genResidencies(personnel);
const bookings = genBookings(personnel, dorms);
const meters = genMeters(dorms);
const dormBills = genDormBills(dorms, meters);
const employeeBills = genEmployeeBills(personnel, dormBills, meters, bookings);
const costTrend = genCostTrend(dormBills);

const output = {
    meta: {
        generatedAt: new Date().toISOString(),
        seed: 20260711,
        counts: {
            personnel: personnel.length,
            personnelActive: personnel.filter(p => p.status === 1).length,
            personnelLeft: personnel.filter(p => p.status === 2).length,
            personnelSuspended: personnel.filter(p => p.status === 3).length,
            dorms: dorms.length,
            dormsActive: dorms.filter(d => d.isActive).length,
            totalCapacity: dorms.filter(d => d.isActive).reduce((s, d) => s + d.capacity, 0),
            occupiedBeds: Object.values(dormOccupied).reduce((s, n) => s + n, 0),
            residencies: residencies.length,
            bookings: bookings.length,
            meterRecords: meters.length,
            dormBills: dormBills.length,
            employeeBills: employeeBills.length
        }
    },
    personnel, dorms, residencies, bookings, meters, dormBills, employeeBills, costTrend
};

// 同时输出 5 条 BILLING_STANDARDS（v2.11：每标准适用 1 种类型）
output.billingStandards = [
    { id: 1, standardName: "2026年下半年-合同工", effectiveFrom: "2026-07-01", effectiveTo: null, hotWaterPrice: 25.00, coldWaterPrice: 4.50, electricityPrice: 0.8000, applicableType: "合同工", isActive: true, remark: "2026 年下半年合同工标准" },
    { id: 2, standardName: "2026年下半年-外包",   effectiveFrom: "2026-07-01", effectiveTo: null, hotWaterPrice: 28.00, coldWaterPrice: 5.00, electricityPrice: 0.9000, applicableType: "外包",   isActive: true, remark: "2026 年下半年外包标准" },
    { id: 3, standardName: "2026年上半年-合同工", effectiveFrom: "2026-01-01", effectiveTo: "2026-06-30", hotWaterPrice: 23.00, coldWaterPrice: 4.20, electricityPrice: 0.7500, applicableType: "合同工", isActive: false, remark: "2026 年上半年合同工标准（已过期）" },
    { id: 4, standardName: "2025年下半年-合同工", effectiveFrom: "2025-07-01", effectiveTo: "2025-12-31", hotWaterPrice: 22.00, coldWaterPrice: 4.00, electricityPrice: 0.7000, applicableType: "合同工", isActive: false, remark: "2025 年下半年合同工标准" },
    { id: 5, standardName: "2026年下半年-临时工", effectiveFrom: "2026-07-01", effectiveTo: null, hotWaterPrice: 23.00, coldWaterPrice: 4.20, electricityPrice: 0.7500, applicableType: "临时工", isActive: true, remark: "2026 年下半年临时工标准" },
    { id: 6, standardName: "2026年下半年-实习生", effectiveFrom: "2026-07-01", effectiveTo: null, hotWaterPrice: 20.00, coldWaterPrice: 3.80, electricityPrice: 0.6500, applicableType: "实习生", isActive: true, remark: "2026 年下半年实习生标准" },
    { id: 7, standardName: "2026年下半年-驻场",   effectiveFrom: "2026-07-01", effectiveTo: null, hotWaterPrice: 26.00, coldWaterPrice: 4.80, electricityPrice: 0.8500, applicableType: "驻场",   isActive: true, remark: "2026 年下半年驻场标准" }
];

console.log(JSON.stringify(output, null, 2));