using OpenQA.Selenium;

namespace XElementArchitecture;

/// <summary>
/// Preserves the historical per-execution driver holder used when the framework
/// was running more than one browser context concurrently.
/// </summary>
public sealed class Async
{
    public IWebDriver? Global_objWebDriver { get; set; }
}
