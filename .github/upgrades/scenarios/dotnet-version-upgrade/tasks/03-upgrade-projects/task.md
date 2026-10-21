# 03-upgrade-projects: Upgrade all projects to net10.0

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
