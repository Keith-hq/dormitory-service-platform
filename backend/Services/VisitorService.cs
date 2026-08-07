using System;
using TemplateDormApi.Models;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Services
{
    public class VisitorService
    {
        public VisitorAuthorization CreateRecord(VisitorApplyRequest req)
        {
            var now = DateTime.Now;
            return new VisitorAuthorization
            {
                // 生成一个 NUMBER(10) 范围内的 ID
                AuthorizationId = long.Parse(now.ToString("MMddHHmmss")),
                StudentId = req.StudentId,
                RoomId = req.RoomId,
                VisitorName = req.VisitorName,
                VisitReason = req.VisitReason,
                AuthorizationToken = "QR_" + Guid.NewGuid().ToString("N").ToUpper(),
                ExpiresTime = now.AddHours(req.DurationHours),
                Status = "有效",
                CreateTime = now
            };
        }
    }
}