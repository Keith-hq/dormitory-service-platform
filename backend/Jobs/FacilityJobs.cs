using Quartz;
using TemplateDormApi.Services;

namespace TemplateDormApi.Jobs;

[DisallowConcurrentExecution]
public class ExpireBookingJob : IJob
{
    private readonly IFacilityBookingService _service;
    public ExpireBookingJob(IFacilityBookingService service) => _service = service;
    public async Task Execute(IJobExecutionContext context) => await _service.ExpireBookings();
}

[DisallowConcurrentExecution]
public class AutoCompleteJob : IJob
{
    private readonly IFacilityBookingService _service;
    public AutoCompleteJob(IFacilityBookingService service) => _service = service;
    public async Task Execute(IJobExecutionContext context) => await _service.AutoComplete();
}
