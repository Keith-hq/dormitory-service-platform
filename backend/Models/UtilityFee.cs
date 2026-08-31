namespace TemplateDormApi.Models;

public sealed class UtilityFee
{
    public long FeeId { get; set; }
    public int RoomId { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public decimal? WaterFee { get; set; }
    public decimal? PowerFee { get; set; }
    // 041 起账单头不再有 Is_Paid：缴费状态唯一事实来源为 D_Fee_Detail.Is_Paid。
    public string PublishStatus { get; set; } = string.Empty;
    public Room? Room { get; set; }
}
