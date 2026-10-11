# Architecture

## System boundary

XElement is one layer in a larger automation framework:

```text
Tests / Page Objects / semantic CoreFunctions
                    ↓
                UIWrapper
            FE / MFE / FEs
                    ↓
                 XElement
        locator + path + index + E
                    ↓
   VerifyPath / VerifyElement / ConcatLocators
                    ↓
              Selenium WebDriver
                    ↓
                   DOM

Global owns framework runtime/lifecycle policy around those layers.
```

## Logical identity vs DOM state

The central design decision is that a logical element is not identical to the current Selenium DOM pointer.

- `Locator` describes durable identity.
- `Index` preserves occurrence when multiple matches exist.
- `Path` is the current resolved Selenium locator strategy.
- `E` is replaceable DOM state.
- `Stale`, `Current` and `Ready` model lifecycle/actionability.

The same XElement instance can therefore survive a DOM replacement by resolving its retained identity and replacing `E`.

## Fast path and strong path

`UIWrapper.FE(string)` is the low-cost default. It minimizes implicit-wait cost, prefers non-throwing existence checks, and can return an unresolved logical XElement.

`UIWrapper.MFE(string)` means **Must Find Element**. It is intentionally more expensive and verifies rendered geometry/stability before returning.

This avoids paying maximum synchronization cost for every lookup.

## Smart selectors

The framework exposes a string-first authoring surface. `By` remains available as an overload, but callers are not required to encode Selenium locator mechanics throughout test code.

`VerifyPath` and `SimplifyLocator` own the translation between logical locator text and current Selenium strategies.

## Relative composition

`ConcatLocators` plus `XElement.FindElement` retain parent logical context when locating a descendant/relative element. Parent occurrence is included in the composed locator, so relative identity can be rebuilt after DOM replacement.

## Runtime

`Global` carries framework-wide runtime concerns:

- WebDriver/session ownership;
- fail-closed test status;
- synchronization defaults;
- highlight/optimization diagnostics;
- setup/teardown;
- continuous state-preserving execution.

This is why the project is better understood as automation architecture rather than an `IWebElement` wrapper.
