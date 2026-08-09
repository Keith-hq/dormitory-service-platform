using Quartz;
using TemplateDormApi.Services;

namespace TemplateDormApi.Jobs;

/// <summary>
/// 过期巡检：15分钟未开始→失效，每15秒执行
/// </summary>
public class ExpireBookingJob : IJob
{
    private readonly IFacilityBookingService _service;

    public ExpireBookingJob(IFacilityBookingService service)
    {
        _service = service;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _service.ExpireBookings();
    }
}

/// <summary>
/// 超时完成：60分钟→已完成，每15秒执行
/// </summary>
public class AutoCompleteJob : IJob
{
    private readonly IFacilityBookingService _service;

    public AutoCompleteJob(IFacilityBookingService service)
    {
        _service = service;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _service.AutoComplete();
    }
}
