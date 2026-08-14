-- 难点④ 三审并发测试：两个会话同时执行本脚本（借用竞态，同 Key）
-- 预期：无论哪个会话先到，库存只扣一次；后到者 INSERT 撞唯一索引
--       回滚库存后返回胜者 Loan_ID（rc=0）。
SET SERVEROUTPUT ON SIZE UNLIMITED
DECLARE
    v_rc  NUMBER;
    v_lid NUMBER;
BEGIN
    -- CPU 预热，使两个并发会话尽量同时到达存储过程
    FOR i IN 1..1000000 LOOP NULL; END LOOP;

    SP_Borrow_Item(1, 'S001', 'RACE-BORROW-001', v_rc, v_lid);
    DBMS_OUTPUT.PUT_LINE('BORROW rc=' || v_rc || ' loanId=' || v_lid);
    COMMIT;
END;
/
EXIT
