# Edit Strategy

## Purpose

Use this file when deciding where and how to implement a change in a fork.

## Rules

- Do not edit `RobustToolbox/` for normal gameplay, UI, prototype, or localization work.
- Touch `RobustToolbox/` only when the task explicitly calls for engine work and there is no content-side solution.
- Prefer extending content code before changing engine code.
- Keep diffs narrow inside upstream files.
- Preserve existing ordering, spacing, and local style in touched files.
- When creating fork-only behavior, prefer `_OpenSpace` paths that mirror the upstream feature layout.
- When adding or changing OpenSpace-specific code in a file outside any `_OpenSpace` path, mark it:
  - Single added or changed line: append `// OpenSpace-Edit` as an inline comment.
  - Multiple lines: wrap with block markers:

```csharp
// OpenSpace Edit Start
...code here...
// OpenSpace Edit End
```

- Keep edit-marker ranges as narrow as practical. Do not wrap unrelated upstream code or whole files when only a small block belongs to OpenSpace.
- Multiple marked blocks in one file are allowed when the OpenSpace changes are separated by upstream code.
- For files that do not use `//` comments, use the native comment syntax while preserving `OpenSpace Edit Start`, `OpenSpace Edit End`, and `OpenSpace-Edit`.
- Prefer reusable hooks, helpers, and data-driven content over one-off special cases or hardcoded values.

## Good Patterns

- Add fork-specific systems or resources beside the upstream feature in `_OpenSpace`.
- Extend an existing public system API instead of rewriting the whole feature.
- Mark narrow OpenSpace-specific edits in inherited files with paired `OpenSpace-Edit` comments.
- Add only the fields, prototypes, and locale entries required for the task.
- Move repeated or feature-configurable behavior into reusable APIs, component data, or prototypes.

## Bad Patterns

- Moving a large amount of upstream code into fork-only files without need.
- Refactoring unrelated upstream code while fixing a small gameplay issue.
- Leaving unmarked OpenSpace-specific code in files outside `_OpenSpace`.
- Editing engine code because it feels cleaner than respecting content boundaries.
- Solving one call site with hardcoded IDs, strings, paths, or special-case branches when the code should stay reusable.
