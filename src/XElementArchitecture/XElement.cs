using System.Drawing;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using XElementArchitecture.CoreFunctionality;

namespace XElementArchitecture;

/// <summary>
/// Stateful abstraction over Selenium IWebElement. Locator/Path/Index describe the
/// logical element; E is only its current replaceable DOM representation.
///
/// Public-safe reconstruction preserving the historical XElement API and behavior
/// while removing employer-specific application data.
/// </summary>
public class XElement
{
    private string _state = "STALE";
    private string _origStyle = string.Empty;
    private By _path = Global.DefaultElementBy;

    public IWebDriver Driver { get; set; }
    public IWebElement? E { get; set; }
    public string Locator { get; set; }
    public Async? Concurrency { get; set; }
    public Dictionary<string, string>? JSProperties { get; set; }
    public Dictionary<string, string> Modifiers { get; set; } = new();
    public IList<IWebElement> IEs { get; set; } = new List<IWebElement>();
    public int Index { get; set; } = -1;

    public By Path
    {
        get => _path;
        set
        {
            _path = value;
            _state = IsDefaultPath(value) ? "STALE" : "CURRENT";
        }
    }

    public string XPath => ConvertCsstoXpath(Locator);
    public string Css => ConvertXPathToCss(Locator);

    public XElement(IWebElement element, By locator)
    {
        Driver = RequireDriver();
        E = element;
        Locator = SimplifyLocator(VerifyPath(locator));
        Path = locator;
        _origStyle = SafeAttribute(element, "style") ?? string.Empty;
        IEs = All();
        Index = GetIndex();
    }

    public XElement(IWebElement element, By path, string locator)
    {
        Driver = RequireDriver();
        E = element;
        Locator = locator.Contains("//", StringComparison.Ordinal)
            ? locator
            : SimplifyLocator(locator);
        Path = path;
        _origStyle = SafeAttribute(element, "style") ?? string.Empty;
    }

    /// <summary>
    /// Known logical element that is currently unresolved. The locator survives;
    /// the current DOM reference is the framework sentinel until reacquisition.
    /// </summary>
    public XElement(string locator, IWebDriver? driver = null)
    {
        Driver = driver ?? RequireDriver();
        Locator = locator;
        Path = Global.DefaultElementBy;
        E = SafeFindDefault();
    }

    public XElement()
    {
        Driver = RequireDriver();
        Locator = string.Empty;
        Path = Global.DefaultElementBy;
        E = SafeFindDefault();
    }

    public XElement(Dictionary<string, string> dataFors, Dictionary<string, string> modifiers, string locator)
    {
        Driver = RequireDriver();
        Locator = locator;
        Modifiers = modifiers ?? new Dictionary<string, string>();
        Path = VerifyPath(locator);
        JSProperties = new Dictionary<string, string>();
        foreach ((string key, string attribute) in dataFors)
            JSProperties[key] = DataFor(attribute, Index < 0 ? 0 : Index);
    }

    public IList<XElement> BuildList()
    {
        IList<XElement> elements = new List<XElement>();
        foreach (IWebElement element in Driver.FindElements(Path))
            elements.Add(new XElement(element, Path));
        return elements;
    }

    public int GetIndex()
    {
        if (E is null) return -1;

        for (int index = 0; index < IEs.Count; index++)
        {
            try
            {
                if (E.Location == IEs[index].Location &&
                    E.Size == IEs[index].Size &&
                    E.Equals(IEs[index]))
                    return index;
            }
            catch (StaleElementReferenceException)
            {
                return -1;
            }
        }
        return -1;
    }

    public bool Stale
    {
        get
        {
            if (_state == "CURRENT")
            {
                try
                {
                    _ = E?.Enabled;
                    return false;
                }
                catch (StaleElementReferenceException)
                {
                    _state = "STALE";
                }
            }

            By resolved = VerifyPath(Locator);
            if (IsDefaultPath(resolved))
            {
                _state = "STALE";
                return true;
            }

            Path = resolved;
            E = VerifyElement(resolved, Index < 0 ? 0 : Index);
            if (E is null || IsDefaultElement(E))
            {
                _state = "STALE";
                return true;
            }

            _state = "CURRENT";
            return false;
        }
    }

    /// <summary>
    /// Historical Current semantics: if stale, repeatedly resolve the stored
    /// locator and replace E without changing the logical XElement instance.
    /// </summary>
    public bool Current
    {
        get
        {
            if (!Stale) return true;

            int attempt = 0;
            while (attempt++ < Attempts)
            {
                Path = VerifyPath(Locator);
                if (!IsDefaultPath(Path))
                {
                    E = VerifyElement(Path, Index < 0 ? 0 : Index);
                    if (E is not null)
                    {
                        _state = "CURRENT";
                        return true;
                    }
                }
                Thread.Sleep(25);
            }
            return false;
        }
    }

