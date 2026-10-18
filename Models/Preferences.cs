using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace HTV11.Models;

[Table("user_preferences")]
public class Preferences : BaseModel
{
	[PrimaryKey("user_id")]
	public Guid Id { get; set; }

	[Column("interests")]
	public List<string> Interests { get; set; } = [];

	[Column("age_group")]
	public string? AgeGroup { get; set; }

	[Column("accessibility_needs")]
	public List<string> AccessibilityNeeds { get; set; } = [];

	[Column("travel_mode")]
	public string? TravelMode { get; set; }

	[Column("budget_preference")]
	public string? BudgetPreference { get; set; }

	[Column("visit_duration")]
	public string? VisitDuration { get; set; }

	[Column("visit_date_start")]
	public DateTime? VisitDateStart { get; set; }

	[Column("visit_date_end")]
	public DateTime? VisitDateEnd { get; set; }

	[Column("setting_preference")]
	public string? SettingPreference { get; set; }

	[Column("activity_pace")]
	public string? ActivityPace { get; set; }
}
