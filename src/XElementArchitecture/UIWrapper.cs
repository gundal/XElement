using System.Drawing;
using OpenQA.Selenium;

namespace XElementArchitecture;

/// <summary>
/// Selenium-facing framework facade. Preserves the historical authoring surface:
/// smart string locators, FE/FEs fast lookup, MFE (Must Find Element), generic
/// FindElement, model inspection and centralized driver access.
/// </summary>
public class UIWrapper
{
    public UIWrapper(IWebDriver driver)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        Global.Global_objWebDriver ??= driver;
        Global.Concurrency ??= new Async { Global_objWebDriver = driver };
        Global.TEST_STATUS = "FAILED"; // fail closed unless a test explicitly passes
    }

    public string? NextTestCase { get; set; }
    public IWebDriver Driver { get; set; }
    public Async? Concurrency { get; set; }
    public string ErrorMsg { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IJavaScriptExecutor? Js => Driver as IJavaScriptExecutor;

    public string GetText() => Driver.FindElement(By.TagName("body")).Text;

    public void SetAttribute(IWebElement element, string attributeName, string value)
    {
        if (Driver is not IJavaScriptExecutor js)
            throw new XElementFrameworkException("Driver does not support JavaScript execution.");
        js.ExecuteScript("arguments[0].setAttribute(arguments[1], arguments[2]);", element, attributeName, value);
    }

    public XElement? FindElement(By by, Dictionary<string, string>? properties = null)
    {
        IWebElement? element = VerifyElement(by);
        if (element is not null)
            return new XElement(element, by);

        By? path = VerifyPath(by.ToString());
        if (path is null) return null;
        element = VerifyElement(path);
        return element is null ? null : new XElement(element, path);
    }

    public XElement? FindElement(string locator)
    {
        By? path = VerifyPath(locator);
        if (path is null) return null;
        IWebElement? element = VerifyElement(path);
        return element is null ? null : new XElement(element, path, locator);
    }

    /// <summary>
    /// FE: fast/flexible lookup. Prefer quick existence checks over exception-heavy
    /// FindElement calls. If absent, return a logical unresolved XElement retaining
    /// the requested locator rather than forcing absence handling into the test.
    /// </summary>
    public XElement FE(string locator)
    {
        TimeSpan previous = Driver.Manage().Timeouts().ImplicitWait;
        try
        {
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(1);

            By? path = VerifyPath(locator);
            if (path is null)
                return new XElement(locator, Driver);

            IWebElement? element = VerifyElement(path);
            return element is null
                ? new XElement(locator, Driver)
                : new XElement(element, path, locator);
        }
        catch (Exception ex) when (ex is WebDriverException or InvalidOperationException)
        {
            ErrorMsg = "Element search error: " + ex.Message;
            return new XElement(locator, Driver);
        }
        finally
        {
            Driver.Manage().Timeouts().ImplicitWait = previous;
        }
    }

    public XElement FE(By by)
    {
        IWebElement? element = VerifyElement(by);
        return element is null
            ? new XElement(by.ToString(), Driver)
            : new XElement(element, by, by.ToString());
    }

    /// <summary>
    /// MFE — Must Find Element. Expensive/stability-aware path for elements that
    /// must exist at this point in the workflow. It is intentionally not the default.
    /// </summary>
    public XElement MFE(string locator)
    {
        int attempts = Math.Max(1, Settings.SettingsManager.Config.TrickyElementAttempts);
        By? path = null;

        for (int tick = 0; tick < attempts && path is null; tick++)
            path = VerifyPath(locator);

        if (path is null)
            throw new XElementFrameworkException($"MFE could not resolve locator '{locator}'.");

        XElement? candidate = null;
        Point zero = Point.Empty;

        for (int tick = 0; tick < attempts; tick++)
        {
            candidate = FE(path);
            if (candidate.E is not null &&
                !candidate.Location.Equals(zero) &&
                !candidate.Size.IsEmpty)
                break;
            Thread.Sleep(15);
        }

        if (candidate?.E is null)
            throw new XElementFrameworkException($"MFE found no rendered element for '{locator}'.");

        Point previousLocation = candidate.Location;
        Size previousSize = candidate.Size;

        for (int tick = 0; tick < attempts; tick++)
        {
            Thread.Sleep(15);
            XElement? next = FindElement(path);
            if (next?.E is null) continue;

            bool geometricallyPresent = !next.Location.Equals(zero) && !next.Size.IsEmpty;
            bool stable = geometricallyPresent &&
                          next.Location.Equals(previousLocation) &&
                          next.Size.Equals(previousSize);
            if (stable)
                return new XElement(next.E, path, locator);

            previousLocation = next.Location;
            previousSize = next.Size;
        }

        throw new XElementFrameworkException(
            $"MFE resolved '{locator}' but it did not become geometrically stable within {attempts} observations.");
    }

    public XElement MFE(By by) => MFE(by.ToString());

    public IList<XElement> FEs(string locator) => FE(locator).BuildList();

    public IList<XElement> FEs(By by, Dictionary<string, string>? properties = null)
    {
        IList<XElement> elements = new List<XElement>();
        foreach (IWebElement element in Driver.FindElements(by))
            elements.Add(new XElement(element, by));
        return elements;
    }

    /// <summary>Returns null instantly when no match exists.</summary>
    public IWebElement? VerifyElement(By by)
    {
        IReadOnlyCollection<IWebElement> elements = Driver.FindElements(by);
        return elements.FirstOrDefault();
    }

    /// <summary>
    /// Lightweight smart-selector check used by UIWrapper's fast path. XElement's
    /// richer VerifyPath handles composition and serialized By recovery.
    /// </summary>
    public By? VerifyPath(string locator)
    {
        if (string.IsNullOrWhiteSpace(locator)) return null;
        TimeSpan previous = Driver.Manage().Timeouts().ImplicitWait;
        try
        {
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(1);
            string s = SimplifyLocator(locator);

            if ((s.StartsWith("//", StringComparison.Ordinal) || s.StartsWith("(//", StringComparison.Ordinal)) &&
                Driver.FindElements(By.XPath(s)).Count > 0)
                return By.XPath(s);

            if (Driver.FindElements(By.Id(s)).Count > 0) return By.Id(s);
            if (Driver.FindElements(By.Name(s)).Count > 0) return By.Name(s);

            try
            {
                By css = By.CssSelector(s);
                if (Driver.FindElements(css).Count > 0) return css;
            }
            catch (InvalidSelectorException) { }

            // Serialized Selenium By values are delegated to XElement's fuller parser.
            if (locator.StartsWith("By.", StringComparison.Ordinal))
            {
                XElement resolver = new(locator, Driver);
                By resolved = resolver.VerifyPath(locator);
                return resolved.ToString() == Global.DefaultElementBy.ToString() ? null : resolved;
            }

            return null;
        }
        finally
        {
            Driver.Manage().Timeouts().ImplicitWait = previous;
        }
    }

    public string SimplifyLocator(string locator)
    {
        string[] prefixes =
        {
            "By.CssSelector: ", "By.XPath: ", "By.Id: ", "By.Name: ",
            "By.ClassName: ", "By.TagName: ", "By.LinkText: "
        };
        locator = locator.Trim().Trim('{', '}');
        foreach (string prefix in prefixes)
            if (locator.StartsWith(prefix, StringComparison.Ordinal))
                return locator[prefix.Length..].Trim();
        return locator;
    }

    public XElement? FindIndexByAttribute(string locator, string attribute, string value = "")
    {
        IList<XElement> elements = FEs(locator);
        foreach (XElement element in elements)
            if (DataFor(element, attribute) == value)
                return element;
        return null;
    }

    public string DataFor(string locator, string attribute, int index = 0)
    {
        IList<XElement> elements = FEs(locator);
        if (index < 0 || elements.Count <= index) return string.Empty;
        return DataFor(elements[index], attribute);
    }

    public string DataFor(XElement element, string attribute) => element.DataFor(attribute, element.Index);
}
