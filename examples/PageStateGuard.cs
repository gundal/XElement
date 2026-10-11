using XElementArchitecture;

namespace XElementArchitecture.Examples;

/// <summary>
/// Employer-neutral illustration of the historical HLI idea: element existence
/// alone is not proof that the application is on the expected screen.
/// </summary>
public sealed class PageStateGuard
{
    private readonly UIWrapper _ui;

    public PageStateGuard(UIWrapper ui) => _ui = ui;

    public void RequireScreen(string uniqueScreenLocator, string expectedScreen)
    {
        XElement marker = _ui.FE(uniqueScreenLocator);
        if (!marker.Ready)
            throw new XElementFrameworkException(
                $"Application state precondition failed. Expected screen: {expectedScreen}");
    }
}
