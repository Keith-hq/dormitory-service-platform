namespace TemplateDormApi.Models;

/// <summary>
/// 门岗登记实体（映射 D_Visitor_Registry，VST-01/02/03）。
/// 访客到访登记 → 扫码核验（关联 D_Visitor_Authorization.Authorization_Token）→ 离开记录。
/// </summary>
public class VisitorRegistry
{
    public long RegistryId { get; set; }

    /// <summary>核验时关联的授权二维码 token（D_Visitor_Authorization.Authorization_Token）</summary>
    public string? QrToken { get; set; }

    public string VisitorName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>被访学生学号（可空）</summary>
    public string? StudentId { get; set; }

    public DateTime EnterTime { get; set; }

    public DateTime? ExitTime { get; set; }

    /// <summary>待核验 / 已核验 / 已离开</summary>
    public string Status { get; set; } = "待核验";
}
