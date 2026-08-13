namespace TemplateDormApi.Models;

/// <summary>
/// 审计事件（映射 D_Audit_Event）。
/// ⚠️ 归属说明：审计域归徐亦尘（FP5-4），其对外接口尚未就绪；
/// 退宿取消需写审计（IT-C2-005 ③），当前按追加写入直接落表，8/14 联调核对后
/// 应改为调用审计模块接口/公共服务。主键无序列，MAX+1 生成（同本模块其他新表）。
/// </summary>
public class AuditEvent
{
    public int AuditId { get; set; }

    public int? ActorAccountId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? TargetType { get; set; }

    public string? TargetId { get; set; }

    public DateTime EventTime { get; set; }
}
