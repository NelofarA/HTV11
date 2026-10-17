using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace HTV11.Models;

[Table("users")]
public class User : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("email")]
    public string Email { get; set; } = "";

    [Column("onboarding_completed")]
    public bool OnboardingCompleted { get; set; }
}