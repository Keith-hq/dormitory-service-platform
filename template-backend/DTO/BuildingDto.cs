using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>
/// 楼栋新增 DTO
/// </summary>
public class BuildingCreateDto
{
    [Required(ErrorMessage = "楼栋名称不能为空")]
    public string BuildingName { get; set; } = string.Empty;

    [Required(ErrorMessage = "楼栋类型不能为空")]
    public string BuildingType { get; set; } = string.Empty;

    [Range(1, 50, ErrorMessage = "楼层数必须在 1~50 之间")]
    public int FloorCount { get; set; }
}

/// <summary>
/// 楼栋编辑 DTO
/// </summary>
public class BuildingUpdateDto
{
    [RegularExpression(@".*\S.*", ErrorMessage = "楼栋名称不能为空")]
    public string? BuildingName { get; set; }

    [RegularExpression(@".*\S.*", ErrorMessage = "楼栋类型不能为空")]
    public string? BuildingType { get; set; }

    [Range(1, 50, ErrorMessage = "楼层数必须在 1~50 之间")]
    public int? FloorCount { get; set; }
}

/// <summary>
/// 分页查询 DTO
/// </summary>
public class BuildingQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? BuildingType { get; set; }
}

/// <summary>
/// 分页结果 DTO
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
