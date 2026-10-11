# Public / IP boundary

This repository is a sanitized architecture demonstration, not a dump of a historical employer repository.

## Preserved

The public reconstruction intentionally preserves:

- XElement's logical-element model;
- UIWrapper's role and public lookup surface;
- `FE`, `MFE` (Must Find Element), `FEs`;
- string-first smart locator handling;
- `VerifyPath` and `VerifyElement`;
- locator retention and stale re-resolution;
- occurrence/index preservation;
- `SimplifyLocator`, `ReduceLocator` and conversion seams;
- `ConcatLocators` and relative `FindElement` behavior;
- generic Knockout `DataFor` inspection;
- Global runtime responsibilities;
- OPTIMIZE/HIGHLIGHT concepts;
- state-preserving continuous execution;
- high-level historical outcomes already used publicly in career material.

## Removed or replaced

The public code intentionally excludes or neutralizes:

- usernames/passwords/API keys/secrets;
- personal or employer email addresses embedded in runtime code;
- internal hostnames, IPs and environment URLs;
- employer namespaces and application IDs;
- customer-specific workflows and page models;
- proprietary selectors and application schemas;
- old executable/browser-driver binaries;
- private configuration files;
- old test assemblies;
- source helpers with uncertain third-party provenance.

## Important distinction

Sanitization should remove sensitive **data and application coupling**, not erase the architecture being demonstrated.
