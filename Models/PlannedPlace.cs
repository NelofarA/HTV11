using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace HTV11.Models;

[Table("planned_places")]
public class PlannedPlace : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("neighborhood")]
    public string Neighborhood { get; set; } = string.Empty;

    [Column("summary")]
    public string Summary { get; set; } = string.Empty;

    [Column("why_recommended")]
    public string WhyRecommended { get; set; } = string.Empty;

    [Column("interests")]
    public List<string> Interests { get; set; } = [];

    [Column("accessibility_notes")]
    public string AccessibilityNotes { get; set; } = string.Empty;

    [Column("age_notes")]
    public string AgeNotes { get; set; } = string.Empty;

    [Column("transit_notes")]
    public string TransitNotes { get; set; } = string.Empty;

    [Column("potential_drawbacks")]
    public string PotentialDrawbacks { get; set; } = string.Empty;

    [Column("visitor_tips")]
    public string VisitorTips { get; set; } = string.Empty;
}
