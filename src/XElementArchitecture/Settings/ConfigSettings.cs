namespace XElementArchitecture.Settings;

/// <summary>
/// Employer-neutral public configuration surface preserving the historical
/// framework switches that materially affected execution behavior.
/// </summary>
public sealed class ConfigSettings
{
    public bool Highlight { get; init; }
    public bool Optimize { get; init; }
    public bool Continuous { get; init; }
    public int ImplicitWaitMilliseconds { get; init; } = 300;
    public int TrickyElementAttempts { get; init; } = 15;
}
