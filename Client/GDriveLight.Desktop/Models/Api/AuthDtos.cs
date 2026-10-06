namespace GDriveLight.Desktop.Models.Api;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token);
