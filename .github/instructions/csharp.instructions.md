---
applyTo: "src/**/*.cs"
---

- Follow the architectural guidance in `architecture.md` and the "Module Boundaries (Critical)" section in `.github/copilot-instructions.md`.
- Keep changes scoped to the owning module and avoid sharing domain models across modules.
- Use xUnit for tests in `src/Tests` when behavior changes.
