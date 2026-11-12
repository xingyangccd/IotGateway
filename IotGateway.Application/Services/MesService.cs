using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IotGateway.Application.DTOs;
using IotGateway.Common.Exceptions;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IotGateway.Application.Services;

public class MesService : IMesService
{
    private static readonly string[] Statuses = { "Planned", "Released", "InProgress", "Paused", "Completed", "Cancelled" };
    private readonly AppDbContext _db;
    private readonly ILogger<MesService> _logger;

    public MesService(AppDbContext db, ILogger<MesService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MesProductDto>> GetProductsAsync() =>
        (await _db.Set<MesProduct>().AsNoTracking().OrderBy(x => x.Code).ToListAsync()).Select(ProductDto).ToArray();

    public Task<MesProductDto> SaveProductAsync(long? id, MesProductRequest request)
    {
        var code = RequiredText(request.Code, "产品编码", 64);
        var name = RequiredText(request.Name, "产品名称", 200);
        var unit = RequiredText(request.Unit, "计量单位", 32);
        var specification = OptionalText(request.Specification, "产品规格", 500) ?? "";
        if (request.CycleTimeSeconds <= 0 || request.CycleTimeSeconds > 999999999999999.999m
            || decimal.Round(request.CycleTimeSeconds, 3) != request.CycleTimeSeconds)
            throw new ValidationException("标准节拍必须大于零，最多保留三位小数且不能超过十五位整数");
        return WriteAsync(async () =>
        {
            var product = id.HasValue
                ? await _db.Set<MesProduct>().SingleOrDefaultAsync(x => x.Id == id.Value)
                    ?? throw new NotFoundException("产品", id.Value)
                : new MesProduct();
            if (await _db.Set<MesProduct>().AnyAsync(x => x.Code == code && x.Id != product.Id))
                throw Conflict("产品编码已存在");
            product.Code = code;
            product.Name = name;
            product.Specification = specification;
            product.Unit = unit;
            product.CycleTimeSeconds = request.CycleTimeSeconds;
            product.Version++;
            if (!id.HasValue) _db.Add(product);
            await _db.SaveChangesAsync();
            return ProductDto(product);
        });
    }

    public Task DeleteProductAsync(long id) => WriteAsync(async () =>
    {
        var product = await _db.Set<MesProduct>().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException("产品", id);
        if (await _db.Set<MesOrder>().IgnoreQueryFilters().AnyAsync(x => x.ProductId == id))
            throw Conflict("产品已被生产订单引用，不能删除");
        _db.Remove(product);
        await _db.SaveChangesAsync();
        return true;
    });

    public async Task<IReadOnlyList<MesLineDto>> GetLinesAsync()
    {
        var lines = await _db.Set<MesLine>().AsNoTracking().OrderBy(x => x.Code).ToListAsync();
        var active = await _db.Set<MesOrder>().AsNoTracking()
            .Where(x => x.Status == "Released" || x.Status == "InProgress" || x.Status == "Paused")
            .Select(x => new { x.LineId, x.Status }).ToListAsync();
        return lines.Select(x => LineDto(x, LineStatus(active.Where(o => o.LineId == x.Id).Select(o => o.Status)))).ToArray();
    }

    public Task<MesLineDto> SaveLineAsync(long? id, MesLineRequest request)
    {
        var code = RequiredText(request.Code, "产线编码", 64);
        var name = RequiredText(request.Name, "产线名称", 200);
        var workshop = RequiredText(request.Workshop, "车间", 100);
        if (request.DeviceId.HasValue && request.DeviceId <= 0)
            throw new ValidationException("关联设备编号必须为正整数");
        return WriteAsync(async () =>
        {
            var line = id.HasValue
                ? await _db.Set<MesLine>().SingleOrDefaultAsync(x => x.Id == id.Value)
                    ?? throw new NotFoundException("产线", id.Value)
                : new MesLine();
            if (await _db.Set<MesLine>().AnyAsync(x => x.Code == code && x.Id != line.Id))
                throw Conflict("产线编码已存在");
            if (request.DeviceId.HasValue && !await _db.Set<Device>().AnyAsync(x => x.Id == request.DeviceId.Value))
                throw new NotFoundException("关联设备", request.DeviceId.Value);
            line.Code = code;
            line.Name = name;
            line.Workshop = workshop;
            line.DeviceId = request.DeviceId;
            line.Version++;
            if (!id.HasValue) _db.Add(line);
            var states = await _db.Set<MesOrder>().AsNoTracking()
                .Where(x => x.LineId == line.Id && (x.Status == "Released" || x.Status == "InProgress" || x.Status == "Paused"))
                .Select(x => x.Status).ToListAsync();
            await _db.SaveChangesAsync();
            return LineDto(line, LineStatus(states));
        });
    }

    public Task DeleteLineAsync(long id) => WriteAsync(async () =>
    {
        var line = await _db.Set<MesLine>().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException("产线", id);
        if (await _db.Set<MesOrder>().IgnoreQueryFilters().AnyAsync(x => x.LineId == id))
            throw Conflict("产线已被生产订单引用，不能删除");
        _db.Remove(line);
        await _db.SaveChangesAsync();
        return true;
    });

    public async Task<MesPageDto<MesOrderDto>> GetOrdersAsync(int page, int pageSize, string? keyword, string? status)
    {
        if (page < 1 || pageSize < 1 || pageSize > 200 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("页码必须为正整数，每页数量必须在一至二百之间，且不能超过可查询范围");
        var query = OrderQuery().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Statuses.Contains(status)) throw new ValidationException("生产订单状态无效");
            query = query.Where(x => x.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = RequiredText(keyword, "搜索关键字", 200);
            query = query.Where(x => x.OrderNo.Contains(term) || x.BatchNo.Contains(term)
                || x.Product.Name.Contains(term) || x.Line.Name.Contains(term));
        }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new MesPageDto<MesOrderDto>(items.Select(OrderDto).ToArray(), total, page, pageSize);
    }

    public async Task<MesOrderDto> GetOrderAsync(long id) => OrderDto(await RequireOrderAsync(id, false));

    public Task<MesOrderDto> SaveOrderAsync(long? id, MesOrderRequest request, string operatorName)
    {
        var orderNo = RequiredText(request.OrderNo, "生产订单号", 64);
        var batchNo = RequiredText(request.BatchNo, "生产批次号", 100);
        if (request.ProductId <= 0 || request.LineId <= 0)
            throw new ValidationException("产品和产线编号必须为正整数");
        if (request.PlannedQuantity <= 0) throw new ValidationException("计划数量必须为正整数");
        if (request.Priority is < 1 or > 5) throw new ValidationException("优先级必须为一至五的整数");
        var start = Utc(request.PlannedStartAt);
        var due = Utc(request.DueAt);
        if (start.Year < 1000 || due.Year < 1000 || due <= start)
            throw new ValidationException("计划开始时间和交期必须有效，且交期必须晚于计划开始时间");
        var actor = RequiredText(operatorName, "操作人员", 200);
        return WriteAsync(async () =>
        {
            var order = id.HasValue ? await RequireOrderAsync(id.Value) : new MesOrder();
            if (order.Status != "Planned") throw Conflict("只有计划状态的订单可以修改，已发布订单不能修改产品、数量或其他计划信息");
            if (await _db.Set<MesOrder>().IgnoreQueryFilters().AnyAsync(x => x.OrderNo == orderNo && x.Id != order.Id))
                throw Conflict("生产订单号已存在，历史订单号也不能重复使用");
            var product = await _db.Set<MesProduct>().SingleOrDefaultAsync(x => x.Id == request.ProductId)
                ?? throw new NotFoundException("产品", request.ProductId);
            var line = await _db.Set<MesLine>().SingleOrDefaultAsync(x => x.Id == request.LineId)
                ?? throw new NotFoundException("产线", request.LineId);
            order.OrderNo = orderNo;
            order.ProductId = product.Id;
            order.Product = product;
            order.LineId = line.Id;
            order.Line = line;
            order.PlannedQuantity = request.PlannedQuantity;
            order.PlannedStartAt = start;
            order.DueAt = due;
            order.Priority = request.Priority;
            order.BatchNo = batchNo;
            order.Version++;
            if (!id.HasValue) _db.Add(order);
            AddEvent(order, id.HasValue ? "update" : "create", id.HasValue ? "Planned" : "",
                actor, id.HasValue ? "修改生产计划" : "创建生产订单");
            await _db.SaveChangesAsync();
            return OrderDto(order);
        });
    }

    public Task DeleteOrderAsync(long id, string operatorName) => WriteAsync(async () =>
    {
        var order = await RequireOrderAsync(id);
        if (order.Status != "Planned") throw Conflict("只有计划状态的订单可以删除，其他订单请使用取消操作");
        order.DeletedAt = DateTime.UtcNow;
        order.Version++;
        AddEvent(order, "delete", order.Status, RequiredText(operatorName, "操作人员", 200), "删除计划订单，保留批次追溯和审计历史");
        await _db.SaveChangesAsync();
        return true;
    });

    public Task<MesOrderDto> TransitionAsync(long id, MesTransitionRequest request, string operatorName)
    {
        var action = RequiredText(request.Action, "流转操作", 32);
        if (action is not ("release" or "start" or "pause" or "resume" or "complete" or "cancel"))
            throw new ValidationException("流转操作无效，允许发布、开工、暂停、恢复、完工或取消");
        var actor = RequiredText(operatorName, "操作人员", 200);
        return WriteAsync(async () =>
        {
            var order = await RequireOrderAsync(id);
            var previous = order.Status;
            var next = (previous, action) switch
            {
                ("Planned", "release") => "Released",
                ("Released", "start") => "InProgress",
                ("InProgress", "pause") => "Paused",
                ("Paused", "resume") => "InProgress",
                ("InProgress" or "Paused", "complete") => "Completed",
                ("Planned" or "Released" or "Paused", "cancel") => "Cancelled",
                _ => throw Conflict("当前订单状态不允许执行该流转操作")
            };
            if (next == "Completed")
            {
                if ((long)order.GoodQuantity + order.ScrapQuantity != order.PlannedQuantity)
                    throw Conflict("累计良品与报废数量必须等于计划数量才能完工");
                var final = await _db.Set<MesInspection>().AsNoTracking()
                    .Where(x => x.OrderId == id && x.InspectionType == "Final")
                    .OrderByDescending(x => x.Id).FirstOrDefaultAsync();
                if (final == null || final.Result != "Pass" || final.ReportedQuantity != order.PlannedQuantity)
                    throw Conflict("必须完成全部报工，并且最近一次终检合格后才能完工");
            }
            order.Status = next;
            if (action == "start") order.StartedAt = DateTime.UtcNow;
            if (next == "Completed") order.CompletedAt = DateTime.UtcNow;
            order.Version++;
            var description = action switch
            {
                "release" => "发布生产订单",
                "start" => "生产订单开工",
                "pause" => "暂停生产订单",
                "resume" => "恢复生产订单",
                "complete" => "生产订单完工",
                _ => "取消生产订单"
            };
            AddEvent(order, action, previous, actor, description);
            await _db.SaveChangesAsync();
            return OrderDto(order);
        });
    }

    public Task<MesReportDto> ReportAsync(long id, MesReportRequest request, string operatorName)
    {
        if (!Guid.TryParse(request.RequestId, out var requestGuid) || requestGuid == Guid.Empty)
            throw new ValidationException("报工请求编号必须为非空有效GUID");
        var requestId = requestGuid.ToString("D");
        var payload = JsonSerializer.Serialize(new
        {
            OrderId = id, request.GoodQuantity, request.ScrapQuantity, request.DefectReason, request.Station
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return WriteAsync(async () =>
        {
            // 先检查幂等键，订单完工之后的网络重试仍返回原始结果。
            var existing = await _db.Set<MesProductionReport>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestId == requestId);
            if (existing != null)
            {
                if (existing.PayloadHash != hash) throw Conflict("该报工请求编号已用于不同的报工内容");
                return ReportDto(existing);
            }
            var quantity = (long)request.GoodQuantity + request.ScrapQuantity;
            if (request.GoodQuantity < 0 || request.ScrapQuantity < 0 || quantity <= 0)
                throw new ValidationException("良品和报废数量必须为非负整数，且合计必须大于零");
            OptionalText(request.DefectReason, "报废原因", 1000);
            OptionalText(request.Station, "工位", 100);
            if (request.ScrapQuantity > 0 && string.IsNullOrWhiteSpace(request.DefectReason))
                throw new ValidationException("存在报废数量时必须填写报废原因");
            var actor = RequiredText(operatorName, "报工人员", 200);
            var order = await RequireOrderAsync(id);
            if (order.Status != "InProgress") throw Conflict("只有生产中的订单可以报工");
            if ((long)order.GoodQuantity + order.ScrapQuantity + quantity > order.PlannedQuantity)
                throw Conflict("本次报工将超过计划数量，已拒绝超产");
            var report = new MesProductionReport
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                GoodQuantity = request.GoodQuantity,
                ScrapQuantity = request.ScrapQuantity,
                DefectReason = request.DefectReason,
                Station = request.Station,
                OperatorName = actor,
                RequestId = requestId,
                PayloadHash = hash
            };
            order.GoodQuantity += request.GoodQuantity;
            order.ScrapQuantity += request.ScrapQuantity;
            order.Version++;
            _db.Add(report);
            AddEvent(order, "report", order.Status, actor,
                $"生产报工：良品{report.GoodQuantity}，报废{report.ScrapQuantity}，请求编号{requestId}");
            // 订单版本更新、报工唯一键和审计事件由同一次 SaveChanges 的数据库事务提交。
            await _db.SaveChangesAsync();
            return ReportDto(report);
        }, retryDuplicate: true);
    }

    public async Task<IReadOnlyList<MesReportDto>> GetReportsAsync(long orderId)
    {
        await RequireOrderAsync(orderId, false);
        var reports = await _db.Set<MesProductionReport>().AsNoTracking().Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.Id).ToListAsync();
        return reports.Select(ReportDto).ToArray();
    }

