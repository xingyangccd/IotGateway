using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

[Table("knowledge_documents")]
public class KnowledgeDocument
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("parent_id")]
    public long? ParentId { get; set; }

    [Required, MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    [Column("kind")]
    public string Kind { get; set; } = "document";

    [Required, Column("content", TypeName = "longtext")]
    public string Content { get; set; } = string.Empty;

    [Column("created_by")]
    public long CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(32), ConcurrencyCheck]
    [Column("version")]
    public string Version { get; set; } = Guid.NewGuid().ToString("N");
}
