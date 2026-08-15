-- 难点④ 三审并发测试准备：清理历史竞态数据，固定基线库存
SET SERVEROUTPUT ON SIZE UNLIMITED
BEGIN
    DELETE FROM D_Item_Loan WHERE Idempotency_Key = 'RACE-BORROW-001';
    DELETE FROM D_Repair_Material_Usage WHERE Idempotency_Key = 'RACE-CONSUME-001';
    -- 基线固定：物品库存回满（满足 CK_D_SHARED_ITEM_QTY，不硬编码数量），耗材库存=10
    UPDATE D_Shared_Item SET Available_Qty = Total_Qty WHERE Item_ID = 1;
    UPDATE D_Repair_Material SET Stock_Qty = 10 WHERE Material_ID = 1;
    -- 竞态会话需要信用账户
    BEGIN INSERT INTO D_Credit_Account (Student_ID, Current_Score)
        VALUES ('S001', 100); EXCEPTION WHEN DUP_VAL_ON_INDEX THEN NULL; END;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('SETUP OK: item1 avail=Total_Qty, mat1 stock=10');
END;
/
EXIT
