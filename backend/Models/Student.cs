namespace TemplateDormApi.Models;

public sealed class Student
{
    public string StudentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public long? MajorId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
