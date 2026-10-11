# Historical lineage

## HElement → XElement

The architecture originated as **HElement** during the HELM era and later evolved into **XElement** as the framework moved into another application environment.

The important continuity was architectural, not a claim that every historical line of code was unchanged:

- logical locator identity rather than disposable DOM identity;
- current `IWebElement` held as replaceable state;
- late resolution and stale re-resolution;
- readiness-driven interaction;
- fast normal lookup and a stronger must-exist path;
- context-aware locator chaining;
- centralized browser mechanics and diagnostics.

The surviving early `sel` repository is a **transition snapshot** of the earlier HELM-era framework with targeted adaptations: selected feature upgrades, environment changes, and removal/reduction of application-specific functionality that was no longer relevant, including portions of the earlier Knockout-specific behavior.

This public repository does **not** publish that historical source.

## Why the public code is a reconstruction

The purpose of this repository is to demonstrate the engineering model without exposing employer source, application details, selectors, internal hostnames, customer information, credentials, or proprietary workflows.

The implementation under `src/` was written as an employer-neutral modern reconstruction of the architectural ideas. It intentionally modernizes several mechanics while preserving the model.

## What changed in the modern reconstruction

Historical mechanics deliberately not reproduced:

- string lifecycle states such as `CURRENT` / `STALE`;
- global/static WebDriver ownership;
- `Thread.Sleep()`-based polling;
- parsing locator behavior from `By.ToString()`;
- DOM sentinel elements used as unresolved state;
- broad/empty exception catches;
- test-framework assertions inside the element layer;
- application-specific Knockout/model inspection in the core abstraction;
- silent or automatic JavaScript click fallbacks.

Modern equivalents in this repository:

- typed `ElementState`;
- injected `IWebDriver` session ownership;
- typed `By` objects inside a `LocatorChain`;
- explicit unresolved state;
- `WebDriverWait` polling and actionability checks;
- explicit `XElementResolutionException`;
- optional diagnostic sink;
- application state handled outside the core element abstraction.

## Historical outcome

A representative HELM critical-path E2E workflow dropped from roughly **9 minutes to under 2 minutes** after the element model and synchronization approach were integrated and hardened. The point was not merely speed: the framework stopped paying conservative waits when the application was already ready.

The same reliability philosophy later continued into deterministic test data, failure-preserving targeted retries, and higher-confidence CI failure signals in later work.

## Relationship to modern tools

Years later, modern browser-automation tools made several related ideas first-class: locator identity, late resolution, auto-waiting and actionability checks.

This project does **not** claim to be an implementation or precursor of any specific modern framework. It documents an independent response to the same underlying problem encountered while engineering a large Selenium system.
