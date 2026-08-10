namespace TemplateDormApi.DTO
{
    public class VisitorApplyRequest
    {
        public string StudentId { get; set; }
        public long RoomId { get; set; }
        public string VisitorName { get; set; }
        public string? VisitReason { get; set; }
        public int DurationHours { get; set; }
    }
}