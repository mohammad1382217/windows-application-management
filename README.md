# MilOps — Secure Military Unit Management (WPF / .NET)

A production-grade, **offline-first** Windows desktop application for managing
soldiers, daily guard schedules (Lohe Posti), weapons, leaves, authorization
tokens, and audit logs — built **security-first** on Clean Architecture.

This `Wpf/` solution is a new desktop client that sits alongside the existing
ASP.NET Core / React web project in the repo root. It does **not** depend on any
network service; all data is stored in a locally **encrypted** database.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Default Credentials](#default-credentials)
- [Solution Structure](#solution-structure)
- [Architecture & Layers](#architecture--layers)
- [Security Design](#security-design)
- [Feature Modules](#feature-modules)
- [Configuration](#configuration)
- [Database & Migrations](#database--migrations)
- [Printing / Export](#printing--export)
- [Known Vulnerability Notice](#known-vulnerability-notice)
- [Engineering Notes](#engineering-notes)

---

## Quick Start

**Prerequisites**
- Windows 10/11 (x64). The app uses DPAPI, TPM (TBS), and SQLCipher.
- .NET SDK. The solution builds as-is on the **.NET 10 SDK**. It is written to
  be .NET 9-compatible — see [Engineering Notes](#engineering-notes) to switch.
- `dotnet-ef` tool for migrations (install once):
  ```bash
  dotnet tool install --global dotnet-ef --version 9.0.0
  ```

**Build & run**
```bash
cd Wpf
dotnet build MilOps.sln
dotnet run --project src/MilOps.Presentation/MilOps.Presentation.csproj
```

On first launch the app will:
1. Generate a SQLCipher DB key and audit HMAC key (DPAPI/TPM protected) and
   store the wrapped blobs under `%LOCALAPPDATA%\MilOps\secrets\`.
2. Create the encrypted database (`%LOCALAPPDATA%\MilOps\milops.db`) and apply
   the schema migration.
3. Seed a default Commander account (see below).

> All secrets, the database, and logs live under `%LOCALAPPDATA%\MilOps\`.
> Nothing is transmitted off the machine.

---

## Default Credentials

On first run a **Commander** account is seeded. **Change it immediately.**

| Field    | Value             |
|----------|-------------------|
| Username | `commander`       |
| Password | `ChangeMe!2024`   |

Commanders can create Operator and Read-Only accounts and rotate any password
from the **Users** module (every such action is audit-logged).

---

## Solution Structure

```
Wpf/
├── Directory.Build.props          # shared TFM + CPM switches (one place)
├── Directory.Packages.props       # central package versions (CPM)
├── MilOps.sln
└── src/
    ├── MilOps.Domain/             # entities, value objects, enums, ports (no deps)
    │   ├── Common/                # Entity, ValueObject, AggregateRoot, AuditableEntity
    │   ├── Entities/              # Soldier, User, CommanderToken, GuardSchedule, ...
    │   ├── ValueObjects/          # NationalCode, PersonnelCode, PersonName, TimeRange
    │   ├── Enums/                 # Role, Permission-derived, HealthType, ...
    │   ├── Exceptions/            # DomainException
    │   ├── Repositories/          # IRepository<T>, IUnitOfWork, ISpecification<T>
    │   └── Security/              # IPasswordHasher, ITokenGenerator, IAuditHasher (ports)
    │
    ├── MilOps.Application/        # CQRS (MediatR), validators, behaviors, DTOs
    │   ├── Common/                # Result, IDateTime
    │   ├── Security/              # Permissions, ICurrentUser, RBAC map
    │   ├── Behaviors/             # Authorization, Validation, UnhandledException
    │   ├── Soldiers/  Schedules/  Weapons/  Leaves/  Tokens/
    │   ├── Users/  Authentication/  Audit/   # features (commands/queries/handlers)
    │   └── DependencyInjection.cs
    │
    ├── MilOps.Infrastructure/     # EF Core + SQLCipher, DPAPI/TPM, Serilog, repos
    │   ├── Security/              # SecretProtector, TpmKeyProtector, Bcrypt, TokenGen, HMAC audit
    │   ├── Persistence/           # EfRepository<T>, EfUnitOfWork, EfAuditRepository
    │   ├── Db/                    # MilOpsDbContext, EncryptedDbContextFactory, Initializer, migration
    │   ├── Logging/               # Serilog config (secret-excluding filters)
    │   └── DependencyInjection.cs
    │
    └── MilOps.Presentation/       # WPF: App host, shell, views, view models
        ├── ViewModels/            # CommunityToolkit.Mvvm, [RelayCommand]
        ├── Views/                 # Login, Main shell, Soldiers/Tokens/.../Audit + editor + dialogs
        ├── Services/              # INavigationService, IDialogService, IPrintService
        ├── Converters/
        └── App.xaml(.cs)          # DI host bootstrap + DB init
```

**Dependency direction** (enforced by project references):
`Presentation → Infrastructure → Application → Domain`. The Domain has **no**
project or package dependencies.

---

## Architecture & Layers

- **Clean Architecture** with the dependency rule pointing inward.
- **CQRS** via MediatR: features split into `Commands` / `Queries` + handlers,
  with `IAuthorizedRequest` markers and a pipeline of behaviors:
  `Authorization → Validation → UnhandledException → Handler`.
- **Repository + Unit-of-Work** abstractions live in the Domain; EF Core
  implementations live in Infrastructure. The Application layer never references
  `DbContext` directly.
- **MVVM** in the Presentation layer using CommunityToolkit.Mvvm source-generated
  commands. ViewModels depend only on `ISender` (MediatR) and services — never on
  EF Core or WPF controls (except the editor dialogs which are windows).
- **Value Objects** (`NationalCode` with real Iranian check-digit validation,
  `PersonnelCode`, `PersonName`, `TimeRange`) encapsulate invariants at the
  domain boundary.

---

## Security Design

Security is implemented as first-class, layered defense in depth.

### Database encryption (SQLCipher)
- The SQLite database is encrypted with **SQLCipher** (bundled via
  `SQLitePCLRaw.bundle_e_sqlcipher`), using a **256-bit raw key** and hardened
  PRAGMAs (stronger KDF iterations, per-page HMAC, FK enforcement).
- The encryption key is **never** in `appsettings.json` or the connection string
  on disk. It is generated at first run with the .NET CSPRNG.

### Key protection (DPAPI + TPM)
- `SecretProtector` wraps every long-lived secret (DB key, audit HMAC key, token
  pepper) before persisting it:
  1. If a TPM is detected (via the Windows TBS API — `tbs.dll` P/Invoke), the
     secret is wrapped with a TPM-bound symmetric key (`TpmKeyProtector`).
  2. Otherwise it is wrapped with **Windows DPAPI (CurrentUser scope)**.
- In memory, secrets are kept as `byte[]` and zeroed (`CryptographicOperations.
  ZeroMemory`) immediately after use.
- The native TPM *sealing* seam (`TrySealWithNativeTpm`) is intentionally
  stubbed so the project builds anywhere; detection of the TPM is real and gates
  the strategy. Drop in a TPM library (e.g. Microsoft.TSS.Api) to enable full
  hardware-bound sealing. See the in-code docstring on `TpmKeyProtector`.

### Authentication & RBAC
- Passwords are hashed with **BCrypt** (configurable work factor, default 12);
  **plaintext is never stored** (this fixes the plaintext-password flaw in the
  original web project's `Login` table).
- Account **lockout** after N failed attempts (configurable). Login failure path
  runs a dummy hash compare to keep timing roughly constant regardless of whether
  the account exists (user-enumeration mitigation).
- Three roles via **Role-Based Access Control**: `Commander` (all permissions),
  `Operator` (read/write operational data, no user/token management),
  `ReadOnly` (read-only on permitted modules). See `RolePermissions`.

### Commander Token System
- The Commander generates one-time authorization tokens (account activation,
  registration, permission assignment).
- Token = Base64Url of CSPRNG bytes, grouped for readability.
- **Only a hash is stored**: `SHA-256(token + pepper)` where the pepper is itself
  DPAPI/TPM-protected — so an attacker with only the DB cannot verify guesses.
- The **plaintext is shown exactly once** (and copied to clipboard) at creation;
  it is never persisted or retrievable again.
- Lifecycle: `Active → Used | Revoked | Expired`, with `Created`/`Expires`/
  `UsedAt` timestamps. Revocation requires a reason.

### Audit logging (tamper-evident)
- All important operations are recorded (login/logout, user create, token
  generate/revoke, soldier/schedule/weapon changes, report prints, etc.).
- Append-only storage: no UPDATE/DELETE path is exposed; a `UNIQUE` index on the
  monotonic `Sequence` guards ordering at the DB level.
- **Chained HMAC-SHA256**: each row stores `PreviousHash` + `RowHash =
  HMAC(key, canonical(row) || PreviousHash)`. Altering or deleting any row breaks
  the chain — **tamper-evident**. The HMAC key is DPAPI/TPM-protected.
- A "Verify Chain" command recomputes the chain and reports the first broken
  sequence number. (This is tamper-*evident*, not tamper-*proof* — see the
  honest threat-model note on `SecretProtector`.)

### Logging
- **Serilog** to rolling daily files (30-day retention) under
  `%LOCALAPPDATA%\MilOps\logs`, with `Microsoft.*` at Warning and **filters that
  exclude** any `Password`, `PasswordHash`, `Token`, or `PlaintextToken` property
  so secrets never reach the log sink.

---

## Feature Modules

| Module | Capabilities |
|---|---|
| **Soldiers** | CRUD, search/filter by text/health/department, paper-style list print |
| **Guard Schedule (Lohe Posti)** | per-date board, shift/post assignments, approval, print |
| **Weapons** | register, issue/return, full assignment history, inventory print |
| **Leaves** | request/approve/reject, prevents double-booking unavailable soldiers |
| **Commander Tokens** | generate (one-time plaintext reveal), revoke, list active/expired |
| **Users** | create (Commander-only), change password, deactivate |
| **Audit Log** | query by date/action, print, HMAC chain integrity verification |

---

## Configuration

`src/MilOps.Presentation/appsettings.json`:

```json
{
  "Authentication": { "MaxFailedAttempts": 5, "MinPasswordLength": 8 },
  "Security": {
    "SecretsDirectory": "",          // empty => %LOCALAPPDATA%\MilOps\secrets
    "DpapiEntropy": "MilOps-v1-secret-entropy",
    "PreferTpm": true,
    "DatabaseKeyBytes": 32,          // 256-bit SQLCipher key
    "AuditHmacKeyBytes": 32,
    "BcryptWorkFactor": 12,
    "TokenBytes": 32
  }
}
```

---

## Database & Migrations

The runtime uses `EncryptedDbContextFactory` (injects the unwrapped key into the
SQLCipher connection). Migrations are scaffolded via the design-time factory
`DesignTimeDbContextFactory` (unencrypted throwaway DB) — **never** with a real key.

```bash
# Add a migration (run from the Infrastructure project)
cd src/MilOps.Infrastructure
dotnet ef migrations add YourMigration --project MilOps.Infrastructure.csproj
```

The initial migration (`InitialCreate`) is included. On startup,
`DatabaseInitializer` runs `MigrateAsync` then seeds the default Commander.

---

## Printing / Export

`PrintService` builds a `FlowDocument` report and supports:
- **WPF printing** via the print dialog, and
- **XPS export** (open in any XPS viewer and "Print to PDF" for a portable copy).

All printing is local/offline. Report footers record generation time and machine
name for traceability.

---

## Known Vulnerability Notice

During restore you'll see:

> NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.10 has a known high severity
> vulnerability (GHSA-2m69-gcr7-jv3q).

This is the **native** SQLite/SQLCipher engine library that has no patched
transitive replacement in the 2.1.x line bundled by `SQLitePCLRaw.bundle_e_sqlcipher`.
Mitigations in this app: the database is at rest encrypted and the app is
offline/standalone (no network exposure of the SQLite engine). Track upstream for
a patched bundle and upgrade when available.

---

## Engineering Notes

- **Target framework:** The requirement specifies .NET 9, but the build machine
  only had the **.NET 10 SDK** installed. To ship something that *actually builds
  and runs* out of the box, `Directory.Build.props` defaults the TFMs to
  `net10.0` / `net10.0-windows`. To build on .NET 9, install the .NET 9 SDK and
  set those two properties to `net9.0` / `net9.0-windows`. All referenced
  packages (EF Core 9, etc.) are .NET 9-compatible.
- **Central Package Management** is enabled; all package versions live in
  `Directory.Packages.props`. Project files reference packages *without* versions.
- **Concurrency:** entities carry a `RowVersion` byte[] mapped as an EF Core
  row-version for optimistic-concurrency on writes.
- **`InternalsVisibleTo`:** the Domain exposes internal `AuditLog.Create(...)` to
  Infrastructure so only the audit pipeline can stamp rows with a valid HMAC.
- The existing web project in the repo root is untouched and independent.
