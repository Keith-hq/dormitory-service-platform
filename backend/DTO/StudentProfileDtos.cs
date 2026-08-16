using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

public sealed class UpdateStudentProfileRequest
{
    [RegularExpression(@"^\d{11}$", ErrorMessage = "手机号必须为 11 位数字")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string? Email { get; set; }
}

public sealed class StudentProfileDto
{
    public string StudentId { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public sealed class AccommodationDto
{
    public long AllocationId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public int BedNo { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
}
