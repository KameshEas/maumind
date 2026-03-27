namespace MauMind.App.Models;

public class AuthState
{
    public bool IsAuthenticated { get; set; }
    public string? Email { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
