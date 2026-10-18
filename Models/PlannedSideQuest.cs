using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace HTV11.Models;

[Table("planned_side_quests")]
public class PlannedSideQuest : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("planned_place_name")]
    public string PlannedPlaceName { get; set; } = string.Empty;

    [Column("area")]
    public string Area { get; set; } = string.Empty;

    [Column("activity")]
    public string Activity { get; set; } = string.Empty;

    [Column("creator_prompt")]
    public string CreatorPrompt { get; set; } = string.Empty;

    [Column("tiktok_search_terms")]
    public string TikTokSearchTerms { get; set; } = string.Empty;

    [Column("duration_minutes")]
    public int DurationMinutes { get; set; }
}
