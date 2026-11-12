using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

// Stored exports are separate from AiReport's generated narrative and model metadata.
[Table("report_artifacts")]
public class ReportArtifact
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("creator_id")]
    public long CreatorId { get; set; }

    [Required, MaxLength(20)]
    [Column("report_type")]
    public string ReportType { get; set; } = string.Empty;

    [Column("range_start")]
    public DateTime RangeStart { get; set; }

    [Column("range_end")]
    public DateTime RangeEnd { get; set; }

    [Column("generated_at")]
    public DateTime GeneratedAt { get; set; }

    [Required, MaxLength(160)]
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    [Column("content_type")]
    public string ContentType { get; set; } = "text/csv; charset=utf-8";

    [Column("file_size")]
    public long FileSize { get; set; }

    [Required]
    [Column("content", TypeName = "longblob")]
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
