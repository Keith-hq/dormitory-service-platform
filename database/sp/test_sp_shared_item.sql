-- 难点④ 回归测试脚本
SET SERVEROUTPUT ON

-- ===== 1. 验证新序列 SEQ_CREDIT_LOG 存在 =====
BEGIN
    DBMS_OUTPUT.PUT_LINE('--- 1. SEQ_CREDIT_LOG 验证 ---');
    DBMS_OUTPUT.PUT_LINE('SEQ_CREDIT_LOG.NEXTVAL = ' || SEQ_CREDIT_LOG.NEXTVAL);
    DBMS_OUTPUT.PUT_LINE('SEQ_ITEM_LOAN.NEXTVAL = ' || SEQ_ITEM_LOAN.NEXTVAL);
    DBMS_OUTPUT.PUT_LINE('SEQ_MATERIAL_USAGE.NEXTVAL = ' || SEQ_MATERIAL_USAGE.NEXTVAL);
END;
/

-- ===== 2. 查看当前数据基线 =====
DECLARE
    v_avail NUMBER;
    v_stock NUMBER;
    v_score NUMBER;
BEGIN
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    SELECT Stock_Qty INTO v_stock FROM D_Repair_Material WHERE Material_ID = 1;
    BEGIN
        SELECT Current_Score INTO v_score FROM D_Credit_Account WHERE Student_ID = 'S001';
    EXCEPTION WHEN NO_DATA_FOUND THEN v_score := -1;
    END;
    DBMS_OUTPUT.PUT_LINE('--- 2. 数据基线 ---');
    DBMS_OUTPUT.PUT_LINE('Item_ID=1 Available_Qty = ' || v_avail);
    DBMS_OUTPUT.PUT_LINE('Material_ID=1 Stock_Qty = ' || v_stock);
    DBMS_OUTPUT.PUT_LINE('S001 Credit_Score = ' || v_score);
END;
/

-- ===== 3. SP_Borrow_Item 测试 =====
DECLARE
    rc NUMBER;
    lid NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('--- 3. SP_Borrow_Item ---');
    SP_Borrow_Item(1, 'S001', rc, lid);
    DBMS_OUTPUT.PUT_LINE('Borrow rc=' || rc || ' loanId=' || lid);
END;
/

-- ===== 4. 验证库存扣减 =====
DECLARE
    v_avail NUMBER;
BEGIN
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    DBMS_OUTPUT.PUT_LINE('--- 4. 库存验证 ---');
    DBMS_OUTPUT.PUT_LINE('After borrow: Available_Qty = ' || v_avail);
END;
/

-- ===== 5. SP_Return_Item 测试 =====
DECLARE
    rc NUMBER;
    v_lid NUMBER;
BEGIN
    SELECT MAX(Loan_ID) INTO v_lid FROM D_Item_Loan
    WHERE Student_ID = 'S001' AND Return_Time IS NULL;
    DBMS_OUTPUT.PUT_LINE('--- 5. SP_Return_Item (Loan_ID=' || v_lid || ') ---');
    SP_Return_Item(v_lid, rc);
    DBMS_OUTPUT.PUT_LINE('Return rc=' || rc);
END;
/

-- ===== 6. 验证库存回加 + 归还时间 =====
DECLARE
    v_avail NUMBER;
    v_rt DATE;
BEGIN
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    DBMS_OUTPUT.PUT_LINE('--- 6. 归还后验证 ---');
    DBMS_OUTPUT.PUT_LINE('Available_Qty (expect original) = ' || v_avail);
    BEGIN
        SELECT Return_Time INTO v_rt FROM D_Item_Loan
        WHERE Student_ID = 'S001' AND Return_Time IS NOT NULL AND ROWNUM = 1;
        DBMS_OUTPUT.PUT_LINE('Return_Time = ' || TO_CHAR(v_rt, 'YYYY-MM-DD HH24:MI:SS'));
    EXCEPTION WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('Return_Time NOT found! FAIL');
    END;
END;
/

-- ===== 7. 重复归还测试（幂等） =====
DECLARE
    rc NUMBER;
    v_lid NUMBER;
BEGIN
    SELECT MAX(Loan_ID) INTO v_lid FROM D_Item_Loan WHERE Student_ID = 'S001';
    DBMS_OUTPUT.PUT_LINE('--- 7. 重复归还 (Loan_ID=' || v_lid || ') ---');
    SP_Return_Item(v_lid, rc);
    DBMS_OUTPUT.PUT_LINE('Duplicate return rc=' || rc || ' (expect 1)');
END;
/

-- ===== 8. SP_Consume_Material 测试 =====
DECLARE
    rc NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('--- 8. SP_Consume_Material ---');
    SP_Consume_Material(1, 1, 3, rc);
    DBMS_OUTPUT.PUT_LINE('Consume 3 units rc=' || rc || ' (expect 0)');
END;
/

-- ===== 9. 验证耗材库存 =====
DECLARE
    v_stock NUMBER;
BEGIN
    SELECT Stock_Qty INTO v_stock FROM D_Repair_Material WHERE Material_ID = 1;
    DBMS_OUTPUT.PUT_LINE('--- 9. 耗材库存 ---');
    DBMS_OUTPUT.PUT_LINE('After consume 3: Stock_Qty = ' || v_stock);
END;
/

-- ===== 10. 耗材库存不足 =====
DECLARE
    rc NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('--- 10. 库存不足测试 ---');
    SP_Consume_Material(1, 1, 99999, rc);
    DBMS_OUTPUT.PUT_LINE('Consume 99999 rc=' || rc || ' (expect 2)');
END;
/

-- ===== 11. SP_Check_Overdue 运行 =====
BEGIN
    DBMS_OUTPUT.PUT_LINE('--- 11. SP_Check_Overdue ---');
    SP_Check_Overdue;
    DBMS_OUTPUT.PUT_LINE('SP_Check_Overdue executed');
END;
/

-- ===== 12. 信用分日志验证 =====
DECLARE
    v_cnt NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_cnt FROM D_Credit_Log
    WHERE Event_Key LIKE 'OVERDUE%' OR Event_Key LIKE 'OVERDUE-SCAN%';
    DBMS_OUTPUT.PUT_LINE('--- 12. 信用日志 ---');
    DBMS_OUTPUT.PUT_LINE('Credit log entries (OVERDUE*): ' || v_cnt);
END;
/
