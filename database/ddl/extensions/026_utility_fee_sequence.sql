-- 扩展表迁移 026：D_Utility_Fee 主键序列与触发器。
-- 在已有环境上执行于 ddl/extensions/025（#55 待合入）之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> ... -> extensions/025
--   -> extensions/026。
-- 编号说明：023 已分配给 #49（D_Checkout 序列+房间唯一索引）、024 分配给 #52、
--   025 分配给 #55（工作台 C-040 裁定），本迁移原为 023，评审后重编号为 026。
-- 本脚本不得修改或重建任何基线表。
--
-- 为什么补 D_Utility_Fee 的序列？
--   基线 DDL 中 D_Utility_Fee.Fee_ID 为裸 NUMBER(10) 主键，无任何生成机制
--   （全库核实：无序列、无触发器、EF 模型未配置 ValueGeneratedOnAdd）。
--   缴费/账单端点（DORM-19 录账单）落地后，EF 首次向该表插入时会以 NULL
--   主键写入，报 ORA-01400。与 020 迁移同理：为并发安全统一走序列
--   （NEXTVAL 原子、无 MAX+1 竞态），WHEN (NEW.Fee_ID IS NULL) 对既有
--   显式赋值的写方保持兼容。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   整段复制后执行（匿名块，幂等写法：已存在的 sequence / trigger 被跳过，
--   重复执行无害）。

-- =====================================================================
-- 主键序列与触发器：D_Utility_Fee
-- 幂等写法同 020 迁移：已存在的 sequence / trigger 会被跳过，可重复执行；
-- 含 NUMBER(10) 上限防护（同 014/020 迁移先例）。
-- =====================================================================

DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Fee_ID), 0)
      INTO v_max_id
      FROM D_Utility_Fee;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Utility_Fee.Fee_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_UTILITY_FEE_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_UTILITY_FEE_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_UTILITY_FEE_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_UTILITY_FEE_ID_BI ' ||
            'BEFORE INSERT ON D_Utility_Fee ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Fee_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_UTILITY_FEE_ID.NEXTVAL ' ||
            '      INTO :NEW.Fee_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/
