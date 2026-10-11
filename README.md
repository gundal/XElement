# XElement
### Stateful Selenium element architecture for reliable UI automation

> **Source-available for portfolio review — All Rights Reserved. Not open source.**
> See [`LICENSE.md`](LICENSE.md) for permitted use.

`XElement` is a modern, employer-neutral reconstruction of an element architecture I first developed while building large Selenium end-to-end systems.

The core idea is simple:

> **The logical element should survive even when its current DOM reference does not.**

Raw Selenium gives a test an `IWebElement` that represents a DOM node that exists *now*. Dynamic applications routinely replace that node. If every test owns that lifecycle, stale-element recovery, locator resolution, synchronization and readiness logic spread throughout the suite.

XElement moves those concerns into a reusable element layer.

```text
Page / Component Object
        ↓
     UIWrapper
        ↓
      XElement
        ↓
 Selenium IWebElement
        ↓
        DOM
```

The central distinction is:

> **The locator is durable identity. The current `IWebElement` is replaceable state.**

## What XElement is

`XElement` is a **concrete stateful abstraction over Selenium's `IWebElement` model using composition**.

It is **not**:

- an `IWebElement` subclass;
- an `IWebElement` interface implementation;
- a replacement for Page Object / Component Object design;
- a wrapper that merely forwards calls.

It owns information that a raw `IWebElement` does not:

- durable logical locator identity;
- the current replaceable `IWebElement` (`E`);
- unresolved / resolved / stale lifecycle state;
- occurrence/index identity;
- scoped parent/child locator chains;
- readiness/actionability checks;
- late resolution and stale re-resolution;
- centralized interaction behavior and diagnostics.

That lets page/component objects express **application meaning** while XElement absorbs **element lifecycle mechanics**.

## Why this architecture exists

While engineering a large C#/Selenium/NUnit framework, the recurring problem was not Selenium's ability to drive a browser. It was that raw `IWebElement` left too much lifecycle and synchronization responsibility with each test author.

A test writer could end up reasoning about:

- whether the element exists yet;
- whether its DOM reference is stale;
- whether rendering is complete;
- whether it is visible/enabled/stable;
- how long to wait;
- when to locate again;
- whether a failure belongs to the test, framework, environment or product.

The design goal became:

> **Tests should describe intent. The framework should own element lifecycle and synchronization complexity.**

## Core model

```text
XElement
 ├─ LocatorChain   ← durable identity
 ├─ ElementState   ← unresolved / resolved / stale
 └─ IWebElement E  ← current replaceable DOM reference
```

A DOM refresh can invalidate `E` without invalidating the logical `XElement`.

```csharp
var save = ui.Element(By.CssSelector("[data-action='save']"));

// The application can re-render between these operations.
save.Click();
```

The test continues to express *which logical control it means* rather than managing the DOM pointer directly.

## Fast lookup vs must-exist lookup

One of the more useful ideas in the historical framework was that robustness should not make **every** lookup expensive.

### `FE` / `Element` — fast/flexible path

```csharp
var optionalBanner = ui.FE(By.Id("banner"));
```

Semantic contract:

> **This element belongs to the workflow; it may or may not physically exist at this exact instant.**

The logical XElement can remain unresolved and be resolved later.

### `MFE` / `MustElement` — required-now path

```csharp
var submit = ui.MFE(By.CssSelector("button[type='submit']"));
```

Semantic contract:

> **At this point in the workflow, this element must exist and be ready.**

The reconstruction waits for actionability and requires stable geometry across consecutive polling observations.

This preserves the original design principle:

> **Use the cheapest reliable lookup/synchronization path first; escalate only when the UI actually requires it.**

## Scoped locator identity

A chained lookup should retain its logical context instead of becoming another disposable DOM reference.

```csharp
var userRow = ui.Element(By.CssSelector("[data-user-id='42']"));
var edit = userRow.Find(By.CssSelector("[data-action='edit']"));

edit.Click();
```

`edit` retains the full locator chain. If the row or button is replaced, the chain can be resolved again from the driver.

## Readiness-driven interaction

The goal was never "add a lot of waits."

The goal was:

```text
ready?
  ├─ yes → execute immediately
  └─ no  → poll until ready → execute
```

The reconstruction uses explicit `WebDriverWait` polling and typed state rather than fixed `Thread.Sleep()` delays.

