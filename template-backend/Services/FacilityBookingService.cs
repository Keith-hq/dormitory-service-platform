using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using TemplateDormApi.Data;

namespace TemplateDormApi.Services;

/// <summary>
/// 公共设施预约服务：封装难点③的五个存储过程
/// 使用原生 OracleCommand 处理 OUT 参数
/// </summary>
public class FacilityBookingService : IFacilityBookingService
{
    private readonly AppDbContext _context;

    public FacilityBookingService(AppDbContext context)
    {
        _context = context;
    }

    private async Task<int> CallWithResultCode(string procedure, params OracleParameter[] parameters)
    {
        using var conn = (OracleConnection)_context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = procedure;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddRange(parameters);

        // 自动加 OUT 参数
        var rc = new OracleParameter("p_Result_Code", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add(rc);

        await cmd.ExecuteNonQueryAsync();
        return rc.Value is Oracle.ManagedDataAccess.Types.OracleDecimal od
            ? (int)od.Value
            : Convert.ToInt32(rc.Value ?? -1);
    }

    public async Task<int> BookFacility(int facilityId, string studentId)
    {
        return await CallWithResultCode("SP_Book_Facility",
            new OracleParameter("p_Facility_ID", facilityId),
            new OracleParameter("p_Student_ID", studentId));
    }

    public async Task<int> StartUse(int bookingId, string studentId)
    {
        return await CallWithResultCode("SP_Start_Use",
            new OracleParameter("p_Booking_ID", bookingId),
            new OracleParameter("p_Student_ID", studentId));
    }

    public async Task<int> EndUse(int bookingId, string studentId)
    {
        return await CallWithResultCode("SP_End_Use",
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
}
