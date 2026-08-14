# 退宿结算 SP 调用说明（给刘润东）

- 作者：李昂
- 日期：2026-08-13
- 依据：难点⑥ 分工对齐方案（组长 + 刘润东已确认）

## 一、分工回顾

退宿结算金额计算归李昂（calc-checkout），退宿流程与床位/房间状态写入归刘润东。刘润东的 settle 在流程中直接调用下面的存储过程，不需要通过 HTTP 接口。

## 二、存储过程签名

```
PROCEDURE SP_Calc_Checkout_Fee(
    p_Student_ID    IN VARCHAR2,   -- 退宿学生学号
    p_Allocation_ID IN NUMBER      -- 住宿分配记录 ID（D_Bed_Allocation.Allocation_ID）
)
```

无返回值、无 OUT 参数。调用示例（Oracle）：

```sql
BEGIN
    SP_Calc_Checkout_Fee('S001', 1);
END;
```

## 三、调用契约（必须遵守）

1. 调用顺序：settle 必须先写入 D_Bed_Allocation.CheckOut_Date，再调用本过程。过程按 CheckOut_Date 所在月份结算；若 CheckOut_Date 仍为 NULL，会回退用 SYSDATE（防御分支，口径有偏差风险，正常流程不要触发）。
2. 事务约定：过程内部不 COMMIT。请在 settle 的退宿事务内调用，由你的外层事务统一提交（床位、房间、费用三处要么全成、要么全不成）。
3. 结算范围：只结算该房间、该月份、Publish_Status = '已发布' 的费用；未发布费用不结算，等月度流程覆盖。
4. 幂等：同一退宿流程重复调用安全——唯一索引 UK_D_FEE_DETAIL(Fee_ID, Student_ID) 兜底，冲突静默跳过，不会产生重复明细。
5. 两入口防重：若该笔费用已有 '月度' 明细（月度 SP 已结算），退宿过程不会重复生成；反向同理，月度 SP 遇到已有 '退宿' 明细也会跳过。v1.3 起唯一索引收紧为 (Fee_ID, Student_ID)，'月度' 与 '退宿' 在库层互斥——即使退宿结算与每月 1 日月度任务并发执行（COUNT 预检查与 INSERT 之间的竞态窗口），也只会产生一条明细，由唯一索引兜底（IT-C10-004）。
6. 异常：p_Allocation_ID 与 p_Student_ID 匹配不到分配记录时抛 NO_DATA_FOUND，settle 侧自行决定是否回滚流程。
7. 业务边界（一审 R5，答辩前请团队确认口径）：若月度流程先跑（每月 1 日）而 settle 尚未写入 CheckOut_Date（流程违约或结算延迟），学生当月会先按全月生成 '月度' 明细；之后 settle 补写 CheckOut_Date 再调退宿过程时，因 '月度' 已存在而跳过，不会补差、也无对账/补算路径。正常流程（先写 CheckOut_Date 再结算）不受影响。

## 四、改动记录（v1.2）

1. 去掉过程内部 COMMIT（事务由调用方统一提交）。
2. 只结算已发布费用（与月度 SP 口径一致）。
3. 新增两入口防重：'月度' 已存在则退宿跳过；月度 SP 同步增加 '退宿' 已存在则月度跳过。
4. CheckOut_Date 为 NULL 时回退 SYSDATE 的分支保留为防御逻辑。

## 五、改动记录（v1.3，一审修订 2026-08-14）

1. 调用方式固化：settle 直调 SP_Calc_Checkout_Fee（在退宿事务内调用，事务由你的外层统一提交）；calc-checkout HTTP 接口仅作人工补算入口，其 Controller 层外层开启并提交事务，C# 服务方法本身无事务——如果你后续改调服务方法而不是直调 SP，先同步本文档口径。
2. 唯一索引收紧：UK_D_FEE_DETAIL 由 (Fee_ID, Student_ID, Bill_Type) 改为 (Fee_ID, Student_ID)（迁移 database/ddl/extensions/022_fee_detail_dedup_uk.sql），'月度' 与 '退宿' 库层互斥，并发竞态由唯一索引兜底（见契约第 5 条）。
3. 业务边界补充（见契约第 7 条，答辩前团队确认）。

以上改动已通过 Oracle 实测（16 条断言：口径对齐、正常结算、幂等重放、双向防重、无内部提交、SYSDATE 兜底、NO_DATA_FOUND、唯一索引互斥），测试脚本见 database/sp/test_sp_checkout_fee.sql（月份无关，失败不中止清理）。
