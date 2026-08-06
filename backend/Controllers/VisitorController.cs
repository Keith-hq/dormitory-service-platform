using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TemplateDormApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VisitorController : ControllerBase
    {
        [HttpPost("test")]
        public IActionResult Test()
        {
            return Ok("访客二维码模块骨架已跑通！");
        }
    }
}
