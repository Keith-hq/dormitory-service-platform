namespace TemplateDormApi.DTO;

public class CreateVoteRequest
{
    public long RoomId { get; set; }
    public string Title { get; set; }
}

public class SubmitVoteRequest
{
    public string Option { get; set; } // 赞成,反对,弃权
}