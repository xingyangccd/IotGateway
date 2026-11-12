using IotGateway.Common.Exceptions;
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
[Route("api/maintenance")]
[Authorize(Policy = "MaintenanceRead")]
[MaintenanceValidation]
public sealed class MaintenanceController(AppDbContext db) : ControllerBase
{
    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct, int page = 1, int pageSize = 20,
        long? deviceId = null, bool? isEnabled = null, string? keyword = null)
    {
        Require(!deviceId.HasValue || deviceId > 0, "设备ID必须大于0");
        var query = db.Set<MaintenancePlan>().AsNoTracking().AsQueryable();
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId);
        if (isEnabled.HasValue) query = query.Where(x => x.IsEnabled == (isEnabled.Value ? 1 : 0));
        var search = Text(keyword, "关键字", 100);
        if (search.Length > 0) query = query.Where(x => x.PlanName != null && x.PlanName.Contains(search));
        return Ok(ApiResponse.Ok(await PageAsync(query.OrderByDescending(x => x.Id), page, pageSize, ct)));
    }

    [HttpGet("plans/{id:long}")]
    public async Task<IActionResult> Plan(long id, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await PlanAsync(db, id, ct)));

    [HttpPost("plans")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<IActionResult> CreatePlan([FromBody] MaintenancePlanRequest request, CancellationToken ct)
    {
        var plan = await WriteAsync(db, async () =>
        {
            await ActorAsync(db, User, ct);
            var entity = new MaintenancePlan();
            await ApplyPlanAsync(entity, request, ct);
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
            db.Set<MaintenancePlan>().Add(entity);
            return entity;
        }, ct);
        return Ok(ApiResponse.Ok(plan));
    }

    [HttpPut("plans/{id:long}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<IActionResult> UpdatePlan(long id, [FromBody] MaintenancePlanRequest request, CancellationToken ct)
    {
        var plan = await WriteAsync(db, async () =>
        {
            await ActorAsync(db, User, ct);
            var entity = await PlanAsync(db, id, ct);
            if (entity.DeviceId != request.DeviceId)
                Require(!await db.Set<MaintenanceRecord>().AnyAsync(x => x.PlanId == id, ct),
                    "已有执行记录的计划不能更换设备", 409);
            await ApplyPlanAsync(entity, request, ct);
            entity.UpdatedAt = DateTime.UtcNow;
            return entity;
        }, ct);
        return Ok(ApiResponse.Ok(plan));
    }

    [HttpPatch("plans/{id:long}/enable")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<IActionResult> EnablePlan(long id, [FromBody] MaintenanceEnableRequest request, CancellationToken ct)
    {
        Require(request.IsEnabled.HasValue, "必须指定isEnabled");
        var plan = await WriteAsync(db, async () =>
        {
            await ActorAsync(db, User, ct);
            var entity = await PlanAsync(db, id, ct);
            entity.IsEnabled = (sbyte)(request.IsEnabled!.Value ? 1 : 0);
            entity.UpdatedAt = DateTime.UtcNow;
            return entity;
        }, ct);
        return Ok(ApiResponse.Ok(plan));
    }

    [HttpDelete("plans/{id:long}")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<IActionResult> DeletePlan(long id, CancellationToken ct)
    {
        await WriteAsync(db, async () =>
        {
            await ActorAsync(db, User, ct);
            var plan = await PlanAsync(db, id, ct);
            Require(!await db.Set<MaintenanceRecord>().AnyAsync(x => x.PlanId == id, ct),
                "已有执行记录的计划不能删除，请停用", 409);
            db.Set<MaintenancePlan>().Remove(plan);
            return true;
        }, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpGet("records")]
    public async Task<IActionResult> Records(CancellationToken ct, int page = 1, int pageSize = 20,
        long? deviceId = null, long? planId = null, string? result = null, string? maintenanceType = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        Require(!deviceId.HasValue || deviceId > 0, "设备ID必须大于0");
        Require(!planId.HasValue || planId >= 0, "计划ID不能为负数");
        var dates = Dates(from, to);
        var query = db.Set<MaintenanceRecord>().AsNoTracking().AsQueryable();
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId);
        if (planId.HasValue) query = query.Where(x => x.PlanId == planId);
        if (result != null)
        {
            var value = Choice(result, "结果", "Completed", "Partial", "Failed");
            query = query.Where(x => x.Result == value);
        }
        if (maintenanceType != null)
        {
            var value = Choice(maintenanceType, "维保类型", "Planned", "Emergency");
            query = query.Where(x => x.MaintenanceType == value);
        }
        if (dates.From.HasValue) query = query.Where(x => x.ExecutedAt >= dates.From);
        if (dates.To.HasValue) query = query.Where(x => x.ExecutedAt <= dates.To);
        return Ok(ApiResponse.Ok(await PageAsync(query.OrderByDescending(x => x.ExecutedAt).ThenByDescending(x => x.Id),
            page, pageSize, ct)));
    }

    [HttpGet("records/{id:long}")]
    public async Task<IActionResult> Record(long id, CancellationToken ct)
    {
        Require(id > 0, "记录ID必须大于0");
        var record = await db.Set<MaintenanceRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessException("维保记录不存在", 404);
        return Ok(ApiResponse.Ok(record));
    }

    [HttpPost("records")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<IActionResult> CreateRecord([FromBody] MaintenanceRecordRequest request, CancellationToken ct)
    {
        var record = await WriteAsync(db, async () =>
        {
            var actor = await ActorAsync(db, User, ct);
            await DeviceAsync(db, request.DeviceId, ct);
            var type = Choice(request.MaintenanceType, "维保类型", "Planned", "Emergency");
            var result = Choice(request.Result, "结果", "Completed", "Partial", "Failed");
            Require(type == "Planned" ? request.PlanId > 0 : request.PlanId == 0,
                "计划维保必须指定计划；紧急维保的planId必须为0");
            MaintenancePlan? plan = null;
            if (type == "Planned")
            {
                plan = await PlanAsync(db, request.PlanId, ct);
                Require(plan.DeviceId == request.DeviceId, "计划与设备不一致");
                Require(plan.IsEnabled == 1, "计划已停用", 409);
                Choice(plan.CycleType, "计划周期", "Calendar", "RunHours");
                Require(plan.CycleValue > 0, "计划周期值无效");
            }
            Cost(request.Cost);
            var now = DateTime.UtcNow;
            Require(request.ExecutedAt.HasValue, "必须指定executedAt");
            var executedAt = request.ExecutedAt!.Value.UtcDateTime;
            Require(executedAt.Year >= 1000 && executedAt <= now, "执行时间无效或晚于当前时间");
            DateTime? nextDue = null;
            if (plan != null && result == "Completed" &&
                string.Equals(plan.CycleType, "Calendar", StringComparison.OrdinalIgnoreCase))
            {
                Require(plan.CycleValue <= (DateTime.MaxValue - executedAt).TotalDays, "下次维保日期超出范围");
                nextDue = executedAt.AddDays(plan.CycleValue);
            }
            // RunHours schedules cannot be converted to dates without a runtime counter snapshot.
            var entity = new MaintenanceRecord
            {
                PlanId = request.PlanId,
                DeviceId = request.DeviceId,
                MaintenanceType = type,
                ExecutorName = Text(actor.Name, "执行人姓名", 50, true),
                ItemsCompleted = Text(request.ItemsCompleted, "执行项目", required: true),
                SparePartsUsed = Json(request.SparePartsUsed, "使用备件"),
                Result = result,
                Cost = request.Cost,
                Remark = Text(request.Remark, "备注", required: result != "Completed"),
                ExecutedAt = executedAt,
                NextDueAt = nextDue,
                CreatedAt = now
            };
            db.Set<MaintenanceRecord>().Add(entity);
            return entity;
        }, ct);
        return Ok(ApiResponse.Ok(record));
    }

    private async Task ApplyPlanAsync(MaintenancePlan plan, MaintenancePlanRequest request, CancellationToken ct)
    {
        await DeviceAsync(db, request.DeviceId, ct);
        plan.DeviceId = request.DeviceId;
        plan.PlanName = Text(request.PlanName, "计划名称", 100, true);
        plan.CycleType = Choice(request.CycleType, "周期类型", "Calendar", "RunHours");
        Require(request.CycleValue > 0 && (plan.CycleType != "Calendar" ||
            request.CycleValue <= (DateTime.MaxValue - DateTime.UtcNow).TotalDays), "周期值无效或过大");
        Require(request.EstimatedDuration > 0, "预计时长必须大于0分钟");
        plan.CycleValue = request.CycleValue;
        plan.EstimatedDuration = request.EstimatedDuration;
        plan.Items = Json(request.Items, "维保项目", arrayOnly: true, required: true);
        plan.RequiredSpareParts = Json(request.RequiredSpareParts, "所需备件");
        plan.IsEnabled = (sbyte)(request.IsEnabled ? 1 : 0);
    }
}