    public async Task<IReadOnlyList<MesInspectionDto>> GetInspectionsAsync(long? orderId)
    {
        var query = _db.Set<MesInspection>().AsNoTracking();
        if (orderId.HasValue)
        {
            await RequireOrderAsync(orderId.Value, false);
            query = query.Where(x => x.OrderId == orderId.Value);
        }
        return (await query.OrderByDescending(x => x.Id).ToListAsync()).Select(InspectionDto).ToArray();
    }

    public Task<MesInspectionDto> InspectAsync(MesInspectionRequest request, string operatorName)
    {
        if (request.InspectionType is not ("First" or "Patrol" or "Final"))
            throw new ValidationException("质检类型必须为首检、巡检或终检对应的有效类型");
        if (request.Result is not ("Pass" or "Fail"))
            throw new ValidationException("质检结论必须为合格或不合格对应的有效类型");
        if (request.SampleQuantity <= 0 || request.QualifiedQuantity < 0 || request.QualifiedQuantity > request.SampleQuantity)
            throw new ValidationException("抽样数量必须为正整数，合格数量必须在零和抽样数量之间");
        if ((request.Result == "Pass") != (request.QualifiedQuantity == request.SampleQuantity))
            throw new ValidationException("质检结论与数量不一致：全部样本合格才可判定合格，否则必须判定不合格");
        var remark = OptionalText(request.Remark, "质检备注", 2000);
        var actor = RequiredText(operatorName, "质检人员", 200);
        return WriteAsync(async () =>
        {
            var order = await RequireOrderAsync(request.OrderId);
            if (order.Status is not ("InProgress" or "Paused"))
                throw Conflict("只有生产中或已暂停的订单可以质检");
            if (request.SampleQuantity > order.PlannedQuantity)
                throw new ValidationException("抽样数量不能超过订单计划数量");
            var reported = order.GoodQuantity + order.ScrapQuantity;
            if (request.InspectionType == "Final" && reported != order.PlannedQuantity)
                throw Conflict("必须完成全部生产报工后才能进行终检");
            var inspection = new MesInspection
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                InspectionType = request.InspectionType!,
                SampleQuantity = request.SampleQuantity,
                QualifiedQuantity = request.QualifiedQuantity,
                Result = request.Result!,
                Remark = remark,
                InspectorName = actor,
                ReportedQuantity = reported
            };
            _db.Add(inspection);
            // 即使质检不改变数量，也更新订单版本，防止与完工或报工交叉提交。
            order.Version++;
            var typeName = request.InspectionType switch { "First" => "首检", "Patrol" => "巡检", _ => "终检" };
            AddEvent(order, "inspection", order.Status, actor,
                $"{typeName}：抽样{request.SampleQuantity}，合格{request.QualifiedQuantity}，结论{(request.Result == "Pass" ? "合格" : "不合格")}");
            await _db.SaveChangesAsync();
            return InspectionDto(inspection);
        });
    }

    public async Task<MesTraceDto> TraceAsync(string? batchNo)
    {
        RequiredText(batchNo, "批次号", 100);
        var candidates = await OrderQuery().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.BatchNo == batchNo).OrderBy(x => x.Id).ToListAsync();
        // MySQL 默认排序规则可能忽略大小写，最终采用序号比较，禁止串批。
        var orders = candidates.Where(x => string.Equals(x.BatchNo, batchNo, StringComparison.Ordinal)).ToArray();
        var ids = orders.Select(x => x.Id).ToArray();
        if (ids.Length == 0)
            return new MesTraceDto(Array.Empty<MesOrderDto>(), Array.Empty<MesReportDto>(),
                Array.Empty<MesInspectionDto>(), Array.Empty<MesOrderEventDto>());
        var reports = await _db.Set<MesProductionReport>().AsNoTracking().Where(x => ids.Contains(x.OrderId))
            .OrderBy(x => x.ReportedAt).ThenBy(x => x.Id).ToListAsync();
        var inspections = await _db.Set<MesInspection>().AsNoTracking().Where(x => ids.Contains(x.OrderId))
            .OrderBy(x => x.InspectedAt).ThenBy(x => x.Id).ToListAsync();
        var events = await _db.Set<MesOrderEvent>().AsNoTracking().Where(x => ids.Contains(x.OrderId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync();
        return new MesTraceDto(orders.Select(OrderDto).ToArray(), reports.Select(ReportDto).ToArray(),
            inspections.Select(InspectionDto).ToArray(), events.Select(EventDto).ToArray());
    }

    public async Task<MesDashboardDto> GetDashboardAsync()
    {
        var orders = await _db.Set<MesOrder>().AsNoTracking().Select(x => new
        {
            x.Id, x.OrderNo, x.LineId, x.Status, x.PlannedQuantity, x.GoodQuantity, x.ScrapQuantity, x.Priority, x.CreatedAt
        }).ToListAsync();
        var lines = await _db.Set<MesLine>().AsNoTracking().OrderBy(x => x.Code).ToListAsync();
        var today = DateTime.UtcNow.Date;
        var firstDay = today.AddDays(-6);
        var tomorrow = today.AddDays(1);
        var reports = await _db.Set<MesProductionReport>().AsNoTracking()
            .Where(x => x.ReportedAt >= firstDay && x.ReportedAt < tomorrow)
            .Select(x => new { x.ReportedAt, x.GoodQuantity, x.ScrapQuantity }).ToListAsync();
        var daily = Enumerable.Range(0, 7).Select(i =>
        {
            var day = firstDay.AddDays(i);
            var dayReports = reports.Where(x => x.ReportedAt.Date == day).ToArray();
            return new MesDailyProductionDto(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                dayReports.Sum(x => (long)x.GoodQuantity), dayReports.Sum(x => (long)x.ScrapQuantity));
        }).ToArray();
        var lineDtos = lines.Select(line =>
        {
            var active = orders.Where(x => x.LineId == line.Id && IsActive(x.Status)).ToArray();
            var current = active.OrderByDescending(x => x.Status == "InProgress")
                .ThenByDescending(x => x.Status == "Paused").ThenBy(x => x.Priority).ThenBy(x => x.CreatedAt).FirstOrDefault();
            var planned = active.Sum(x => (long)x.PlannedQuantity);
            var good = active.Sum(x => (long)x.GoodQuantity);
            var scrap = active.Sum(x => (long)x.ScrapQuantity);
            return new MesLineDashboardDto(line.Id, line.Name, LineStatus(active.Select(x => x.Status)),
                current?.OrderNo, planned, good, scrap, Percent(good + scrap, planned));
        }).ToArray();
        var plannedQuantity = orders.Where(x => x.Status != "Cancelled").Sum(x => (long)x.PlannedQuantity);
        var goodQuantity = orders.Sum(x => (long)x.GoodQuantity);
        var scrapQuantity = orders.Sum(x => (long)x.ScrapQuantity);
        var reportedForPlan = orders.Where(x => x.Status != "Cancelled").Sum(x => (long)x.GoodQuantity + x.ScrapQuantity);
        return new MesDashboardDto(orders.Count, orders.Count(x => IsActive(x.Status)),
            orders.Count(x => x.Status == "Completed"), plannedQuantity, goodQuantity, scrapQuantity,
            Percent(reportedForPlan, plannedQuantity), Percent(goodQuantity, goodQuantity + scrapQuantity), lineDtos, daily);
    }

    private IQueryable<MesOrder> OrderQuery() => _db.Set<MesOrder>().Include(x => x.Product).Include(x => x.Line);

    private async Task<MesOrder> RequireOrderAsync(long id, bool tracking = true)
    {
        var query = OrderQuery();
        if (!tracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("生产订单", id);
    }

    private void AddEvent(MesOrder order, string action, string previous, string actor, string description) =>
        _db.Add(new MesOrderEvent
        {
            Order = order, OrderId = order.Id, Action = action, FromStatus = previous,
            ToStatus = order.Status, OperatorName = actor, Description = description
        });

    private async Task<T> WriteAsync<T>(Func<Task<T>> operation, bool retryDuplicate = false)
    {
        const int attempts = 6;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (ex is DbUpdateException or DbException)
            {
                var database = DatabaseError(ex);
                var concurrent = ex is DbUpdateConcurrencyException;
                var transient = database is "busy";
                var duplicate = database is "duplicate";
                var retry = concurrent || transient || (duplicate && retryDuplicate);
                _logger.LogWarning(ex, "MES数据库写入失败，第{Attempt}次尝试，错误类型{ErrorType}", attempt + 1,
                    concurrent ? "并发版本冲突" : database);
                foreach (var entry in _db.ChangeTracker.Entries().Where(x => x.Entity.GetType().Name.StartsWith("Mes", StringComparison.Ordinal)).ToArray())
                    entry.State = EntityState.Detached;
                if (retry && attempt + 1 < attempts)
                {
                    await Task.Delay(20 * (attempt + 1));
                    continue;
                }
                if (concurrent || duplicate)
                    throw Conflict(duplicate ? "编码、订单号或报工请求编号已存在，请检查后重试" : "订单已被其他操作更新，请重新获取后重试");
                if (transient) throw new BusinessException("数据库正忙，请稍后使用相同请求编号重试", 503);
                if (database == "foreign-key") throw Conflict("关联数据不存在或正在被其他业务引用，操作未提交");
                throw;
            }
        }
        throw new InvalidOperationException("数据库重试流程异常");
    }

    private static string DatabaseError(Exception error)
    {
        for (Exception? current = error; current != null; current = current.InnerException)
        {
            var type = current.GetType();
            if (type.FullName == "Microsoft.Data.Sqlite.SqliteException")
            {
                var code = (int?)type.GetProperty("SqliteErrorCode")?.GetValue(current);
                var extended = (int?)type.GetProperty("SqliteExtendedErrorCode")?.GetValue(current);
                if (code is 5 or 6) return "busy";
                if (extended is 1555 or 2067) return "duplicate";
                if (extended == 787) return "foreign-key";
            }
            if (type.FullName is "MySqlConnector.MySqlException" or "MySql.Data.MySqlClient.MySqlException")
            {
                var number = (int?)type.GetProperty("Number")?.GetValue(current);
                if (number is 1205 or 1213) return "busy";
                if (number == 1062) return "duplicate";
                if (number is 1451 or 1452) return "foreign-key";
            }
        }
        return "其他数据库错误";
    }

    private static BusinessException Conflict(string message) => new(message, 409);

    private static string RequiredText(string? value, string field, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{field}不能为空");
        if (value.Length > maximum) throw new ValidationException($"{field}不能超过{maximum}个字符");
        return value.Trim();
    }

    private static string? OptionalText(string? value, string field, int maximum)
    {
        if (value?.Length > maximum) throw new ValidationException($"{field}不能超过{maximum}个字符");
        return value;
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static bool IsActive(string status) => status is "Released" or "InProgress" or "Paused";
    private static string LineStatus(IEnumerable<string> statuses)
    {
        var states = statuses.ToArray();
        return states.Contains("InProgress") ? "运行中" : states.Contains("Paused") ? "已暂停" : states.Contains("Released") ? "待开工" : "空闲";
    }
    private static decimal Percent(long value, long total) => total == 0 ? 0 : decimal.Round(100m * value / total, 2);
    private static MesProductDto ProductDto(MesProduct x) => new(x.Id, x.Code, x.Name, x.Specification, x.Unit, x.CycleTimeSeconds);
    private static MesLineDto LineDto(MesLine x, string status) => new(x.Id, x.Code, x.Name, x.Workshop, x.DeviceId, status);
    private static MesOrderDto OrderDto(MesOrder x) => new(x.Id, x.OrderNo, x.ProductId, x.Product.Name, x.LineId, x.Line.Name,
        x.PlannedQuantity, x.GoodQuantity, x.ScrapQuantity, x.Status, x.PlannedStartAt, x.DueAt, x.Priority, x.BatchNo,
        x.CreatedAt, x.StartedAt, x.CompletedAt);
    private static MesReportDto ReportDto(MesProductionReport x) => new(x.Id, x.OrderId, x.OrderNo, x.GoodQuantity,
        x.ScrapQuantity, x.DefectReason, x.Station, x.OperatorName, x.ReportedAt, x.RequestId);
    private static MesInspectionDto InspectionDto(MesInspection x) => new(x.Id, x.OrderId, x.OrderNo, x.InspectionType,
        x.SampleQuantity, x.QualifiedQuantity, x.Result, x.Remark, x.InspectorName, x.InspectedAt);
    private static MesOrderEventDto EventDto(MesOrderEvent x) => new(x.Id, x.OrderId, x.Action, x.FromStatus,
        x.ToStatus, x.OperatorName, x.Description, x.CreatedAt);
}
