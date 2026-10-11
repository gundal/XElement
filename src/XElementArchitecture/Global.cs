using OpenQA.Selenium;
using XElementArchitecture.Settings;

namespace XElementArchitecture;

/// <summary>
/// Sanitized reconstruction of the framework runtime. It intentionally preserves
/// the historical responsibilities: driver/session ownership, fail-closed test
/// status, synchronization policy, OPTIMIZE/HIGHLIGHT switches and optional
/// state-preserving continuous execution.
/// </summary>
public static class Global
{
    public const string DEFAULT_ELEMENT = "body";
    public const int IMPLICIT_WAIT = 300;
    public const int NumberOfAttemptsForTrickyElements = 15;

    private static readonly Dictionary<string, Action<UIWrapper>> RegisteredTests =
        new(StringComparer.OrdinalIgnoreCase);

    public static Async? Concurrency { get; set; }
    public static bool THREADING { get; set; }
    public static string TEST_STATUS { get; set; } = "FAILED";
    public static IWebDriver? Global_objWebDriver { get; set; }
    public static UIWrapper? Apm { get; set; }

    public static bool HIGHLIGHT => SettingsManager.Config.Highlight;
    public static bool OPTIMIZE => SettingsManager.Config.Optimize;
    public static bool CONTINUOUS => SettingsManager.Config.Continuous;

    public static By DefaultElementBy => By.CssSelector(DEFAULT_ELEMENT);

    public static UIWrapper Setup(IWebDriver driver, ConfigSettings? settings = null)
    {
        SettingsManager.Initialize(settings);
        Global_objWebDriver = driver ?? throw new ArgumentNullException(nameof(driver));
        Concurrency = new Async { Global_objWebDriver = driver };

        driver.Manage().Timeouts().ImplicitWait =
            TimeSpan.FromMilliseconds(SettingsManager.Config.ImplicitWaitMilliseconds);

        Apm = new UIWrapper(driver);
        TEST_STATUS = "FAILED";
        return Apm;
    }

    public static void RegisterTestCase(string name, Action<UIWrapper> testCase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(testCase);
        RegisteredTests[name] = testCase;
    }

    public static void NextTC(UIWrapper apm)
    {
        if (string.IsNullOrWhiteSpace(apm.NextTestCase))
            return;

        if (!RegisteredTests.TryGetValue(apm.NextTestCase, out Action<UIWrapper>? next))
            throw new XElementFrameworkException(
                $"Continuous execution requested unknown test case '{apm.NextTestCase}'.");

        next(apm);
    }

    public static void TearDown(UIWrapper apm)
    {
        try
        {
            bool passed = TEST_STATUS.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase);
            bool hasNext = !string.IsNullOrWhiteSpace(apm.NextTestCase) &&
                           !apm.NextTestCase.Contains("FIN", StringComparison.OrdinalIgnoreCase);

            if (CONTINUOUS && passed && hasNext)
            {
                NextTC(apm);
                return;
            }

            if (!passed)
                throw new Error(apm);
        }
        finally
        {
            if (!THREADING)
                Global_objWebDriver?.Quit();
        }
    }

    public static void FinishLoadingApp()
    {
        // Historical seam: application-specific readiness logic lived here.
        // Public reconstruction deliberately leaves the hook generic.
    }

    public static void MarkSuccess() => TEST_STATUS = "SUCCESS";
}
