namespace TemplateDormApi.DTO;

public class CreateVoteRequest
{
    public int RoomId { get; set; }
    public string InitiatorStudentId { get; set; }
    public string Topic { get; set; }
    public int DurationDays { get; set; } // 投票持续天数
    public int EligibleCount { get; set; }
}

public class SubmitVoteRequest
{
    public string Choice { get; set; }
}