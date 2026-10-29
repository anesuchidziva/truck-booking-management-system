using Microsoft.AspNetCore.Identity;

public sealed class ApplicationUser : IdentityUser<int>
{
    public string DisplayName { get; set; } = "";
    public string? FirebaseUid { get; set; }
    public bool IsDisabled { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
