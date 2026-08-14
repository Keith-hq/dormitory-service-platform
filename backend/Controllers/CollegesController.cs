using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("colleges")]
public class CollegesController : ControllerBase
{
    // GET /colleges
    [HttpGet]
    public IActionResult GetColleges()
    {
        return StatusCode(501, ApiResponse.Error(501, "学院列表接口尚未实现"));
    }

    // POST /colleges
    [HttpPost]
    public IActionResult CreateCollege([FromBody] object request)
    {
        return StatusCode(501, ApiResponse.Error(501, "创建学院接口尚未实现"));
    }

    // PUT /colleges/{id}
    [HttpPut("{id}")]
    public IActionResult UpdateCollege(int id, [FromBody] object request)
    {
        return StatusCode(501, ApiResponse.Error(501, "修改学院接口尚未实现"));
    }

    // DELETE /colleges/{id}
    [HttpDelete("{id}")]
    public IActionResult DeleteCollege(int id)
    {
        return StatusCode(501, ApiResponse.Error(501, "删除学院接口尚未实现"));
    }
}