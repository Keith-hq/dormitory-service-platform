using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TemplateDormApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VoteController : ControllerBase
    {
        [HttpPost("test")]
        public IActionResult Test()
        {
            return Ok("投票模块骨架已跑通！");
        }
    }
}
