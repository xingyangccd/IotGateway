namespace IotGateway.WebAPI.DTOs;

public sealed class MaintenancePlanRequest
{
    public long DeviceId { get; set; }
    public string? PlanName { get; set; }
    public string? CycleType { get; set; }
    public int CycleValue { get; set; }
    public string? Items { get; set; }
    public int EstimatedDuration { get; set; } = 60;
    public string? RequiredSpareParts { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class MaintenanceEnableRequest
{
    public bool? IsEnabled { get; set; }
}

public sealed class MaintenanceRecordRequest
{
    public long PlanId { get; set; }
    public long DeviceId { get; set; }
    public string? MaintenanceType { get; set; } = "Planned";
    public string? ItemsCompleted { get; set; }
    public string? SparePartsUsed { get; set; }
    public string? Result { get; set; } = "Completed";
    public decimal Cost { get; set; }
    public string? Remark { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
}

public sealed class RepairOrderRequest
{
    public long DeviceId { get; set; }
    public string? FaultPhenomenon { get; set; }
    public string? Severity { get; set; } = "Normal";
    public string? Remark { get; set; }
}

public sealed class RepairAssignmentRequest
{
    public long AssignedTo { get; set; }
}

public sealed class RepairCompletionRequest
{
    public string? FaultCause { get; set; }
    public string? RepairAction { get; set; }
    public decimal RepairCost { get; set; }
    public int DowntimeMinutes { get; set; }
    public string? SparePartsUsed { get; set; }
    public string? Remark { get; set; }
}

public sealed record MaintenancePage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
