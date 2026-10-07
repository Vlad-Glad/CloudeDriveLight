namespace GDriveLight.Desktop.Models.Api;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string FirstName);
public record LoginResponse(string Token);
