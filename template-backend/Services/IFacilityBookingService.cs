namespace TemplateDormApi.Services;

public interface IFacilityBookingService
{
    Task<(int resultCode, int bookingId)> BookFacility(int facilityId, string studentId);
    Task<int> StartUse(int bookingId, string studentId);
    Task<int> FinishUse(int bookingId, string studentId);
    Task ExpireBookings();
    Task AutoComplete();
}
