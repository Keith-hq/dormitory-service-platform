namespace TemplateDormApi.Services;

public interface IAuditService
{
    Task LogEventAsync(
        string eventType,
        string? targetType = null,
        string? targetId = null,
        int? actorAccountId = null,
        DateTime? eventTime = null);
}