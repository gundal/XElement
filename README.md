# XElement
### Stateful Selenium element architecture for reliable end-to-end UI automation

> **Source-available for portfolio review — All Rights Reserved. Not open source.**  
> Copyright © 2014–2026 Leeam Dagan. See [`LICENSE.md`](LICENSE.md).

XElement documents and reconstructs the Selenium framework architecture I originally developed during my HELM-era automation work and later evolved in another application environment.

This repository is intentionally **not a 2026 redesign of the idea**. The public code preserves the framework's distinctive authoring model, method names, responsibilities and algorithms wherever they are important to understanding the architecture. Employer-specific application code, credentials, hostnames, customer data and proprietary selectors are excluded or replaced with synthetic examples.

The core idea is:

> **The logical element survives even when its current DOM reference does not.**

Raw Selenium exposes an `IWebElement` representing a DOM node that exists at a particular moment. Dynamic UIs can replace that node. XElement treated the locator and occurrence as durable identity while the current `IWebElement` (`E`) remained replaceable state.

```text
Test / Page Object / Core Function
              ↓
          UIWrapper
      FE / MFE / FEs
              ↓
           XElement
   Locator / Path / Index / E
              ↓
 VerifyPath / VerifyElement
 ConcatLocators / FindElement
              ↓
          Selenium
              ↓
             DOM

Global
 ├─ WebDriver / framework lifecycle
 ├─ fail-closed TEST_STATUS
 ├─ synchronization policy
 ├─ HIGHLIGHT / OPTIMIZE
 ├─ continuous state-preserving execution
 └─ shared runtime context
```

## What XElement is

`XElement` is a **concrete stateful abstraction over Selenium's `IWebElement` model using composition**.

It does not inherit from `IWebElement`, and it is not a replacement for Page Objects. Page/Component Objects model application meaning; XElement owns element lifecycle and Selenium mechanics underneath them.

The important distinction is:

> **The XElement is the logical element. `E` is only its current DOM representation.**

An XElement retains:

- the logical locator (`Locator`);
- the currently resolved Selenium path (`Path`);
- the current replaceable `IWebElement` (`E`);
- occurrence/index identity (`Index`);
- current/stale/readiness state;
- visibility and geometry information;
- relative locator composition;
- optional application-model metadata.

That is what makes late re-resolution possible.

---

# The authoring model

The framework was designed so a test author could normally write:

```csharp
XElement username = ui.FE("username");
XElement password = ui.FE("password");
XElement login = ui.MFE("button[type='submit']");

username.SendKeys("example-user");
password.SendKeys("example-value");
login.Click();
```

rather than repeatedly writing and coordinating:

```csharp
By.Id(...)
By.CssSelector(...)
By.XPath(...)
WebDriverWait(...)
try/catch StaleElementReferenceException
FindElement(...)
```

`By` overloads existed, but **requiring the test author to construct a `By` was not the design**. Smart string locator handling belonged in the framework.

---

# UIWrapper

`UIWrapper` was the Selenium-facing facade. It owned the current driver and centralized element lookup behavior.

## `FE()` — fast / flexible element lookup

```csharp
XElement banner = ui.FE("status-banner");
```

Semantic contract:

> **This logical element belongs to the workflow. It may or may not physically exist at this exact instant.**

The fast path preferred `FindElements()`-style existence checks and temporarily minimized implicit wait cost so the common path did not pay a large timeout penalty merely to discover that an element was not present yet.

If the element could not currently be resolved, the logical XElement could remain valid with its locator retained and be resolved later.

## `MFE()` — Must Find Element

```csharp
XElement submit = ui.MFE("button[type='submit']");
```

`MFE` means **Must Find Element**.

Semantic contract:

> **At this point in the workflow, this element is required to exist.**

MFE deliberately did more work than FE. It could repeatedly resolve the locator, confirm that a rendered element had a non-zero location and size, reacquire it, and compare geometry between observations before returning it.

That gave the framework two different synchronization costs:

```text
FE
└─ cheapest reliable lookup path

MFE
└─ stronger, stability-aware lookup when the workflow requires it
```

The design principle was:

> **Use the cheapest reliable selector and synchronization path first; escalate only when the UI actually requires it.**

## `FEs()`

`FEs()` returned a list of XElements rather than exposing raw repeated `IWebElement` handling to callers.

## `VerifyElement()`

`UIWrapper.VerifyElement(By)` used the non-throwing collection lookup path and returned either the first match or `null` immediately.

## `VerifyPath()` / `SimplifyLocator()`

The framework accepted smart locator strings and serialized Selenium `By` values, normalized them, and attempted the appropriate strategy without forcing every test author to care whether the locator happened to be ID, name, CSS or XPath.

---

# XElement lifecycle

## Current vs stale

The framework retained enough information to represent:

> **“This logical element is part of the workflow, but its current DOM representation is no longer usable.”**

