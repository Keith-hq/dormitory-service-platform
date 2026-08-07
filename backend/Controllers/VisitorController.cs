using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.Models;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers
{
    [ApiController]
    [Route("api/visitor-authorizations")]
    public class VisitorController : ControllerBase
    {
        [HttpPost]
        public ApiResponse<VisitorAuthorization> Post([FromBody] VisitorApplyRequest request)
        {
            var service = new VisitorService();
            var result = service.CreateRecord(request);

            return ApiResponse.Ok(result, "访客授权申请成功");
        }
    }
}