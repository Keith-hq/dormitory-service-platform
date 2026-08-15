using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 水电分摊服务：封装难点①的存储过程调用和分摊查询
/// </summary>
public class FeeSharingService : IFeeSharingService
{
    private readonly AppDbContext _context;

    public FeeSharingService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 每月1日触发：批量生成当月全部分摊
    /// 调用存储过程 SP_Calc_Monthly_Fee('2026-08')
    /// </summary>
    public async Task CalcMonthlyFee(string yearMonth)
    {
        // Oracle 存储过程调用：
        // BEGIN 过程名(参数); END;
        // {0} 是参数占位符，EF Core 会自动处理 SQL 注入防护
        await _context.Database.ExecuteSqlRawAsync(
            "BEGIN SP_Calc_Monthly_Fee({0}); END;",
            yearMonth);
    }

    /// <summary>
    /// 学生退宿时触发：结算退宿学生的当月分摊
    /// 调用存储过程 SP_Calc_Checkout_Fee('S001', 1)
    /// 一审 R1（难点⑥）：本方法无事务——SP 内部不 COMMIT（v1.2 契约），
    /// 事务由调用方统一管理：
    ///   - 接口/人工补算入口（FeeSharingController）在本方法外层开启并提交事务；
    ///   - 刘润东的退宿事务可直接在同一外层事务内调用本方法（或直调 SP），
    ///     不会二次 BeginTransaction，由他的事务统一提交。
    /// </summary>
    public async Task CalcCheckoutFee(string studentId, int allocationId)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "BEGIN SP_Calc_Checkout_Fee({0}, {1}); END;",
            studentId, allocationId);
    }

    /// <summary>
    /// 查询某学生某月的个人分摊明细
    /// 通过 Create_Time 所在月份过滤
    /// </summary>
    public async Task<List<FeeDetail>> GetFeeDetail(string studentId, string yearMonth)
    {
        // 原生 SQL 查询——绕过 EF Core 的表映射问题
        return await _context.FeeDetails
            .FromSqlRaw(
                @"SELECT * FROM D_Fee_Detail
                  WHERE Student_ID = {0}
                    AND Create_Time >= TO_DATE({1} || '-01', 'YYYY-MM-DD')
                    AND Create_Time < ADD_MONTHS(TO_DATE({1} || '-01', 'YYYY-MM-DD'), 1)
                  ORDER BY Create_Time DESC",
                studentId, yearMonth)
            .ToListAsync();
    }
}