    public bool Ready => Current && Displayed && Visible;

    public bool Visible
    {
        get
        {
            try { return E is not null && !Size.IsEmpty && !Location.IsEmpty; }
            catch (StaleElementReferenceException) { _state = "STALE"; return false; }
        }
    }

    public bool Enabled
    {
        get
        {
            try { return E?.Enabled ?? false; }
            catch (StaleElementReferenceException) { _state = "STALE"; return false; }
        }
    }

    public bool Displayed
    {
        get
        {
            try { return E?.Displayed ?? false; }
            catch (StaleElementReferenceException) { _state = "STALE"; return false; }
        }
    }

    public Point Location => E?.Location ?? Point.Empty;
    public bool Selected => E?.Selected ?? false;
    public Size Size => E?.Size ?? Size.Empty;
    public string Value => GetAttribute("value") ?? string.Empty;
    public string TagName => E?.TagName ?? string.Empty;
    public string Text => E?.Text ?? string.Empty;

    public string Style
    {
        get => GetAttribute("style") ?? string.Empty;
        set => SetAttribute("style", value ?? _origStyle);
    }

    public IList<IWebElement> All()
    {
        IList<IWebElement> matches = Driver.FindElements(Path);
        return matches.Where(element =>
        {
            try { return !string.Equals(element.TagName, "script", StringComparison.OrdinalIgnoreCase); }
            catch (StaleElementReferenceException) { return false; }
        }).ToList();
    }

    /// <summary>Instantly returns the requested match or null; does not throw for absence.</summary>
    public IWebElement? VerifyElement(By by, int index = 0)
    {
        IReadOnlyCollection<IWebElement> found = Driver.FindElements(by);
        if (index < 0 || found.Count <= index) return null;
        return found.ElementAt(index);
    }

    /// <summary>
    /// Verify the current E reference; if it is stale, reacquire using retained
    /// locator identity and occurrence index.
    /// </summary>
    public IWebElement? VerifyElement()
    {
        if (E is not null && !IsDefaultElement(E))
        {
            try
            {
                _ = E.Displayed;
                return E;
            }
            catch (StaleElementReferenceException)
            {
                _state = "STALE";
            }
        }

        By path = VerifyPath(Locator);
        if (!IsDefaultPath(path))
        {
            IReadOnlyCollection<IWebElement> matches = Driver.FindElements(path);
            int target = Index < 0 ? 0 : Index;
            if (matches.Count > target)
            {
                E = matches.ElementAt(target);
                Path = path;
                _state = "CURRENT";
                return E;
            }
        }

        E = SafeFindDefault();
        _state = "STALE";
        return E;
    }

    public string ConvertXPathToCss(string xpath)
    {
        if (!xpath.Contains("]/", StringComparison.Ordinal))
            return SimplifyLocator(xpath.Replace("//", string.Empty).Replace("@", string.Empty));
        return xpath;
    }

    public string ConvertCsstoXpath(string css)
    {
        if (css.Contains("By.", StringComparison.Ordinal))
            css = SimplifyLocator(css);
        if (css.Contains("//", StringComparison.Ordinal))
            return css;
        return Css2xpath.Transform(css);
    }

    /// <summary>
    /// Convert parent locator to XPath when required, apply parent occurrence,
    /// append a relative child locator, and verify the composed path.
    /// </summary>
    public string ConcatLocators(string input, string modifier, int index = -1)
    {
        if (!input.Contains("//", StringComparison.Ordinal))
            input = ConvertCsstoXpath(input);

        if (!(modifier.StartsWith("//", StringComparison.Ordinal) ||
              modifier.StartsWith("/.", StringComparison.Ordinal) ||
              modifier.StartsWith("/", StringComparison.Ordinal)))
            modifier = "/" + modifier;

        if (index >= 0)
            input = $"({input})[{index + 1}]";

        string combined = string.Concat(input, modifier).Replace('"', '\'');
        By verified = VerifyPath(combined);
        if (IsDefaultPath(verified))
            throw new XElementFrameworkException(
                $"Element '{input}' with modifier '{modifier}' is not currently valid. Composed locator: {combined}");
        return VerifyPath(verified);
    }

