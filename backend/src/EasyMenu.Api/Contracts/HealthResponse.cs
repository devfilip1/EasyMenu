namespace EasyMenu.Api.Contracts;

public sealed record HealthResponse(string Status)
{
    public const string Healthy = "healthy";
    public const string Unhealthy = "unhealthy";
}
