-- 难点④ 三审并发测试校验：两条竞态只产生一行记录、库存只扣一次
SET SERVEROUTPUT ON SIZE UNLIMITED
DECLARE
    v_cnt   NUMBER;
    v_avail NUMBER;
    v_stock NUMBER;
    v_total NUMBER;
    v_ok    NUMBER := 1;

    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN
            DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE
            v_ok := 0;
            DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT COUNT(*) INTO v_cnt FROM D_Item_Loan WHERE Idempotency_Key = 'RACE-BORROW-001';
    Assert(v_cnt = 1, '借用竞态只产生 1 条借出记录 (got ' || v_cnt || ')');

    SELECT Total_Qty INTO v_total FROM D_Shared_Item WHERE Item_ID = 1;
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    Assert(v_avail = v_total - 1,
        '物品库存只扣一次 (Total_Qty-1=' || (v_total - 1) || ', got ' || v_avail || ')');

    SELECT COUNT(*) INTO v_cnt FROM D_Repair_Material_Usage WHERE Idempotency_Key = 'RACE-CONSUME-001';
    Assert(v_cnt = 1, '耗材竞态只产生 1 条消耗记录 (got ' || v_cnt || ')');

    SELECT Stock_Qty INTO v_stock FROM D_Repair_Material WHERE Material_ID = 1;
    Assert(v_stock = 7, '耗材库存只扣一次 (10-3=7, got ' || v_stock || ')');

    IF v_ok = 1 THEN
        DBMS_OUTPUT.PUT_LINE('========== 并发校验全部通过 ==========');
    ELSE
        RAISE_APPLICATION_ERROR(-20098, 'FAIL: 并发校验未通过');
    END IF;
END;
/
EXIT
