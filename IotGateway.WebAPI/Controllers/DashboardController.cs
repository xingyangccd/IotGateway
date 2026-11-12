using IotGateway.Common.Helpers;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var deviceTotal = await db.Devices.CountAsync(cancellationToken);
        var deviceOnline = await db.Devices.CountAsync(d => d.ConnectionStatus == 1, cancellationToken);
        var activeAlarms = await db.Alarms.CountAsync(a =>
            a.Status.ToLower() != "resolved" && a.Status.ToLower() != "closed", cancellationToken);
        long total;
        long good;
        if (db.Model.FindEntityType(typeof(MesProductionReport)) is not null)
        {
            var reports = db.Set<MesProductionReport>().Where(r => r.ReportedAt >= today && r.ReportedAt < tomorrow);
            good = await reports.SumAsync(r => (long)r.GoodQuantity, cancellationToken);
            var scrap = await reports.SumAsync(r => (long)r.ScrapQuantity, cancellationToken);
            total = good + scrap;
        }
        else
        {
            var production = db.ProductionRecords.Where(p =>
                (p.ShiftDate ?? p.CreatedAt) >= today && (p.ShiftDate ?? p.CreatedAt) < tomorrow);
            total = await production.SumAsync(p => (long)p.ActualQuantity, cancellationToken);
            var defects = await production.SumAsync(p => (long)p.DefectQuantity, cancellationToken);
            good = Math.Max(0, total - defects);
        }
        return Ok(ApiResponse.Ok(new
        {
            deviceTotal, deviceOnline, activeAlarms, todayProduction = total,
            goodRate = total == 0 ? 0m : Math.Round((decimal)good / total * 100, 2),
            simulationEnabled = configuration.GetValue<bool>("Simulation:Enabled")
        }));
    }
}
