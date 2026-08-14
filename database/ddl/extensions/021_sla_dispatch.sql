-- 扩展表迁移 021：难点⑤ SLA 派单 DDL（三段式合并版）。
-- 2026-08-13 定稿（数据库负责人确认，C-033/C-034）。
-- 在已有环境上执行于 ddl/extensions/020_repair_late_hygiene_room_sequences.sql 之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> extensions/011
--   -> ... -> extensions/019 -> extensions/020 -> extensions/021。
-- 本脚本只 ALTER 基线表加列/约束与建序列，不重建、不改写既有列。
--
-- 为什么走编号迁移（C-033/C-034，2026-08-13 确认）？
--   难点⑤（PR #45）把 2 个增列、2 个 CHECK 与 1 个序列内联在
--   database/sp/sp_sla_dispatch.sql 中：不在编号迁移家族内、verify 无结构
--   检查、全量重建链无落点。裁决：剥离为独立编号迁移，一个文件三段式
--   （Part A/B/C），枚举以 PRD 为准（用户 2026-08-13 拍板）。
--
-- ⚠️⚠️ 执行前提（重要） ⚠️⚠️
--   Part A / Part B 可随时先行执行（独立增列与序列，无依赖）。
--   Part C（两个 CHECK）依赖 PR #45 状态机改造完成：
--     - PR #45 的 SP_Claim_Ticket / SP_Complete_Repair 现写入'处理中'，
--       CHECK 建成后这些写入会 ORA-02290；库中若已有'处理中'数据，
--       ADD CONSTRAINT 本身会 ORA-02293 失败。
--     - 必须等 PR #45 按 4 值状态机改完（派单=待处理->已派单、接单不改状态、
--       完工=已派单->已完成）且存量'处理中'归并到'已派单'后，再执行 Part C。
--   建议执行顺序：PR #45 合入且状态机改完后，一次性执行本文件全部三段；
--   或先执行 A/B，待 PR #45 就绪后单独执行 Part C 段。
--
-- 语义说明：
--   Escalation_Time DATE 可空：NULL=从未升级；NOT NULL=已升级（SP_Escalate_SLA
--     条件 UPDATE + SQL%ROWCOUNT 守门写入，防二次升级；多实例并发安全）。
--   Repair_Result VARCHAR2(200 CHAR)：DORM-28 契约枚举"已修复/需更换配件/
--     无法修复"，可空；实例 NLS_LENGTH_SEMANTICS=BYTE（C-018），须显式 CHAR
--     语义（同 013/014/017/018/019 先例）。库层不建 Repair_Result CHECK：
--     契约枚举可能随裁决调整，且 PR #45 Oracle 回归测试 Test 6 用自由文本
--     样本验证落库，建了会与其冲突（如需库层兜底，另行小迁移）。
--   SEQ_SLA_LOG：D_Repair_Log.Log_ID 专用序列，MAX(Log_ID)+1 起点对齐存量
--     （同 C-016/020 先例），NUMBER(10) 上限防护；SP_Complete_Repair 显式
--     NEXTVAL 写入，无需触发器（未来 EF 直插时再补，见文末注释块）。
--   CK_D_REPAIR_TICKET_STATUS 状态集 = 待处理/已派单/已完成/已撤销（PRD
--     §2.3.4：已撤销、已完成明文；流程 提交->待处理、固定派单->已派单、
--     接单处理状态不变、完工->已完成；剔除'处理中'——PRD 无此词）。
--     终态提示：PRD 注明"'已完成'还是'已结案'待后续抉择"，当前按 PRD 现文
--     用'已完成'，若后续裁决改'已结案'需小迁移替换本 CHECK 值。
--   CK_D_ADMIN_ROLE 角色枚举 = 楼长/维修员/超级管理员（PRD §2.3.4 执行者
--     维修员/楼长；无卫生检查员岗位——用户 2026-08-13 拍板由楼长执行卫生
--     检查；超级管理员纳入；辅导员不存 D_Admin，未来需存先扩展枚举）。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   Part A：单条 ALTER，选中执行（Ctrl+Enter），不带 "/"。
--   Part B：先执行 ALTER，再执行序列匿名块（整段选中执行）。
--   Part C：两条 ALTER 分别选中执行；执行前先跑"存量合规检查"（见下）。
--
-- 重复执行说明：
--   ALTER TABLE ADD COLUMN / ADD CONSTRAINT 非幂等（ORA-01430 / ORA-02264）；
--   CREATE SEQUENCE 幂等（ORA-00955 已存在，匿名块捕获跳过）。
--   已执行过 PR #45 sp_sla_dispatch.sql 内联补丁的环境：列/序列已存在，
--   仅需执行文末"兼容对齐"段（Repair_Result BYTE -> CHAR 语义）。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql，确认新增的第 19
--   部分输出：ESCALATION_TIME 列 1 行 + REPAIR_RESULT 列 1 行（CHAR_USED='C'、
--   CHAR_LENGTH=200）+ SEQ_SLA_LOG 序列 1 行 + 两个 CHECK 约束各 1 行
--   （Part C 未执行时约束段返回 0 行为预期）。