The historical implementation used a guaranteed DOM element as a **sentinel** for unresolved/stale state. The public reconstruction preserves that model with a neutral `body` sentinel rather than an application-specific element.

```text
requested logical element
        ↓
     XElement
        ↓
CURRENT ── E points to requested DOM element
STALE   ── locator survives; E can temporarily point at sentinel
        ↓
   re-resolve Locator
        ↓
 replace E and return to CURRENT
```

## `VerifyElement()`

The no-argument `XElement.VerifyElement()` is important to the architecture.

It first verifies whether the current `E` is still usable. If the DOM reference has become stale, the framework uses the retained locator and index to find a replacement DOM element and assign it back to the **same logical XElement**.

The test object does not have to be rebuilt merely because the DOM was rebuilt.

## `Ready`

`Ready` combines lifecycle and rendered state. A logical element is useful for an interaction only when its current DOM representation is resolved, displayed and geometrically present.

---

# Locator identity and composition

Locator identity was not merely metadata for logging. It enabled the framework to rebuild element references.

## `SimplifyLocator()`

`SimplifyLocator()` removes Selenium's serialized `By.*` wrapper syntax so the logical locator can be retained independently of the particular Selenium constructor used to create it.

## `ConvertCsstoXpath()` / `ConvertXPathToCss()`

The framework could normalize locator representation where needed. CSS-to-XPath conversion mattered especially when a relative child locator needed to be composed with a parent locator.

The public `Css2xpath.Transform()` implementation is newly written; it preserves the architectural entry point without publishing uncertain third-party or employer-derived helper code.

## `ConcatLocators()`

`ConcatLocators()` is one of the most important pieces of the original architecture.

Given a logical parent locator plus a relative modifier, it:

1. normalizes the parent into a composable XPath when necessary;
2. preserves the parent's occurrence/index;
3. normalizes the relative child path;
4. concatenates them;
5. verifies that the composed path is valid.

Conceptually:

```text
parent:   [data-user-id='42']
child:    /..//*[@data-action='edit']

               ↓ Convert / preserve occurrence

composed logical locator
               ↓
          VerifyPath()
```

## The real `FindElement()`

The framework's `XElement.FindElement(string locator, int index = -1)` intentionally did **not** behave like a one-time search from a disposable `IWebElement`.

It used the parent XElement's retained logical locator and occurrence as the starting point:

```csharp
XElement row = ui.MFE("[data-user-id='42']");
XElement edit = row.FindElement("//*[@data-action='edit']");
```

The resulting child remains a logical XElement with its own retained locator rather than merely another transient DOM pointer.

---

# Interaction handling and OPTIMIZE

Actions such as `Click()` were centralized in XElement so lifecycle and synchronization behavior did not have to be duplicated throughout the suite.

A click could include:

1. checking/recovering current state;
2. checking readiness;
3. diagnosing an interaction attempted too early;
4. performing the normal Selenium interaction;
5. using an explicit fallback when WebDriver could not complete an otherwise expected interaction;
6. invoking the application-readiness seam afterward.

The historical `OPTIMIZE` mode separated two questions:

> **Can this workflow run?**

from:

> **Is this implementation production-worthy?**

Normal framework protections could keep test development moving while diagnostics identified places where synchronization or selectors needed work. With `OPTIMIZE` enabled, those weak points failed loudly so they could not quietly become permanent test-suite debt.

---

# Knockout / application-state inspection

The earlier framework could inspect Knockout-bound application objects through `ko.dataFor` rather than relying only on rendered text.

That let tests reason about:

```text
Rendered UI
    +
Bound application model
```

The public repository retains the generic `DataFor` mechanism but deliberately excludes employer-specific object schemas, selectors and workflow logic.

---

# Global — the framework runtime

XElement did not exist in isolation. `Global` shows that the larger system was an automation runtime rather than only an element helper.

The public reconstruction preserves the architectural responsibilities that mattered:

- WebDriver/session ownership;
- a fail-closed `TEST_STATUS` that starts as `FAILED`;
- framework synchronization settings;
- `HIGHLIGHT` diagnostics;
- `OPTIMIZE` hardening mode;
- shared execution context;
- setup / teardown lifecycle;
- optional continuous state-preserving execution.

## Continuous execution

The historical system could run tests independently or chain compatible tests together without throwing away browser/application state between every case.

The original environment used reflection and application-specific test class names. The public reconstruction replaces those names with a neutral registry, but retains the same architectural idea:

```text
Test A succeeds
     ↓
NextTestCase configured?
     ↓ yes
reuse UIWrapper / browser / state
     ↓
Test B
```

That is execution-policy separation: the test implementation does not need a second copy merely because it is being run as part of a longer stateful workflow.

---

# Page Objects and semantic layers

XElement works **with** Page Objects; it does not replace them.

```text
Test
 ↓
Page / Component Object or Core Function
 ↓
UIWrapper
 ↓
XElement
 ↓
Selenium IWebElement
 ↓
DOM
```

