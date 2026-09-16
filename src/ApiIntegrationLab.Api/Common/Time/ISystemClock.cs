namespace ApiIntegrationLab.Api.Common.Time;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
