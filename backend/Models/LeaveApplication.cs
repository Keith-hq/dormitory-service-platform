namespace TemplateDormApi.Models;

/// <summary>
/// 离校报备实体（映射 D_Leave_Application）
/// 状态机：待批 → 已通过 / 已驳回 / 已撤回；修改与撤销仅在「待批」可用。
/// 主键由序列 SEQ_D_LEAVE_APPLICATION_ID + 触发器生成（迁移 023），EF 侧配置 ValueGeneratedOnAdd。
/// </summary>
public class LeaveApplication
{
    /// <summary>报备ID（APPLY_ID）</summary>
    public int ApplyId { get; set; }

    /// <summary>申请学生学号（STUDENT_ID，FK → D_Student）</summary>
    public string? StudentId { get; set; }

    /// <summary>离校日期（LEAVE_DATE）</summary>
    public DateTime LeaveDate { get; set; }

    /// <summary>返校日期（RETURN_DATE）</summary>
    public DateTime ReturnDate { get; set; }

    /// <summary>目的地（DESTINATION）</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>状态（STATUS）：待批 / 已通过 / 已驳回 / 已撤回</summary>
    public string Status { get; set; } = "待批";

    /// <summary>驳回原因（REASON，迁移 017 加列，C-023 裁决）</summary>
    public string? Reason { get; set; }
}
