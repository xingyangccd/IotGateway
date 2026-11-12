using IotGateway.Common.Helpers;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using IotGateway.WebAPI.DTOs;
using IotGateway.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IotGateway.WebAPI.Services.MaintenanceOperations;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Route("api/repair-orders")]
[Authorize(Policy = "MaintenanceRead")]
[MaintenanceValidation]
public sealed class RepairOrdersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int page = 1, int pageSize = 20,
        long? deviceId = null, long? assignedTo = null, string? status = null, string? severity = null,
        string? keyword = null, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        Require(!deviceId.HasValue || deviceId > 0, "设备ID必须大于0");
        Require(!assignedTo.HasValue || assignedTo > 0, "指派用户ID必须大于0");
        var dates = Dates(from, to);
        var query = db.Set<RepairOrder>().AsNoTracking().AsQueryable();
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId);
        if (assignedTo.HasValue) query = query.Where(x => x.AssignedTo == assignedTo);
        if (status != null)
        {
            var value = Choice(status, "状态", RepairStates);
            query = query.Where(x => x.Status == value);
        }
        if (severity != null)
        {
            var value = Choice(severity, "紧急程度", "Normal", "Urgent", "Critical");
            query = query.Where(x => x.Severity == value);
        }
        var search = Text(keyword, "关键字", 100);
        if (search.Length > 0)
            query = query.Where(x => x.OrderNo.Contains(search) ||
                (x.FaultPhenomenon != null && x.FaultPhenomenon.Contains(search)));
        if (dates.From.HasValue) query = query.Where(x => x.CreatedAt >= dates.From);
        if (dates.To.HasValue) query = query.Where(x => x.CreatedAt <= dates.To);
        return Ok(ApiResponse.Ok(await PageAsync(query.OrderByDescending(x => x.Id), page, pageSize, ct)));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await RepairAsync(db, id, ct)));

    [HttpPost]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<IActionResult> Create([FromBody] RepairOrderRequest request, CancellationToken ct)
    {
        var order = await WriteAsync(db, async () =>
        {
            var actor = await ActorAsync(db, User, ct);
            var entity = new RepairOrder
            {
                OrderNo = "RO" + Guid.NewGuid().ToString("N")[..28],
                ReporterId = actor.Id,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            await ApplyRequestAsync(entity, request, ct);
            entity.UpdatedAt = entity.CreatedAt;
            db.Set<RepairOrder>().Add(entity);
            return entity;
        }, ct);
        return Ok(ApiResponse.Ok(order));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<IActionResult> Update(long id, [FromBody] RepairOrderRequest request, CancellationToken ct)
    {
        var order = await WriteAsync(db, async () =>
        {
            await ActorAsync(db, User, ct);
            var entity = await RepairAsync(db, id, ct);
            State(entity, "Pending");
            await ApplyRequestAsync(entity, request, ct);
            entity.UpdatedAt = DateTime.UtcNow;
            return entity;
        }, ct);
        return Ok(ApiResponse.Ok(order));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await WriteAsync(db, async () =>
        {
            await ActorAsync(db, User, ct);
            var order = await RepairAsync(db, id, ct);
            State(order, "Pending");
            db.Set<RepairOrder>().Remove(order);
            return true;
        }, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpPost("{id:long}/assign")]
    [Authorize(Policy = "MaintenanceWrite")]
    public Task<IActionResult> Assign(long id, [FromBody] RepairAssignmentRequest request, CancellationToken ct) =>
        TransitionAsync(id, ["Pending", "Assigned"], async (order, actor, now) =>
        {
            var assignee = await EligibleUserAsync(db, request.AssignedTo, false, ct);
            order.AssignedTo = assignee.Id;
            order.AssignedAt = now;
            order.Status = "Assigned";
        }, ct);

    [HttpPost("{id:long}/start")]
    [Authorize(Policy = "MaintenanceWrite")]
    public Task<IActionResult> Start(long id, CancellationToken ct) =>
        TransitionAsync(id, ["Assigned"], (order, actor, now) =>
        {
            Require(order.AssignedTo == actor.Id, "仅当前指派人可以开始维修", 403);
            Require(order.AssignedAt.HasValue && order.AssignedAt <= now, "工单指派时间无效", 409);
            order.StartedAt = now;
            order.Status = "Repairing";
            return Task.CompletedTask;
        }, ct);

    [HttpPost("{id:long}/complete")]
    [Authorize(Policy = "MaintenanceWrite")]
    public Task<IActionResult> Complete(long id, [FromBody] RepairCompletionRequest request, CancellationToken ct) =>
        TransitionAsync(id, ["Repairing"], (order, actor, now) =>
        {
            Require(order.AssignedTo == actor.Id, "仅当前指派人可以完成维修", 403);
            Require(order.StartedAt.HasValue && order.StartedAt <= now, "工单开始时间无效", 409);
            Cost(request.RepairCost);
            Require(request.DowntimeMinutes >= 0, "停机时长不能为负数");
            order.FaultCause = Text(request.FaultCause, "故障原因", required: true);
            order.RepairAction = Text(request.RepairAction, "维修措施", required: true);
            order.RepairCost = request.RepairCost;
            order.DowntimeMinutes = request.DowntimeMinutes;
            order.SparePartsUsed = Json(request.SparePartsUsed, "使用备件");
            order.Remark = Text(request.Remark, "备注");
            order.CompletedAt = now;
            order.Status = "Completed";
            return Task.CompletedTask;
        }, ct);

    [HttpPost("{id:long}/verify")]
    [Authorize(Policy = "MaintenanceWrite")]
    public Task<IActionResult> Verify(long id, CancellationToken ct) =>
        TransitionAsync(id, ["Completed"], (order, actor, now) =>
        {
            Require(order.CompletedAt.HasValue && order.CompletedAt <= now &&
                order.StartedAt.HasValue && order.StartedAt <= order.CompletedAt &&
                !string.IsNullOrWhiteSpace(order.FaultCause) && !string.IsNullOrWhiteSpace(order.RepairAction),
                "工单缺少有效的维修完成信息", 409);
            order.VerifiedBy = actor.Id;
            order.VerifiedAt = now;
            order.Status = "Verified";
            return Task.CompletedTask;
        }, ct);

    [HttpPost("{id:long}/close")]
    [Authorize(Policy = "MaintenanceWrite")]
    public Task<IActionResult> Close(long id, CancellationToken ct) =>
        TransitionAsync(id, ["Verified"], (order, actor, now) =>
        {
            Require(order.VerifiedBy.HasValue && order.VerifiedAt.HasValue && order.VerifiedAt <= now,
                "工单缺少验证信息", 409);
            // The existing entity has no ClosedAt column; UpdatedAt records final closure time.
            order.Status = "Closed";
            return Task.CompletedTask;
        }, ct);

    private async Task<IActionResult> TransitionAsync(long id, string[] states,
        Func<RepairOrder, Actor, DateTime, Task> action, CancellationToken ct)
    {
        var order = await WriteAsync(db, async () =>
        {
            var actor = await ActorAsync(db, User, ct);
            var entity = await RepairAsync(db, id, ct);
            State(entity, states);
            var now = DateTime.UtcNow;
            await action(entity, actor, now);
            entity.UpdatedAt = now;
            return entity;
        }, ct);
        return Ok(ApiResponse.Ok(order));
    }

    private async Task ApplyRequestAsync(RepairOrder order, RepairOrderRequest request, CancellationToken ct)
    {
        await DeviceAsync(db, request.DeviceId, ct);
        order.DeviceId = request.DeviceId;
        order.FaultPhenomenon = Text(request.FaultPhenomenon, "故障现象", required: true);
        order.Severity = Choice(request.Severity, "紧急程度", "Normal", "Urgent", "Critical");
        order.Remark = Text(request.Remark, "备注");
    }
}
