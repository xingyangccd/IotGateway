using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace IotGateway.Application.DTOs;

public sealed record BasicSettingsDto(string SystemName, int DataRetentionDays, int AlarmCooldown);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateBasicSettingsDto
{
    [JsonRequired]
    [Required]
    [StringLength(100)]
    public string SystemName { get; set; } = string.Empty;

    [JsonRequired]
    [Range(7, 365)]
    public int DataRetentionDays { get; set; }

    [JsonRequired]
    [Range(10, 3600)]
    public int AlarmCooldown { get; set; }
}
