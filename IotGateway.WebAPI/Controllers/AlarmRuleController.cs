using System.ComponentModel.DataAnnotations;
using IotGateway.Application.DTOs;
using IotGateway.Application.Services;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using IotGateway.WebAPI.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Authorize]
[AlarmApiValidation]
[Route("api/alarm-rule")]
public sealed class AlarmRuleController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var rules = await db.AlarmRules.AsNoTracking().OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id).ToListAsync(cancellationToken);
        return Ok(ApiResponse<AlarmRuleDto[]>.Ok(rules.Select(AlarmRuleEvaluator.ToDto).ToArray()));
    }

    [HttpPost]
    [Authorize(Policy = "AlarmWrite")]
    public async Task<IActionResult> Create([FromBody, Required] AlarmRuleRequest request,
        CancellationToken cancellationToken)
    {
        var rule = new AlarmRule { CreatedBy = AlarmApiValidationAttribute.ActorId(User) };
        AlarmRuleEvaluator.Apply(request, rule);
        db.AlarmRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<AlarmRuleDto>.Ok(AlarmRuleEvaluator.ToDto(rule)));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "AlarmWrite")]
    public async Task<IActionResult> Update(long id, [FromBody, Required] AlarmRuleRequest request,
        CancellationToken cancellationToken)
    {
        AlarmApiValidationAttribute.ActorId(User);
        var rule = await db.AlarmRules.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("告警规则", id);
        AlarmRuleEvaluator.Apply(request, rule);
        rule.UpdatedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppException("告警规则已被修改或删除，请刷新后重试。", 409);
        }
        return Ok(ApiResponse<AlarmRuleDto>.Ok(AlarmRuleEvaluator.ToDto(rule)));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "AlarmWrite")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        AlarmApiValidationAttribute.ActorId(User);
        if (!await db.AlarmRules.AnyAsync(r => r.Id == id, cancellationToken))
            throw new NotFoundException("告警规则", id);
        try
        {
            var affected = await db.AlarmRules.Where(r => r.Id == id &&
                    !db.Alarms.Any(a => a.RuleId == r.Id))
                .ExecuteDeleteAsync(cancellationToken);
            if (affected == 0)
                throw new AppException("规则已被历史告警引用或已被删除，不能删除告警审计记录。", 409);
        }
        catch (MySqlException exception) when (exception.Number == 1451)
        {
            throw new AppException("规则已被历史告警引用，不能删除。", 409);
        }
        return Ok(ApiResponse.Ok());
    }

    [HttpPut("{id:long}/toggle")]
    [Authorize(Policy = "AlarmWrite")]
    public async Task<IActionResult> Toggle(long id, [FromBody, Required] ToggleAlarmRuleRequest request,
        CancellationToken cancellationToken)
    {
        AlarmApiValidationAttribute.ActorId(User);
        var enabled = request.Enabled!.Value ? (sbyte)1 : (sbyte)0;
        var now = DateTime.UtcNow;
        var affected = await db.AlarmRules.Where(r => r.Id == id).ExecuteUpdateAsync(update => update
            .SetProperty(r => r.IsEnabled, enabled).SetProperty(r => r.UpdatedAt, now), cancellationToken);
        if (affected == 0)
            throw new NotFoundException("告警规则", id);
        return Ok(ApiResponse.Ok());
    }

    [HttpPost("test")]
    [Authorize(Policy = "AlarmWrite")]
    public async Task<IActionResult> Test([FromBody, Required] AlarmRuleTestRequest request,
        CancellationToken cancellationToken)
    {
        AlarmApiValidationAttribute.ActorId(User);
        var rule = new AlarmRule();
        AlarmRuleEvaluator.Apply(request, rule);
        var supplied = request.Value ?? request.TestValue;
        if (supplied.HasValue)
        {
            var matched = AlarmRuleEvaluator.Matches(rule, supplied.Value);
            return Ok(ApiResponse<AlarmRuleTestResult>.Ok(new AlarmRuleTestResult(matched,
                matched ? $"提供的测试值 {supplied.Value} 满足告警条件。" : $"提供的测试值 {supplied.Value} 不满足告警条件。")));
        }

        var sample = await (from data in db.DeviceData.AsNoTracking()
                            join device in db.Devices.AsNoTracking() on data.DeviceId equals device.Id
                            where data.Quality == 192 && data.ParamName == rule.ParamName &&
                                  device.Category == rule.DeviceType
                            orderby data.CollectedAt descending, data.Id descending
                            select new { Data = data, Device = device }).FirstOrDefaultAsync(cancellationToken);
        if (sample is null)
            return Ok(ApiResponse<AlarmRuleTestResult>.Ok(new AlarmRuleTestResult(false,
                "未找到匹配设备类型和指标的良好质量真实遥测数据；请先采集数据或提供 value/testValue。")));

        var matches = AlarmRuleEvaluator.Matches(rule, sample.Data.ParamValue);
        var message = matches
            ? AlarmRuleEvaluator.RenderMessage(rule, sample.Device, sample.Data.ParamValue)
            : $"设备 {sample.Device.DeviceCode} 最新良好质量数据 {sample.Data.ParamValue} 不满足告警条件。";
        return Ok(ApiResponse<AlarmRuleTestResult>.Ok(new AlarmRuleTestResult(matches, message)));
    }
}
