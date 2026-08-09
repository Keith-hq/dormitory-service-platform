using TemplateDormApi.Models;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

public class VisitorService
{
    //接口1:申请授权
    public VisitorAuthorization Apply(VisitorApplyRequest req)
    {
        var now = DateTime.Now;
        return new VisitorAuthorization
        {
            AuthorizationId = 0,
            StudentId = req.StudentId,
            RoomId = req.RoomId,
            VisitorName = req.VisitorName,
            VisitReason = req.VisitReason,
            AuthorizationToken = "VSR_" + Guid.NewGuid().ToString("N").ToUpper().Substring(0, 12),
            ExpiresTime = now.AddHours(req.DurationHours),
            Status = "有效"
        };
    }

    //接口2:查询列表 (M1阶段Mock数据)
    public List<VisitorAuthorization> GetList(string studentId)
    {
        return new List<VisitorAuthorization> {
            new VisitorAuthorization { AuthorizationId = 1, StudentId = studentId, VisitorName = "张三", Status = "有效" },
            new VisitorAuthorization { AuthorizationId = 2, StudentId = studentId, VisitorName = "李四", Status = "已过期" }
        };
    }
}