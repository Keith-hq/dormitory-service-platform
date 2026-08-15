export const roleOptions = [
  { key: 'student', label: '学生' },
  { key: 'admin', label: '宿管' },
  { key: 'counselor', label: '辅导员' },
  { key: 'repairman', label: '维修员' },
  { key: 'super_admin', label: '超管' }
]

export const roleScenes = {
  student: {
    roleLabel: '学生生活空间',
    identity: '林晓满',
    location: '桂苑 A 栋 · 503 室',
    title: '今天的宿舍生活，井然有序',
    subtitle: '晚归申请已通过，报修师傅预计 16:30 到达。',
    stamp: '居住良好',
    nav: ['生活首页', '我的住宿', '费用中心', '报修中心', '校园生活', '生活报告'],
    metrics: [
      { label: '钱包余额', value: '¥268.40', delta: '充足' },
      { label: '本月水电', value: '¥42.70', delta: '较上月 −8%' },
      { label: '信用分', value: '96', delta: '+2' }
    ],
    quickActions: ['发起报修', '晚归申请', '费用缴纳', '查看住宿'],
    notifications: [
      { type: '楼栋', title: '宿舍楼热水系统检修通知', time: '今天 13:20', unread: true },
      { type: '账单', title: '8 月水电账单已生成', time: '昨天 18:45', unread: true },
      { type: '安全', title: '开学季防诈骗安全提醒', time: '8 月 13 日', unread: false }
    ],
    tasks: [
      { time: '16:30', title: '书桌插座报修', meta: '师傅：陈工 · 处理中', level: 'amber' },
      { time: '21:45', title: '晚归申请', meta: '辅导员已审批 · 已通过', level: 'green' },
      { time: '周五', title: '8 月水电账单', meta: '待缴 ¥42.70', level: 'blue' }
    ],
    timeline: [
      { day: '今天', title: '宿舍卫生自查', note: '拍照提交，预计 3 分钟' },
      { day: '明天', title: '空调滤网清洁', note: '物业将在 14:00 上门' },
      { day: '18 日', title: '楼层安全巡检', note: '无需留寝，提前收好贵重物品' }
    ]
  },
  admin: {
    roleLabel: '楼栋运营空间',
    identity: '周值班',
    location: '桂苑 A 栋 · 一层值班台',
    title: '今晚有 4 件事需要跟进',
    subtitle: '两项维修即将超时，A503 的晚归申请等待核验。',
    stamp: '值班在岗',
    nav: ['运营首页', '住宿管理', '退宿清算', '费用管理', '安全巡检', '设施公告'],
    metrics: [
      { label: '在住人数', value: '286', delta: '入住率 94%' },
      { label: '待办事项', value: '12', delta: '高优先级 4' },
      { label: '今日访客', value: '18', delta: '在楼 3 人' }
    ],
    quickActions: ['办理入住', '退宿验房', '发布通知', '新增巡检'],
    notifications: [
      { type: '巡检', title: '今晚消防巡检范围已更新', time: '今天 14:05', unread: true },
      { type: '退宿', title: 'B207 提交退宿清算申请', time: '今天 13:48', unread: true },
      { type: '楼栋', title: '桂苑停电演练安排', time: '昨天 16:30', unread: false }
    ],
    tasks: [
      { time: '15:40', title: 'A503 插座报修', meta: '已派单 · 距超时 42 分', level: 'amber' },
      { time: '16:10', title: 'B207 退宿验房', meta: '等待宿管确认', level: 'blue' },
      { time: '20:00', title: '三层消防通道巡检', meta: '本班次重点任务', level: 'red' }
    ],
    timeline: [
      { day: '交班前', title: '核对三笔退宿清算', note: 'B207、B411、C106' },
      { day: '20:00', title: '消防通道巡检', note: '重点检查三层东侧' },
      { day: '22:30', title: '晚归名单核验', note: '当前已登记 7 人' }
    ]
  },
  counselor: {
    roleLabel: '学生安全审批空间',
    identity: '陈辅导员',
    location: '计算机学院 · 学工办公室',
    title: '18 份离校申请等待审批',
    subtitle: '其中 3 份目的地信息不完整，2 名学生返校时间已逾期。',
    stamp: '审批在线',
    nav: ['审批首页', '离校审批', '去向统计'],
    metrics: [
      { label: '待审批', value: '18', delta: '高优先级 3' },
      { label: '今日通过', value: '24', delta: '通过率 92%' },
      { label: '逾期未返', value: '2', delta: '需要联系' }
    ],
    quickActions: ['批量审批', '联系学生', '去向统计', '导出名单'],
    notifications: [
      { type: '逾期', title: '两名学生超过计划返校时间', time: '今天 14:18', unread: true },
      { type: '申请', title: '国庆离校申请新增 18 份', time: '今天 13:40', unread: true },
      { type: '学院', title: '安全报备口径已更新', time: '昨天 16:20', unread: false }
    ],
    tasks: [
      { time: '15:20', title: '审核省外离校申请', meta: '8 人 · 目的地已核验', level: 'amber' },
      { time: '16:00', title: '联系逾期未返学生', meta: '2 人 · 等待确认', level: 'red' },
      { time: '17:30', title: '提交学院去向统计', meta: '数据完成度 96%', level: 'blue' }
    ],
    timeline: [
      { day: '现在', title: '处理待补充申请', note: '缺少详细目的地 3 份' },
      { day: '16:00', title: '核对返校名单', note: '重点关注逾期学生' },
      { day: '下班前', title: '导出安全动态表', note: '发送至学院值班群' }
    ]
  },
  repairman: {
    roleLabel: '维修协作空间',
    identity: '陈师傅',
    location: '桂苑片区 · 维修一组',
    title: '下一张工单，距超时还有 42 分钟',
    subtitle: 'A503 插座故障已定位，所需材料库存充足。',
    stamp: '维修在岗',
    nav: ['维修首页', '待接工单', '处理中', '维修记录', '材料领用'],
    metrics: [
      { label: '待接工单', value: '9', delta: '紧急 2 单' },
      { label: '处理中', value: '3', delta: '临近超时 1' },
      { label: '今日完成', value: '6', delta: '按时率 97%' }
    ],
    quickActions: ['领取工单', '更新进度', '登记材料', '提交完成'],
    notifications: [
      { type: 'SLA', title: 'A503 工单将在 42 分钟后超时', time: '刚刚', unread: true },
      { type: '派单', title: 'B411 门锁工单已指派给你', time: '今天 14:02', unread: true },
      { type: '库存', title: '五孔插座库存已补充', time: '昨天 18:10', unread: false }
    ],
    tasks: [
      { time: '15:40', title: 'A503 插座维修', meta: '处理中 · 已定位故障', level: 'amber' },
      { time: '16:20', title: 'B411 门锁更换', meta: '已派单 · 待上门', level: 'blue' },
      { time: '18:00', title: 'C106 空调检修', meta: '待接单 · 普通', level: 'green' }
    ],
    timeline: [
      { day: '现在', title: '完成 A503 工单', note: '登记插座材料消耗' },
      { day: '16:20', title: '前往 B411', note: '携带门锁芯与工具箱' },
      { day: '交班前', title: '提交维修日志', note: '补充现场照片与结果' }
    ]
  },
  super_admin: {
    roleLabel: '校级治理空间',
    identity: '系统管理员',
    location: '校级宿舍运营中心',
    title: '把组织、权限与审计放在同一张桌上',
    subtitle: '4 个园区运行稳定，今日有 2 项高风险操作待复核。',
    stamp: '系统正常',
    nav: ['全局概览', '住宿档案', '账号权限', '业务配置', '审计中心', '数据报告'],
    metrics: [
      { label: '管理床位', value: '4,862', delta: '可用 318' },
      { label: '今日请求', value: '1,284', delta: '成功率 99.8%' },
      { label: '风险事件', value: '2', delta: '等待复核' }
    ],
    quickActions: ['新增账号', '角色授权', '查看审计', '导出报表'],
    notifications: [
      { type: '风险', title: '两项高风险授权等待复核', time: '今天 14:22', unread: true },
      { type: '系统', title: '月度数据快照将在 17:30 执行', time: '今天 11:10', unread: true },
      { type: '档案', title: '桂苑楼栋档案批量更新完成', time: '昨天 19:05', unread: false }
    ],
    tasks: [
      { time: '14:22', title: '宿管角色批量授权', meta: '申请人：运营中心 · 待复核', level: 'red' },
      { time: '15:05', title: '桂苑楼栋档案更新', meta: '18 个字段发生变化', level: 'blue' },
      { time: '17:30', title: '月度数据快照', meta: '计划任务 · 准备就绪', level: 'green' }
    ],
    timeline: [
      { day: '现在', title: '复核高风险授权', note: '涉及 12 个账号' },
      { day: '18:00', title: '检查数据快照', note: '数据库备份与审计摘要' },
      { day: '周一', title: '发布月度运营报告', note: '面向学生处与后勤处' }
    ]
  }
}
