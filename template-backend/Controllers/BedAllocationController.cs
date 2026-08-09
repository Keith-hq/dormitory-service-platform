using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 住宿分配 — DORM-08/09/10
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BedAllocationController : ControllerBase
{
    /// <summary>DORM-08 入住分配</summary>
    [HttpPost("checkin")]
    public ActionResult<ApiResponse<object>> CheckIn([FromBody] object dto)
    {
        // TODO: 分配学生到房间床位，D_Room.Occupancy +1（事务）
        return Ok(ApiResponse.Ok(new { }));
    }

    /// <summary>DORM-09 调寝</summary>
    [HttpPost("transfer")]
    public ActionResult<ApiResponse<object>> Transfer([FromBody] object dto)
    {
        // TODO: 写旧记录 CheckOut_Date，建新记录，联动两个房间 Occupancy
        return Ok(ApiResponse.Ok(new { }));
    }

    /// <summary>DORM-10 房间住户</summary>
    [HttpGet("residents/{roomId}")]
    public ActionResult<ApiResponse<object>> Residents(int roomId)
    {
        // TODO: 查询某房间当前所有在住学生信息
        return Ok(ApiResponse.Ok(new { items = Array.Empty<object>() }));
    }
}
