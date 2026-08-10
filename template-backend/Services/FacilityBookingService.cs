using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using TemplateDormApi.Data;

namespace TemplateDormApi.Services;

public class FacilityBookingService : IFacilityBookingService
{
    private readonly AppDbContext _context;

    public FacilityBookingService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>返回 (resultCode, bookingId)。bookingId 仅在 resultCode=0 时有效。</summary>
    private async Task<(int, int)> CallBook(string procedure, params OracleParameter[] parameters)
    {
        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = procedure;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddRange(parameters);

        var rc = new OracleParameter("p_Result_Code", OracleDbType.Int32, ParameterDirection.Output);
        var bid = new OracleParameter("p_Booking_ID", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add(rc);
        cmd.Parameters.Add(bid);

        await cmd.ExecuteNonQueryAsync();

        if (!wasOpen) conn.Close();

        int code = rc.Value is Oracle.ManagedDataAccess.Types.OracleDecimal od ? (int)od.Value : -1;
        int bookingId = bid.Value is Oracle.ManagedDataAccess.Types.OracleDecimal bd ? (int)bd.Value : 0;
        return (code, bookingId);
    }

    public async Task<(int resultCode, int bookingId)> BookFacility(int facilityId, string studentId)
    {
        return await CallBook("SP_Book_Facility",
            new OracleParameter("p_Facility_ID", facilityId),
            new OracleParameter("p_Student_ID", studentId));
    }

    public async Task<int> StartUse(int bookingId, string studentId)
    {
        return await CallResultCode("SP_Start_Use",
            new OracleParameter("p_Booking_ID", bookingId),
            new OracleParameter("p_Student_ID", studentId));
    }

    public async Task<int> FinishUse(int bookingId, string studentId)
    {
        return await CallResultCode("SP_Finish_Use",
            new OracleParameter("p_Booking_ID", bookingId),
            new OracleParameter("p_Student_ID", studentId));
    }

    public async Task ExpireBookings()
    {
        await _context.Database.ExecuteSqlRawAsync("BEGIN SP_Expire_Booking; END;");
    }

    public async Task AutoComplete()
    {
        await _context.Database.ExecuteSqlRawAsync("BEGIN SP_Auto_Complete; END;");
    }

    private async Task<int> CallResultCode(string procedure, params OracleParameter[] parameters)
    {
        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = procedure;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddRange(parameters);
        var rc = new OracleParameter("p_Result_Code", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add(rc);

        await cmd.ExecuteNonQueryAsync();

        if (!wasOpen) conn.Close();

        return rc.Value is Oracle.ManagedDataAccess.Types.OracleDecimal od ? (int)od.Value : -1;
    }
}
