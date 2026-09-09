namespace TemplateDormApi.Services;

/// <summary>
/// 保洁任务前缀常量（借道 D_Repair_Ticket 承载，用 Issue_Desc 前缀区分“保洁”与“报修”）。
/// 读侧统一用 IsCleaning 过滤，避免保洁混入普通报修流水。
/// </summary>
public static class CleaningTags
{
    /// <summary>学生申请的宿舍保洁前缀</summary>
    public const string DormPrefix = "[保洁-宿舍] ";

    /// <summary>每周楼栋整体保洁前缀</summary>
    public const string BuildingPrefix = "[保洁-楼栋] ";

    public const string SearchPrefix = "[保洁-";

    /// <summary>判断一条报修工单是否属于“保洁任务”</summary>
    public static bool IsCleaning(string? description)
        => !string.IsNullOrEmpty(description) && description.StartsWith(SearchPrefix, System.StringComparison.Ordinal);
}
