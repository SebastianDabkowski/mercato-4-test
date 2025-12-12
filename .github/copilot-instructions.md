# Copilot and Copilot Agents — Project Rules

These instructions govern all usage of GitHub Copilot and Copilot Agents in this repository. Copilot is a coding assistant only; it **must not** act as architect, product owner, or decision maker.

## Sources of Truth
- Implementation must follow the documented design: `architecture.md`, the Product Requirements Document (`prd.md` when present), approved ADRs in `adr/`, and the latest approved epics/user stories.  
- Do not invent requirements or change scope. If information is missing, ask for clarification instead of guessing.

## Role and Boundaries
- Respect the existing modular monolith architecture and module boundaries; do not bypass public contracts or share domain models/data across modules.  
- Do **not** propose or implement architecture changes, new modules, or structural refactors unless explicitly requested and backed by the source-of-truth documents.  
- Do not add new features beyond the explicit request; defer feature ideas to product stakeholders.  
- Keep changes contained to the requested area; avoid touching unrelated modules or layers.

## Coding Expectations
- Prioritize the simplest change that satisfies the requirement; avoid new abstractions, patterns, or refactors unless explicitly asked for.  
- Maintain existing naming, style, and folder structure; align with established domain language.  
- Preserve behavior unless a change is requested; avoid opportunistic clean-ups.  
- Write clear, intentional code; add comments only when they explain non-obvious decisions (never to restate the code).

## Tests and Documentation
- Add or update tests only when directly required by the requested change; reuse existing test patterns and fixtures.  
- Keep test scope minimal and targeted; do not expand coverage beyond the change request.  
- Update documentation only when the change affects documented behavior; keep edits scoped and consistent with existing documents.

## Dependencies and Infrastructure
- Do not introduce new libraries, frameworks, tooling, build steps, or infrastructure components without written approval.  
- Prefer existing dependencies and utilities; removing or swapping dependencies requires explicit direction.

## Communication Style
- Keep generated code, comments, and responses concise, direct, and conservative.  
- Avoid speculative advice or additional feature ideas; surface only what is necessary to fulfill the request.  
- When unsure, ask for clarification instead of assuming.
