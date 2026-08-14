-- 难点④ 三审并发测试：两个会话同时执行本脚本（耗材出库竞态，同 Key）
-- 预期：库存只扣一次；后到者 INSERT 撞唯一索引回滚后返回 rc=0。
SET SERVEROUTPUT ON SIZE UNLIMITED
DECLARE
    v_rc NUMBER;
BEGIN
    FOR i IN 1..1000000 LOOP NULL; END LOOP;

    SP_Consume_Material(1, 1, 3, 'RACE-CONSUME-001', v_rc);
    DBMS_OUTPUT.PUT_LINE('CONSUME rc=' || v_rc);
    COMMIT;
END;
/
EXIT
