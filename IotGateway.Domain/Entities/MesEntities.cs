namespace IotGateway.Domain.Entities;

public class MesProduct
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Specification { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal CycleTimeSeconds { get; set; }
    public long Version { get; set; }
}

public class MesLine
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Workshop { get; set; } = "";
    public long? DeviceId { get; set; }
    public long Version { get; set; }
}

public class MesOrder
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = "";
    public long ProductId { get; set; }
    public MesProduct Product { get; set; } = null!;
    public long LineId { get; set; }
    public MesLine Line { get; set; } = null!;
    public int PlannedQuantity { get; set; }
    public int GoodQuantity { get; set; }
    public int ScrapQuantity { get; set; }
    public string Status { get; set; } = "Planned";
    public DateTime PlannedStartAt { get; set; }
    public DateTime DueAt { get; set; }
    public int Priority { get; set; }
    public string BatchNo { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public long Version { get; set; }
}

public class MesProductionReport
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = "";
    public int GoodQuantity { get; set; }
    public int ScrapQuantity { get; set; }
    public string? DefectReason { get; set; }
    public string? Station { get; set; }
    public string OperatorName { get; set; } = "";
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public string RequestId { get; set; } = "";
    public string PayloadHash { get; set; } = "";
}

public class MesInspection
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = "";
    public string InspectionType { get; set; } = "";
    public int SampleQuantity { get; set; }
    public int QualifiedQuantity { get; set; }
    public string Result { get; set; } = "";
    public string? Remark { get; set; }
    public string InspectorName { get; set; } = "";
    public DateTime InspectedAt { get; set; } = DateTime.UtcNow;
    public int ReportedQuantity { get; set; }
}

public class MesOrderEvent
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public MesOrder Order { get; set; } = null!;
    public string Action { get; set; } = "";
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string OperatorName { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
