namespace TemplateDormApi.Models;

/// <summary>
/// 信用分申诉实体（映射 D_Credit_Appeal，APPEAL-01/02/03）。
/// 学生对某条扣分明细（D_Credit_Log.Log_ID）发起申诉，楼长/超管复核。
/// </summary>
public class CreditAppeal
{
    public int AppealId { get; set; }

    /// <summary>被申诉的扣分明细 ID（D_Credit_Log.Log_ID）。
    /// 学生归属经 Credit_Log.Student_ID 联查（041 删除冗余 Student_ID 列）。</summary>
    public int CreditLogId { get; set; }

    /// <summary>申诉原因</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>待复核 / 已通过 / 已驳回</summary>
    public string Status { get; set; } = "待复核";

    /// <summary>复核结论说明</summary>
    public string? ResultDesc { get; set; }

    /// <summary>复核人（D_Admin.Admin_ID）</summary>
    public string? ReviewedBy { get; set; }

    public DateTime? ReviewTime { get; set; }

    public DateTime CreateTime { get; set; }
}
