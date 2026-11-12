using System.Security.Claims;
using IotGateway.Application.DTOs;
using IotGateway.Application.Services;
using IotGateway.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Authorize]
[MesRequestValidation]
[Route("api/mes")]
public class MesController : ControllerBase
{
    private const string Managers = "admin,engineer";
    private const string Production = "admin,engineer,operator";
    private readonly IMesService _mes;

    public MesController(IMesService mes) => _mes = mes;

    private string OperatorName => new[]
    {
        User.FindFirstValue("RealName"), User.Identity?.Name, User.FindFirstValue(ClaimTypes.NameIdentifier)
    }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "已认证用户";

    [HttpGet("products")]
    public async Task<IActionResult> Products() => Ok(ApiResponse.Ok(await _mes.GetProductsAsync()));

    [HttpPost("products")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> CreateProduct([FromBody] MesProductRequest request) =>
        Ok(ApiResponse.Ok(await _mes.SaveProductAsync(null, request), "产品创建成功"));

    [HttpPut("products/{id:long}")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> UpdateProduct(long id, [FromBody] MesProductRequest request) =>
        Ok(ApiResponse.Ok(await _mes.SaveProductAsync(id, request), "产品更新成功"));

    [HttpDelete("products/{id:long}")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> DeleteProduct(long id)
    {
        await _mes.DeleteProductAsync(id);
        return Ok(ApiResponse.Ok(message: "产品删除成功"));
    }

    [HttpGet("lines")]
    public async Task<IActionResult> Lines() => Ok(ApiResponse.Ok(await _mes.GetLinesAsync()));

    [HttpPost("lines")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> CreateLine([FromBody] MesLineRequest request) =>
        Ok(ApiResponse.Ok(await _mes.SaveLineAsync(null, request), "产线创建成功"));

    [HttpPut("lines/{id:long}")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> UpdateLine(long id, [FromBody] MesLineRequest request) =>
        Ok(ApiResponse.Ok(await _mes.SaveLineAsync(id, request), "产线更新成功"));

    [HttpDelete("lines/{id:long}")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> DeleteLine(long id)
    {
        await _mes.DeleteLineAsync(id);
        return Ok(ApiResponse.Ok(message: "产线删除成功"));
    }

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null, [FromQuery] string? status = null) =>
        Ok(ApiResponse.Ok(await _mes.GetOrdersAsync(page, pageSize, keyword, status)));

    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> Order(long id) => Ok(ApiResponse.Ok(await _mes.GetOrderAsync(id)));

    [HttpPost("orders")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> CreateOrder([FromBody] MesOrderRequest request) =>
        Ok(ApiResponse.Ok(await _mes.SaveOrderAsync(null, request, OperatorName), "生产订单创建成功"));

    [HttpPut("orders/{id:long}")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> UpdateOrder(long id, [FromBody] MesOrderRequest request) =>
        Ok(ApiResponse.Ok(await _mes.SaveOrderAsync(id, request, OperatorName), "生产订单更新成功"));

    [HttpDelete("orders/{id:long}")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> DeleteOrder(long id)
    {
        await _mes.DeleteOrderAsync(id, OperatorName);
        return Ok(ApiResponse.Ok(message: "计划订单已删除，追溯历史已保留"));
    }

    [HttpPost("orders/{id:long}/transition")]
    [Authorize(Roles = Production)]
    public async Task<IActionResult> Transition(long id, [FromBody] MesTransitionRequest request) =>
        Ok(ApiResponse.Ok(await _mes.TransitionAsync(id, request, OperatorName), "订单流转成功"));

    [HttpPost("orders/{id:long}/reports")]
    [Authorize(Roles = Production)]
    public async Task<IActionResult> Report(long id, [FromBody] MesReportRequest request) =>
        Ok(ApiResponse.Ok(await _mes.ReportAsync(id, request, OperatorName), "报工成功"));

    [HttpGet("orders/{id:long}/reports")]
    public async Task<IActionResult> Reports(long id) => Ok(ApiResponse.Ok(await _mes.GetReportsAsync(id)));

    [HttpGet("inspections")]
    public async Task<IActionResult> Inspections([FromQuery] long? orderId = null) =>
        Ok(ApiResponse.Ok(await _mes.GetInspectionsAsync(orderId)));

    [HttpPost("inspections")]
    [Authorize(Roles = Production)]
    public async Task<IActionResult> Inspect([FromBody] MesInspectionRequest request) =>
        Ok(ApiResponse.Ok(await _mes.InspectAsync(request, OperatorName), "质检记录已保存"));

    [HttpGet("trace")]
    public async Task<IActionResult> Trace([FromQuery] string? batchNo) =>
        Ok(ApiResponse.Ok(await _mes.TraceAsync(batchNo)));

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard() => Ok(ApiResponse.Ok(await _mes.GetDashboardAsync()));
}

public sealed class MesRequestValidationAttribute : ActionFilterAttribute
{
    public MesRequestValidationAttribute() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
            context.Result = new UnprocessableEntityObjectResult(
                ApiResponse.Fail("请求格式无效，请检查必填项、日期格式以及数量和编号的整数类型", 422));
    }
}