A Page Object expresses application behavior. XElement handles browser-element lifecycle underneath it.

See [`examples/LoginPage.cs`](examples/LoginPage.cs) and [`src/XElementArchitecture/CoreFunctionality/CoreFunctions.cs`](src/XElementArchitecture/CoreFunctionality/CoreFunctions.cs).

---

# Failure localization / application-state guards

A selector can be valid and still be valid on the **wrong screen**.

The broader framework addressed that class of failure with application-state preconditions so a test could stop near the actual navigation/state failure instead of continuing and generating misleading downstream failures.

The employer-neutral example is in [`examples/PageStateGuard.cs`](examples/PageStateGuard.cs).

---

# Historical lineage

The architecture originated as **HElement** during my HELM-era work and later evolved into **XElement**.

The surviving early `sel` repository is a **transition snapshot of the earlier framework with targeted adaptations**, including feature changes and removal/reduction of application-specific Knockout behavior that was no longer relevant in the new environment. It should not be interpreted as evidence that all of the architecture originated at the date of that snapshot.

This public repository does not publish the original private repositories. It reconstructs the architecture from that historical work while removing employer-sensitive material.

See [`docs/HISTORY.md`](docs/HISTORY.md).

---

# Historical result

After the HElement architecture replaced raw `IWebElement` handling across a representative critical-path workflow, runtime dropped from approximately:

# **~9 minutes → under 2 minutes**

More than the raw speedup, the framework stopped paying conservative waits when the application was already ready.

The larger objective was failure trustworthiness:

> **If a known-valid E2E workflow fails, investigate the product instead of reflexively assuming Selenium was flaky.**

That philosophy later continued in my work with deterministic test data, targeted retries that preserved the original failure evidence, CI diagnostics and highly trustworthy failure signals.

---

# What is deliberately different in this public reconstruction

This repository is **high-fidelity, not byte-for-byte historical source**.

Changes are limited to what is necessary for public release and current buildability:

- employer namespaces, hostnames, customer/workflow details and credentials are removed;
- application-specific selectors are replaced with synthetic selectors;
- obsolete Selenium API calls are mapped to current equivalents where necessary;
- xMatters/HELM-specific assembly names and reflection targets are replaced with neutral examples;
- the public CSS-to-XPath helper is newly written;
- generic exceptions replace dependencies on an employer's test assembly/runtime where necessary.

What has intentionally **not** been redesigned away:

- `UIWrapper`;
- `Global` runtime responsibilities;
- string-first smart locator authoring;
- `FE`, `MFE` (**Must Find Element**) and `FEs`;
- `VerifyPath()`;
- `VerifyElement()`;
- sentinel/unresolved logical state;
- `SimplifyLocator()` / `ReduceLocator()`;
- CSS/XPath conversion;
- `ConcatLocators()`;
- index/occurrence preservation;
- the real relative `FindElement()`;
- `Current`, `Stale`, `Ready`;
- generic `DataFor()` application-model inspection;
- centralized interaction handling;
- `HIGHLIGHT` and `OPTIMIZE` diagnostics;
- state-preserving continuous execution.

Those are part of the architecture being demonstrated.

---

# Repository map

```text
src/XElementArchitecture/
├── XElement.cs
├── UIWrapper.cs
├── Global.cs
├── Async.cs
├── FrameworkExceptions.cs
├── CoreFunctionality/
│   ├── CoreFunctions.cs
│   └── Css2Xpath.cs
└── Settings/
    ├── ConfigSettings.cs
    └── SettingsManager.cs

examples/
├── LoginPage.cs
├── ScopedComponent.cs
└── PageStateGuard.cs

docs/
├── ARCHITECTURE.md
├── HISTORY.md
├── PUBLIC-BOUNDARY.md
└── PRE-PUBLISH-CHECKLIST.md
```

---

# Build

```bash
dotnet restore src/XElementArchitecture/XElementArchitecture.csproj
dotnet build src/XElementArchitecture/XElementArchitecture.csproj -c Release

dotnet restore examples/XElementArchitecture.Examples.csproj
dotnet build examples/XElementArchitecture.Examples.csproj -c Release
```

GitHub Actions performs these compile checks.

---

# Public / IP boundary

This repository contains only employer-neutral reconstruction code, synthetic examples and high-level historical context.

It intentionally excludes:

- credentials and secrets;
- private hostnames and infrastructure addresses;
- customer data;
- employer-specific page models and workflows;
- proprietary application schemas;
- original binaries and driver executables;
- private configuration files;
- historical source files whose provenance or redistribution rights are unclear.

See [`docs/PUBLIC-BOUNDARY.md`](docs/PUBLIC-BOUNDARY.md).

## Usage

This repository is public for portfolio review and technical evaluation. It is **not open source**, and no general reuse license is granted.

See [`LICENSE.md`](LICENSE.md).