    /// <summary>
    /// Generic Knockout view-model inspection preserved from the historical
    /// architecture. Application-specific object names/schemas are intentionally absent.
    /// </summary>
    public string DataFor(string attribute, int index = 0)
    {
        if (Stale || E is null) return string.Empty;
        if (Driver is not IJavaScriptExecutor js) return string.Empty;

        const string script = @"
            if (typeof ko === 'undefined' || !ko.dataFor) return '';
            const model = ko.dataFor(arguments[0]);
            if (!model) return '';
            const value = ko.unwrap ? ko.unwrap(model[arguments[1]]) : model[arguments[1]];
            if (value === undefined || value === null) return '';
            return String(value);";

        try
        {
            return js.ExecuteScript(script, E, attribute)?.ToString() ?? string.Empty;
        }
        catch (WebDriverException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// The real FindElement: descendant lookup starts from this logical element's
    /// retained locator/index rather than from a disposable current IWebElement.
    /// </summary>
    public XElement FindElement(string locator, int index = -1)
    {
        string newLocator = ConcatLocators(Locator, locator, Index);
        if (index >= 0)
            newLocator = $"({newLocator})[{index + 1}]";

        By path = VerifyPath(newLocator);
        IWebElement? element = VerifyElement(path);
        return element is null
            ? new XElement(newLocator, Driver)
            : new XElement(element, path, newLocator);
    }

    public XElement? FindIndexByAttribute(IList<XElement> elements, string attribute, string value = "")
    {
        foreach (XElement element in elements)
        {
            if (element.DataFor(attribute) == value)
                return element;
        }
        return null;
    }

    /// <summary>
    /// Normalize Selenium By.ToString() output and historical quote/bracket noise
    /// back to the locator text retained as logical identity.
    /// </summary>
    public string SimplifyLocator(string locator)
    {
        locator = locator.Trim();
        string[] prefixes =
        {
            "By.CssSelector: ", "By.XPath: ", "By.Id: ", "By.Name: ",
            "By.ClassName: ", "By.TagName: ", "By.LinkText: "
        };

        foreach (string prefix in prefixes)
            if (locator.StartsWith(prefix, StringComparison.Ordinal))
                return locator[prefix.Length..].Trim();

        return locator;
    }

    /// <summary>
    /// Retains the historical helper used to inspect pieces of an XPath locator.
    /// The public version is generic rather than tied to an employer-specific
    /// test-attribute name.
    /// </summary>
    public string ReduceLocator(string locator, int section = 0)
    {
        string xpath = locator.Contains("//", StringComparison.Ordinal)
            ? locator
            : ConvertCsstoXpath(locator);

        Match attribute = Regex.Match(xpath, """@(?<name>[\w:-]+)\s*=\s*['"](?<value>[^'"]+)['"]""");
        if (!attribute.Success)
            return section == 0 ? xpath : string.Empty;

        string baseLocator = $"//*[@{attribute.Groups["name"].Value}='{attribute.Groups["value"].Value}']";
        Match index = Regex.Match(xpath, @"\[(?<index>\d+)\]\s*$");
        string tail = xpath[(attribute.Index + attribute.Length)..].TrimEnd(')', ']');

        string[] parts =
        {
            baseLocator,
            attribute.Groups["value"].Value,
            tail,
            index.Success ? index.Value : string.Empty
        };
        return section >= 0 && section < parts.Length ? parts[section] : string.Empty;
    }

    public string VerifyPath(By path) => SimplifyLocator(path.ToString());

    /// <summary>
    /// Smart-selector resolution. Accepts raw IDs/names/CSS/XPath and serialized
    /// Selenium By values; returns the sentinel locator when nothing currently resolves.
    /// </summary>
    public By VerifyPath(string locator)
    {
        if (string.IsNullOrWhiteSpace(locator)) return Global.DefaultElementBy;

        string candidate = locator.Trim().Trim('{', '}');
        By? serialized = ParseSerializedBy(candidate);
        if (serialized is not null && HasAny(serialized)) return serialized;

        string simplified = SimplifyLocator(candidate);

        bool looksLikeXPath = simplified.StartsWith("//", StringComparison.Ordinal) ||
                              simplified.StartsWith("(//", StringComparison.Ordinal);
        if (looksLikeXPath && HasAny(By.XPath(simplified)))
            return By.XPath(simplified);

        By id = By.Id(simplified);
        if (HasAny(id)) return id;

        By name = By.Name(simplified);
        if (HasAny(name)) return name;

        try
        {
            By css = By.CssSelector(simplified);
            if (HasAny(css)) return css;
        }
        catch (InvalidSelectorException)
        {
            // Continue to XPath conversion.
        }

        try
        {
            string xpath = ConvertCsstoXpath(simplified);
            By converted = By.XPath(xpath);
            if (HasAny(converted)) return converted;
        }
        catch (Exception) when (!Global.OPTIMIZE)
        {
            // Historical normal path tolerated unresolved elements and represented
            // them using the sentinel rather than forcing every caller to catch.
        }

        return Global.DefaultElementBy;
    }

    public void SetAttribute(string attributeName, string value)
    {
        if (!Ready || E is null)
            throw new XElementFrameworkException($"Element '{Locator}' is not ready.");
        if (Driver is not IJavaScriptExecutor js)
            throw new XElementFrameworkException("Driver does not support JavaScript execution.");
        js.ExecuteScript("arguments[0].setAttribute(arguments[1], arguments[2]);", E, attributeName, value);
    }

    public void ShowMe(bool highlight = true)
    {
        if (!Global.HIGHLIGHT) return;
        SetAttribute("style", highlight
            ? "border: 3px solid yellow; color: yellow; font-weight: bold;"
            : _origStyle);
    }

    public void Click()
    {
        int attempt = 0;
        while (!Ready && attempt++ < Attempts)
        {
            if (Global.OPTIMIZE)
                throw new XElementOptimizationException(
                    $"PLEASE OPTIMIZE THIS TESTCASE -- Click called before '{Locator}' was Ready.");
            Thread.Sleep(25);
        }

        if (E is null)
            throw new XElementFrameworkException($"Unable to resolve '{Locator}' for Click().");

        ShowMe();
        try
        {
            E.Click();
        }
        catch (ElementClickInterceptedException)
        {
            new Actions(Driver).MoveToElement(E).Click().Perform();
        }
        catch (ElementNotInteractableException)
        {
            // Preserve the historical diagnostic fallback, but make it visible.
            Console.WriteLine($"XElement diagnostic: normal click failed for '{Locator}', using JavaScript fallback.");
            if (Driver is not IJavaScriptExecutor js)
                throw;
            js.ExecuteScript("arguments[0].click();", E);
        }
        finally
        {
            Global.FinishLoadingApp();
        }
    }

    public void SendKeys(string value, bool clear = false)
    {
        if (!Current || E is null)
            throw new XElementFrameworkException($"Attempted SendKeys on unresolved element '{Locator}'.");
        if (clear) E.Clear();
        E.SendKeys(value);
    }

    public void Submit()
    {
        if (!Ready || E is null)
            throw new XElementFrameworkException($"Element '{Locator}' is not ready for Submit().");
        E.Submit();
    }

    public void Clear()
    {
        if (!Current || E is null)
            throw new XElementFrameworkException($"Element '{Locator}' is not current for Clear().");
        E.Clear();
    }

    public string? GetAttribute(string attributeName)
    {
        VerifyElement();
        return E is null || IsDefaultElement(E) ? null : E.GetAttribute(attributeName);
    }

    public string? GetCssValue(string propertyName)
    {
        VerifyElement();
        return E is null || IsDefaultElement(E) ? null : E.GetCssValue(propertyName);
    }

    private int Attempts => Settings.SettingsManager.Config.TrickyElementAttempts > 0
        ? Settings.SettingsManager.Config.TrickyElementAttempts
        : Global.NumberOfAttemptsForTrickyElements;

    private bool HasAny(By by)
    {
        TimeSpan previous = Driver.Manage().Timeouts().ImplicitWait;
        try
        {
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(1);
            return Driver.FindElements(by).Count > 0;
        }
        catch (WebDriverException)
        {
            return false;
        }
        finally
        {
            Driver.Manage().Timeouts().ImplicitWait = previous;
        }
    }

    private By? ParseSerializedBy(string text)
    {
        if (!text.StartsWith("By.", StringComparison.Ordinal)) return null;
        int colon = text.IndexOf(':');
        if (colon < 0) return null;

        string type = text[3..colon].Trim();
        string value = text[(colon + 1)..].Trim();
        return type switch
        {
            "Id" => By.Id(value),
            "Name" => By.Name(value),
            "XPath" => By.XPath(value),
            "CssSelector" => By.CssSelector(value),
            "ClassName" => By.ClassName(value),
            "TagName" => By.TagName(value),
            "LinkText" => By.LinkText(value),
            _ => null
        };
    }

    private bool IsDefaultPath(By by) => by.ToString() == Global.DefaultElementBy.ToString();

    private bool IsDefaultElement(IWebElement element)
    {
        IWebElement? sentinel = SafeFindDefault();
        return sentinel is not null && element.Equals(sentinel);
    }

    private IWebElement? SafeFindDefault()
    {
        try { return Driver.FindElements(Global.DefaultElementBy).FirstOrDefault(); }
        catch (WebDriverException) { return null; }
    }

    private static string? SafeAttribute(IWebElement element, string name)
    {
        try { return element.GetAttribute(name); }
        catch (WebDriverException) { return null; }
    }

    private static IWebDriver RequireDriver() =>
        Global.Global_objWebDriver ?? throw new InvalidOperationException(
            "Global.Setup(driver) must be called, or a driver must be supplied to the XElement constructor.");
}
