namespace IotGateway.Domain.Entities;

public sealed class BasicSettings
{
    public int Id { get; set; } = 1;
    public string SystemName { get; set; } = "IoT 设备管理系统";
    public int DataRetentionDays { get; set; } = 90;
    public int AlarmCooldown { get; set; } = 60;
}
