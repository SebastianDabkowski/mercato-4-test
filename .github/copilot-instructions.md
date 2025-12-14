# Copilot and Copilot Agents — Project Rules

These instructions govern all usage of GitHub Copilot and Copilot Agents in this repository. Copilot is a coding assistant only; it **must not** act as architect, product owner, or decision maker.

## Repository Overview
Mercato is a multi-vendor marketplace platform built as a modular monolith using ASP.NET Core. The system connects buyers and sellers in a unified web platform with escrow payments, commission calculation, and order management.

### Technology Stack
- **Framework:** ASP.NET Core 9.0 (C#)
- **Architecture:** Modular Monolith with clear bounded contexts
- **Database:** SQL Server/SQLite (module-owned schemas)
- **Cloud:** Azure
- **Payment Provider:** Przelewy24
- **Deployment:** Azure Web Apps

### Project Structure
```
src/
├── Application/           # Web application layer
│   └── SD.ProjectName.WebApp/
├── Modules/              # Business domain modules (bounded contexts)
│   └── SD.ProjectName.Modules.Products/
├── Tests/                # Unit and integration tests
│   └── SD.ProjectName.Tests.Products/
└── TestUI/              # UI tests
    └── SD.ProjectName.TestUI.WebTest/
```

## Build and Test Commands
- **Restore dependencies:** `dotnet restore src/SD.ProjectNameVertical.sln`
- **Build solution:** `dotnet build src/SD.ProjectNameVertical.sln --configuration Release`
- **Run all tests:** `dotnet test src/SD.ProjectNameVertical.sln`
- **Run specific test project:** `dotnet test src/Tests/SD.ProjectName.Tests.Products/SD.ProjectName.Tests.Products.csproj`
- **Publish web app:** `dotnet publish src/Application/SD.ProjectName.WebApp/SD.ProjectName.WebApp.csproj -c Release -o ./publish`

Before submitting any PR, ensure all tests pass and the solution builds successfully.

## Sources of Truth
- Implementation must follow the documented design: `architecture.md`, the Product Requirements Document (`prd.md` when present), approved ADRs in `adr/`, and the latest approved epics/user stories.  
- Do not invent requirements or change scope. If information is missing, ask for clarification instead of guessing.

## Role and Boundaries
- Respect the existing modular monolith architecture and module boundaries; do not bypass public contracts or share domain models/data across modules.  
- Do **not** propose or implement architecture changes, new modules, or structural refactors unless explicitly requested and backed by the source-of-truth documents.  
- Do not add new features beyond the explicit request; defer feature ideas to product stakeholders.  
- Keep changes contained to the requested area; avoid touching unrelated modules or layers.

### Module Boundaries (Critical)
Each module in `src/Modules/` represents a bounded context with:
- **Owned schema:** Each module owns its database tables and migrations
- **Public contracts:** Cross-module communication via explicit interfaces and domain events only
- **No shared entities:** Exchange identifiers and DTOs, never share domain models
- **Isolation:** Never write directly to another module's tables

Currently implemented modules: Products (more to be added incrementally)

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

## Security and Sensitive Areas
- **Payment logic:** Exercise extreme caution with payment processing, webhook handlers, and commission calculations
- **Authentication/Authorization:** Changes to identity, roles, or access control require explicit approval
- **GDPR-sensitive code:** User data handling, export, and deletion logic requires careful review
- **Cross-module boundaries:** Maintain strict isolation; violations can compromise data integrity
- All changes must pass security scanning before merge

## Communication Style
- Keep generated code, comments, and responses concise, direct, and conservative.  
- Avoid speculative advice or additional feature ideas; surface only what is necessary to fulfill the request.  
- When unsure, ask for clarification instead of assuming.
