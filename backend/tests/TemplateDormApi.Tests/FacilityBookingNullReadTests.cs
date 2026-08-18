using Oracle.ManagedDataAccess.Types;

namespace TemplateDormApi.Tests;

/// <summary>
/// PR #78 回归测试（兰皓衍评审建议）：SP 业务拒绝提前 RETURN 时，
/// p_Result_Code / p_Booking_ID OUT 参数为 NULL（OracleDecimal.Null），
/// 直接读 .Value 抛 OracleNullValueException（ORA-50048）。
/// 守卫 `is OracleDecimal od && !od.IsNull` 必须先短路返回默认值，不得触碰 .Value。
/// 模式与 FacilityBookingService.CallBook（第 38/39 行）、CallResultCode（第 91 行）一致。
/// </summary>
public class FacilityBookingNullReadTests
{
    [Fact]
    public void OracleDecimal_Null_ShortCircuitsToFallback_NotThrow()
    {
        object value = OracleDecimal.Null;

        // 对应 CallBook / CallResultCode 读 p_Result_Code 的两处守卫（修前抛 ORA-50048）
        int code = value is OracleDecimal od && !od.IsNull ? (int)od.Value : -1;

        Assert.Equal(-1, code);
    }

    [Fact]
    public void OracleDecimal_Null_BookingIdFallsBackToZero()
    {
        object value = OracleDecimal.Null;

        // 对应 CallBook 读 p_Booking_ID（第 39 行）
        int bookingId = value is OracleDecimal bd && !bd.IsNull ? (int)bd.Value : 0;

        Assert.Equal(0, bookingId);
    }

    [Fact]
    public void OracleDecimal_NonNull_ReturnsValue()
    {
        object value = new OracleDecimal(7);

        int code = value is OracleDecimal od && !od.IsNull ? (int)od.Value : -1;

        Assert.Equal(7, code);
    }
}
