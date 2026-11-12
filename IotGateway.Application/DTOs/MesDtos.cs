namespace IotGateway.Application.DTOs;

public class MesProductRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Specification { get; set; }
    public string? Unit { get; set; }
    public decimal CycleTimeSeconds { get; set; }
}

public class MesLineRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Workshop { get; set; }
    public long? DeviceId { get; set; }
}

public class MesOrderRequest
{
    public string? OrderNo { get; set; }
    public long ProductId { get; set; }
    public long LineId { get; set; }
    public int PlannedQuantity { get; set; }
    public DateTime PlannedStartAt { get; set; }
    public DateTime DueAt { get; set; }
    public int Priority { get; set; } = 3;
    public string? BatchNo { get; set; }
}

public class MesTransitionRequest
{
    public string? Action { get; set; }
}

public class MesReportRequest
{
    public int GoodQuantity { get; set; }
    public int ScrapQuantity { get; set; }
    public string? DefectReason { get; set; }
    public string? Station { get; set; }
    public string? RequestId { get; set; }
}

public class MesInspectionRequest
{
    public long OrderId { get; set; }
    public string? InspectionType { get; set; }
    public int SampleQuantity { get; set; }
    public int QualifiedQuantity { get; set; }
    public string? Result { get; set; }
    public string? Remark { get; set; }
}

public record MesProductDto(long Id, string Code, string Name, string Specification, string Unit, decimal CycleTimeSeconds);
public record MesLineDto(long Id, string Code, string Name, string Workshop, long? DeviceId, string Status);
public record MesOrderDto(long Id, string OrderNo, long ProductId, string ProductName, long LineId, string LineName,
    int PlannedQuantity, int GoodQuantity, int ScrapQuantity, string Status, DateTime PlannedStartAt,
    DateTime DueAt, int Priority, string BatchNo, DateTime CreatedAt, DateTime? StartedAt, DateTime? CompletedAt);
public record MesReportDto(long Id, long OrderId, string OrderNo, int GoodQuantity, int ScrapQuantity,
    string? DefectReason, string? Station, string OperatorName, DateTime ReportedAt, string RequestId);
public record MesInspectionDto(long Id, long OrderId, string OrderNo, string InspectionType, int SampleQuantity,
    int QualifiedQuantity, string Result, string? Remark, string InspectorName, DateTime InspectedAt);
public record MesOrderEventDto(long Id, long OrderId, string Action, string FromStatus, string ToStatus,
    string OperatorName, string Description, DateTime CreatedAt);
public record MesPageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public record MesTraceDto(IReadOnlyList<MesOrderDto> Orders, IReadOnlyList<MesReportDto> Reports,
    IReadOnlyList<MesInspectionDto> Inspections, IReadOnlyList<MesOrderEventDto> Events);
public record MesLineDashboardDto(long Id, string Name, string Status, string? ActiveOrderNo,
    long PlannedQuantity, long GoodQuantity, long ScrapQuantity, decimal Progress);
public record MesDailyProductionDto(string Date, long GoodQuantity, long ScrapQuantity);
public record MesDashboardDto(int TotalOrders, int ActiveOrders, int CompletedOrders,
    long PlannedQuantity, long GoodQuantity, long ScrapQuantity, decimal CompletionRate, decimal QualityRate,
    IReadOnlyList<MesLineDashboardDto> Lines, IReadOnlyList<MesDailyProductionDto> DailyProduction);
