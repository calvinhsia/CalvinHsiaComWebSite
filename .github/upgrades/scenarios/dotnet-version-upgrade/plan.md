# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade 5 projects from `net8.0` to `net10.0`
**Scope**: Small solution, ~5 projects — Blazor WASM client, Azure Functions API, shared library, test project, benchmark suite

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: 5 projects, all on .NET 8 (modern .NET), clear dependency structure. No .NET Framework boundary to cross.

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | All-at-Once | 5 projects, modern-to-modern; small scope, mechanical TFM bump |
| Package Management | Central Package Management (CPM) | All SDK-style, same ecosystem, ≥5 projects |
| Unsupported API Handling | Fix Inline | Modern-to-modern; API changes are minor, no deferred stubs needed |

## Tasks

### 01-prerequisites: Verify prerequisites and update global.json

Verify that the .NET 10 SDK is installed and the `global.json` file is updated to allow net10.0 builds. The repository root has a `global.json` that pins the SDK version to .NET 8 — this must be updated to allow the .NET 10 SDK. Also install the `wasm-tools` workload for .NET 10, which is required by the `Client` project (`WasmEnableSIMD=false` depends on this workload).

Validate SDK availability with `dotnet --version` and `dotnet workload list`. If `global.json` is pinned to `8.x`, update it to `10.x` (or remove the pinned version constraint and rely on the installed SDK).

**Done when**: `dotnet --version` reports a .NET 10 SDK, `wasm-tools` workload is installed for .NET 10, and `global.json` no longer blocks `net10.0` builds.

---

### 02-cpm-setup: Set up Central Package Management

Create `Directory.Packages.props` at the solution root and migrate all package versions out of the individual `.csproj` files. All 5 projects use `PackageReference` (SDK-style) without centralized versions — consolidating them now avoids version drift as packages are bumped.

Collect all unique `PackageReference` entries across `Client\Client.csproj`, `Api\Api.csproj`, `Shared\Shared.csproj`, `TestProject1\TestProject1.csproj`, and `BenchmarkSuite1\BenchmarkSuite1.csproj`. Write `Directory.Packages.props` with `<PackageVersion>` entries at the current versions. Remove the `Version=` attribute from every `<PackageReference>` in each project file. Enable CPM by adding `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` to `Directory.Build.props` (create if absent).

Verify the solution still builds on `net8.0` after the CPM migration before proceeding.

**Done when**: `Directory.Packages.props` exists with all package versions, each `.csproj` has version-less `<PackageReference>` entries, and the solution builds successfully with CPM active.

---

### 03-upgrade-projects: Upgrade all projects to net10.0

Update all five project files to target `net10.0`, bump all NuGet packages to versions compatible with .NET 10, fix any breaking API and behavioral changes, and install the `wasm-tools` workload for .NET 10. This is the core upgrade task.

**TFM changes**: Set `<TargetFramework>net10.0</TargetFramework>` in all 5 projects. Keep `<WasmEnableSIMD>false</WasmEnableSIMD>` in `Client\Client.csproj` (iPad compatibility).

**Package upgrades** (update `Directory.Packages.props`):
- `Api`: `Microsoft.Azure.Functions.Worker`, `Microsoft.Azure.Functions.Worker.Sdk`, `Microsoft.Azure.Functions.Worker.Extensions.Http`, `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`, `Azure.Storage.Blobs`, `Microsoft.IdentityModel.Protocols.OpenIdConnect`
- `Client`: all referenced packages
- `Shared`: all referenced packages — note NuGet.0003 flag means one package is now built into the framework and should be removed
- `TestProject1`: `Microsoft.NET.Test.Sdk`, `Microsoft.Playwright`, `MSTest.TestAdapter`, `MSTest.TestFramework`, `coverlet.collector`, `Moq`, `MetadataExtractor` — NuGet.0005 flags a deprecated package that needs a replacement
- `BenchmarkSuite1`: all referenced packages

**Code fixes**: The assessment flagged IdentityModel & Claims-based Security issues (3 issues) in Api and/or Shared. Fix any source-incompatible API calls, behavioral changes, and deprecated usages inline during this task.

**Done when**: All 5 projects target `net10.0`, all packages are on .NET 10-compatible versions, the solution builds with 0 errors, and no suppression pragmas were added to paper over issues.

---

### 04-validate: Final validation — build and test

Run a full solution build and the test suite to confirm everything is functional after the upgrade. Fix any remaining test failures or build warnings in projects touched by this upgrade.

Run `dotnet build` on the solution, then `dotnet test --filter "TestCategory!=Manual"` to execute all automated tests (unit, integration, interactive) while excluding manual tests that require human interaction.

**Done when**: Solution builds without errors or warnings (in modified projects), all automated tests pass, and no manual suppressions remain.
