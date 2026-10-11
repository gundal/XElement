# Historical context

The architecture began as **HElement** during Leeam Dagan's HELM-era automation work and later evolved into **XElement**.

The problem being solved was practical: raw Selenium `IWebElement` required individual test authors to manage too much DOM lifecycle, synchronization, locator and stale-reference behavior themselves.

The framework moved those responsibilities into a reusable logical-element layer and a surrounding automation runtime.

## Surviving source snapshots

The surviving early `sel` repository is a transition snapshot of the earlier framework rather than a clean-room project beginning at that repository's first date.

It contains substantial earlier architecture plus targeted changes for a new application environment, including feature adjustments and removal/reduction of application-specific Knockout behavior that was no longer relevant.

The private historical source is intentionally not published here.

## Why the public source is reconstructed

The public repository is intended to demonstrate the engineering architecture without exposing:

- employer-specific application code;
- internal infrastructure/configuration;
- credentials or historical secrets;
- customer/workflow data;
- source whose redistribution rights are uncertain.

The reconstruction therefore preserves architectural responsibilities, method names, authoring semantics and significant algorithms while replacing private/application-specific material with neutral equivalents.

## Relationship to modern browser tooling

Modern browser automation frameworks later made locator identity, late resolution, automatic waiting and actionability first-class concepts.

This repository does not claim lineage from or into those projects. HElement/XElement was an independent response to the same underlying automation problems encountered in a large Selenium suite.
