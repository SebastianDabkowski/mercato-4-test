---
applyTo: "src/**/*.cs"
---

- Follow the architectural guidance in `architecture.md` and the module-boundary rules in `.github/copilot-instructions.md`.
- Keep changes scoped to the owning module and avoid sharing domain models across modules.
- Use xUnit (and FluentAssertions when already in use) for tests in `src/Tests` when behavior changes.