-- =====================================================================
-- Part A：D_Repair_Ticket 增加 Escalation_Time（SLA 首次升级时间）
-- =====================================================================
ALTER TABLE D_Repair_Ticket
    ADD (Escalation_Time DATE);

-- =====================================================================
-- Part B：D_Repair_Log 增加 Repair_Result + 专用主键序列 SEQ_SLA_LOG
-- =====================================================================
ALTER TABLE D_Repair_Log
    ADD (Repair_Result VARCHAR2(200 CHAR));

DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Log_ID), 0)
      INTO v_max_id
      FROM D_Repair_Log;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Repair_Log.Log_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_SLA_LOG';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_SLA_LOG START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;
/

-- =====================================================================
-- Part C：状态集与角色枚举 CHECK（⚠️ 依赖 PR #45 状态机改造，见文件头）
-- =====================================================================

-- 存量合规检查（启用前须各返回 0 行）：
--   SELECT DISTINCT Status FROM D_Repair_Ticket
--    WHERE Status NOT IN ('待处理', '已派单', '已完成', '已撤销');
--   SELECT DISTINCT Role_Level FROM D_Admin
--    WHERE Role_Level NOT IN ('楼长', '维修员', '超级管理员');
-- 若 D_Repair_Ticket 已有'处理中'数据，先归并到'已派单'再执行：
--   UPDATE D_Repair_Ticket SET Status = '已派单' WHERE Status = '处理中';
-- 若 PR #45 尚未改状态机（SP 仍写'处理中'），禁止执行本段。

ALTER TABLE D_Repair_Ticket
    ADD CONSTRAINT CK_D_REPAIR_TICKET_STATUS
        CHECK (Status IN ('待处理', '已派单', '已完成', '已撤销'));

ALTER TABLE D_Admin
    ADD CONSTRAINT CK_D_ADMIN_ROLE
        CHECK (Role_Level IN ('楼长', '维修员', '超级管理员'));

-- ============ 兼容对齐（仅"已执行过 sp_sla_dispatch.sql 内联补丁"的环境需要） ============
-- 内联补丁创建的 Repair_Result 是 VARCHAR2(200) BYTE 语义，与 Part B 的
-- VARCHAR2(200 CHAR) 不一致（BYTE 语义下 200 字节只够约 66 个中文字符，
-- 契约"维修结果最长 200 字"按字符计）；对齐后口径才一致。
-- 全新重建库（010->...->021）不需要执行本段。
-- ALTER TABLE D_Repair_Log MODIFY (Repair_Result VARCHAR2(200 CHAR));

-- ============ 可选：D_Repair_Log 主键触发器（未来 EF 直插时启用） ============
-- 当前唯一写入方为 SP_Complete_Repair（显式 SEQ_SLA_LOG.NEXTVAL），不需要；
-- 若后续出现 EF 直插（RepairLog.LogId 已配置 ValueGeneratedOnAdd，C-032
-- 同源地雷），启用下面两段（幂等写法，同 020 先例）。
-- DECLARE
--     v_exists NUMBER;
-- BEGIN
--     SELECT COUNT(*) INTO v_exists FROM USER_TRIGGERS
--      WHERE TRIGGER_NAME = 'TRG_D_REPAIR_LOG_ID_BI';
--     IF v_exists = 0 THEN
--         EXECUTE IMMEDIATE
--             'CREATE TRIGGER TRG_D_REPAIR_LOG_ID_BI ' ||
--             'BEFORE INSERT ON D_Repair_Log ' ||
--             'FOR EACH ROW ' ||
--             'WHEN (NEW.Log_ID IS NULL) ' ||
--             'BEGIN ' ||
--             '    SELECT SEQ_SLA_LOG.NEXTVAL INTO :NEW.Log_ID FROM dual; ' ||
--             'END;';
--     END IF;
-- END;
-- /