For required interactions, an element is expected to be:

- resolved;
- displayed;
- enabled;
- rendered with non-zero area.

The stronger `MFE` path also looks for stable geometry between observations.

## XElement + Page Objects

XElement is **lower-level than POM**, not a competing pattern.

```csharp
public sealed class LoginPage
{
    private readonly UIWrapper _ui;

    public LoginPage(IWebDriver driver) => _ui = new UIWrapper(driver);

    private XElement Username => _ui.Element(By.Id("username"));
    private XElement Password => _ui.Element(By.Id("password"));
    private XElement Submit => _ui.MustElement(By.CssSelector("button[type='submit']"));

    public void Login(string username, string password)
    {
        Username.SendKeys(username);
        Password.SendKeys(password);
        Submit.Click();
    }
}
```

The Page Object says **what the application does**.

XElement handles **how the logical control survives and becomes interactable**.

## Failure localization

A second recurring problem in large E2E suites is a test continuing after navigation or application state has already gone wrong.

The historical framework also used an application-state guard so an otherwise-valid selector could not silently execute on the wrong screen when similar controls existed in multiple places.

The public repo keeps that as a general architectural lesson rather than employer-specific implementation. See [`examples/PageStateGuard.cs`](examples/PageStateGuard.cs).

> **Element existence is not always sufficient evidence that the application is in the correct state.**

## Historical lineage

The architecture originated as **HElement** during my HELM-era work and later evolved into **XElement** as I adapted the framework to another application environment.

The surviving early `sel` repository is best understood as a **transition snapshot of that earlier framework with targeted adaptations**, including feature upgrades and removal/reduction of application-specific behavior that was no longer relevant, including portions of the earlier Knockout-specific functionality.

This repository does **not** publish that historical source. The code under `src/` is a newly written, employer-neutral reconstruction of the architecture.

See [`docs/HISTORY.md`](docs/HISTORY.md) and [`docs/PUBLIC-BOUNDARY.md`](docs/PUBLIC-BOUNDARY.md).

## What I modernized

The historical implementation used techniques that made sense in its original environment but that I would not reproduce unchanged today.

This reconstruction replaces:

- string states with typed `ElementState`;
- global/static drivers with injected `IWebDriver` ownership;
- fixed sleeps with explicit polling;
- locator-string parsing with typed `By` chains;
- DOM-backed sentinel state with explicit unresolved state;
- broad/empty catches with explicit failure handling;
- hidden JavaScript interaction fallbacks with fail-closed interaction.

I would **not** replace the fundamental ideas:

- logical element identity;
- locator retention;
- late resolution;
- stale re-resolution;
- state-aware actions;
- adaptive synchronization;
- fast normal lookup;
- stronger must-exist lookup;
- scoped locator composition;
- diagnostic failure localization.

## Historical outcome

A representative HELM critical-path E2E workflow dropped from roughly **9 minutes to under 2 minutes** after the element model and synchronization approach were integrated and hardened.

The more important outcome was that the framework stopped paying conservative waits when the application was already ready, improving both execution speed and the meaning of failures.

## Relationship to modern tools

Years later, modern browser-automation tools made several related ideas first-class concepts: locator identity, late resolution, automatic waiting and actionability checks.

This project does **not** claim to be an implementation or precursor of any specific modern framework. It documents an independent response to the same underlying class of UI-automation problems.

## Build

```bash
dotnet restore src/XElementArchitecture/XElementArchitecture.csproj
dotnet build src/XElementArchitecture/XElementArchitecture.csproj -c Release

dotnet restore examples/XElementArchitecture.Examples.csproj
dotnet build examples/XElementArchitecture.Examples.csproj -c Release
```

GitHub Actions performs the same compile checks on every push and pull request.

## Public / IP boundary

This repository intentionally contains only:

- employer-neutral architecture;
- newly written demonstration code;
- synthetic selectors and examples;
- high-level historical design context;
- already-public career outcomes.

It intentionally excludes employer source code, internal hostnames, customer data, credentials, proprietary selectors/workflows, application-specific schemas, binaries and historical private-repository files.

## Usage

This repository is public for portfolio review and technical evaluation. It is **not open source** and no general reuse license is granted.

See [`LICENSE.md`](LICENSE.md).
