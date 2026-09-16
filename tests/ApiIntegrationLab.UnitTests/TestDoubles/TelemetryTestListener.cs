using System.Collections.Concurrent;
using System.Diagnostics;

namespace ApiIntegrationLab.UnitTests.TestDoubles;

internal sealed class TelemetryTestListener : IDisposable
{
    private readonly ConcurrentBag<Activity> _activities = [];
    private readonly ActivityListener _listener;

    private TelemetryTestListener(string sourceName)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _activities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public string SerializedActivities => string.Join(
        "\n",
        _activities
            .OrderBy(activity => activity.DisplayName, StringComparer.Ordinal)
            .Select(activity => $"{activity.DisplayName}|{string.Join(",", activity.TagObjects
                .OrderBy(tag => tag.Key, StringComparer.Ordinal)
                .Select(tag => $"{tag.Key}={tag.Value}"))}"));

    public static TelemetryTestListener ListenTo(string sourceName) => new(sourceName);

    public void Dispose() => _listener.Dispose();
}
