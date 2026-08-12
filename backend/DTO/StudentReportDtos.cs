using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

public sealed class MonthlyReportQueryDto
{
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "yearMonth 格式必须为 yyyy-MM")]
    public string? YearMonth { get; set; }
}

public sealed class AnnualReportQueryDto
{
    [Range(2000, 2100, ErrorMessage = "年份必须在 2000 到 2100 之间")]
    public int? Year { get; set; }
}

public sealed class MonthlyFeeReportDto
{
    public string YearMonth { get; set; } = string.Empty;
    public decimal UtilityTotal { get; set; }
}

public sealed class FacilityUsageReportDto
{
    public string YearMonth { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public sealed class AnnualReportDto
{
    public int Year { get; set; }
    public decimal UtilityTotal { get; set; }
    public decimal HygieneAvg { get; set; }
    public int AccessCount { get; set; }
    public string? Overview { get; set; }
}
