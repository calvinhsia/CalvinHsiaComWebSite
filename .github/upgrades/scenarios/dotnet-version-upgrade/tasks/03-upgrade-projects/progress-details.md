# Progress Details — 03-upgrade-projects

## What Changed

### Project files — TFM bumped to net10.0
- `Client\Client.csproj` — `net8.0` → `net10.0` (WasmEnableSIMD=false preserved)
- `Api\Api.csproj` — `net8.0` → `net10.0`
- `Shared\Shared.csproj` — `net8.0` → `net10.0`; removed `<PackageReference Include="System.ComponentModel.Annotations" />` (now built into net10.0)
- `TestProject1\TestProject1.csproj` — `net8.0` → `net10.0`; fixed two Playwright Exec commands (path `net8.0` → `net10.0`)
- `BenchmarkSuite1\BenchmarkSuite1.csproj` — `<TargetFrameworks>net8.0</TargetFrameworks>` → `<TargetFramework>net10.0</TargetFramework>` (plural → singular)

### Directory.Packages.props — package versions updated
12 packages bumped to net10-compatible versions, System.ComponentModel.Annotations removed, MSTest 3.1.1 → 4.5.1 (deprecated → supported).

### Api\JwtValidator.cs — binary incompatible API migrated
- Removed `using System.IdentityModel.Tokens.Jwt`; added `using Microsoft.IdentityModel.JsonWebTokens`
- `JwtSecurityTokenHandler` → `JsonWebTokenHandler`
- `handler.ValidateToken(jwt, params, out token)` (sync) → `await handler.ValidateTokenAsync(jwt, params)` (async)
- Error/exception handling consolidated into `result.IsValid` check

### TestProject1 — MSTest 4.x breaking changes fixed
- `[ExpectedException]` attribute removed (MSTest 4): replaced with `Assert.Throws<T>()` in TestFreeCell.cs and TestMyPixSerialization.cs
- `Assert.ThrowsException<T>` → `Assert.Throws<T>` in TestMyPixSerialization.cs, TestPictureQuery.cs, TestFreeCell.cs
- `Assert.ThrowsExceptionAsync<T>` → `Assert.ThrowsAsync<T>` in TestAlbumService.cs
- `TestTimeout.Infinite` → `-1` in FreeCellSolverTests.cs (constant removed in MSTest 4)
- FreeCellSolverTests.cs `Assert.Throws` calls use fully qualified `Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Throws` to avoid ambiguity with `using static Microsoft.Playwright.Assertions`

## Build Results

`dotnet build calvinhsiacom.sln` → **Build succeeded with 47 warning(s)** (exit code 0)

All 47 warnings are pre-existing: MSTEST0032 analyzer hints and nullable reference CS8xxx warnings in Client and TestProject1 — none introduced by this upgrade.

## Done When Criteria

- ✅ All 5 project files target net10.0
- ✅ WasmEnableSIMD=false preserved in Client.csproj
- ✅ System.ComponentModel.Annotations removed from Shared.csproj
- ✅ Playwright script paths updated to net10.0 in TestProject1.csproj
- ✅ JwtSecurityTokenHandler migrated to JsonWebTokenHandler (async)
- ✅ MSTest 4.x API breaking changes fixed (ExpectedException, ThrowsException, TestTimeout.Infinite)
- ✅ Solution builds with zero errors
