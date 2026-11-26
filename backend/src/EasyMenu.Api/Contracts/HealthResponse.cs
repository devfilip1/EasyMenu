namespace EasyMenu.Api.Contracts;

public sealed record HealthResponse(HealthStatus Status);

public enum HealthStatus
{
    Healthy,
    Unhealthy,
}
