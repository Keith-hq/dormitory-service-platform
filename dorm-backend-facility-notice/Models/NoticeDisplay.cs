using System.ComponentModel.DataAnnotations;

namespace DormBackendFacilityNotice.Models;

/// <summary>
/// 公告置顶实体 —— 对应 D_Notice_Display（与 D_Notice 1:1）
/// 置顶状态独立建表，避免与公告本体耦合
/// </summary>
public class NoticeDisplay
{
    [Key]
    public int NoticeId { get; set; }

    /// <summary>是否置顶（是 / 否）</summary>
    public string IsPinned { get; set; } = "否";

    /// <summary>置顶时间（可空）</summary>
    public DateTime? PinTime { get; set; }

    /// <summary>所属公告（导航）</summary>
    public Notice? Notice { get; set; }
}
