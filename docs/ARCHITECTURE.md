# Architecture notes

## Separation of concerns

```text
Test / workflow
      ↓
Page or component object
      ↓
UIWrapper
      ↓
XElement
      ↓
Selenium IWebElement
      ↓
DOM
```

`XElement` is intentionally **not** a replacement for Page Object / Component Object design.

- **Page/component objects** model the application and expose meaningful actions.
- **XElement** models the lifecycle of the logical UI element underneath them.
- **`IWebElement`** is the current Selenium DOM reference and is treated as replaceable state.

## Logical identity vs current DOM state

The central design choice is composition:

```text
XElement
 ├─ LocatorChain   ← durable identity
 ├─ ElementState   ← unresolved / resolved / stale
 └─ IWebElement E  ← current replaceable DOM reference
```

An `XElement` remains the same logical object even when the browser replaces the node that its current `IWebElement` points at.

## Fast path vs must-exist path

### `FE` / `Element`

One immediate resolution attempt. If the element does not exist yet, the logical `XElement` remains valid and unresolved.

> This element belongs to the workflow; it may not physically exist at this exact instant.

### `MFE` / `MustElement`

The workflow now requires the element. The reconstruction waits until it is actionable and sees the same geometry on two consecutive polling observations.

> This element is required to exist and be stable now.

That keeps the normal path cheap without weakening the cases that genuinely require stronger synchronization.

## Scoped locator chains

```csharp
var row = ui.Element(By.CssSelector("[data-user-id='42']"));
var edit = row.Find(By.CssSelector("[data-action='edit']"));

edit.Click();
```

If either DOM node is replaced, the chain can be resolved again from the driver.

## Readiness and stability

The reconstruction uses explicit polling via Selenium `WebDriverWait` rather than fixed sleeps. A required element is considered actionable when it is:

- resolved;
- displayed;
- enabled;
- rendered with non-zero area.

The stronger stable path additionally requires identical geometry on consecutive observations.

## Failure localization

A recurring theme in the original framework was preventing a test from continuing far beyond the true cause of failure. An application-state precondition can prevent otherwise-valid selectors from executing on the wrong screen when similar controls exist in multiple places.

`examples/PageStateGuard.cs` demonstrates the same general idea without employer-specific implementation.

## Diagnostics, not hidden recovery

The historical framework experimented with controlled fallbacks while emitting diagnostics. The public reconstruction is intentionally more conservative: stale references are re-resolved, but normal interaction failures are not silently converted into JavaScript interactions.

That keeps recovery observable and avoids turning framework resilience into false confidence.
