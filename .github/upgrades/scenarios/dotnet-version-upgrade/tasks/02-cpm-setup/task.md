# 02-cpm-setup: Set up Central Package Management

Create `Directory.Packages.props` at the solution root and migrate all package versions out of the individual `.csproj` files. All 5 projects use `PackageReference` (SDK-style) without centralized versions — consolidating them now avoids version drift as packages are bumped.

Collect all unique `PackageReference` entries across `Client\Client.csproj`, `Api\Api.csproj`, `Shared\Shared.csproj`, `TestProject1\TestProject1.csproj`, and `BenchmarkSuite1\BenchmarkSuite1.csproj`. Write `Directory.Packages.props` with `<PackageVersion>` entries at the current versions. Remove the `Version=` attribute from every `<PackageReference>` in each project file. Enable CPM by adding `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` to `Directory.Build.props` (create if absent).

Verify the solution still builds on `net8.0` after the CPM migration before proceeding.

**Done when**: `Directory.Packages.props` exists with all package versions, each `.csproj` has version-less `<PackageReference>` entries, and the solution builds successfully with CPM active.
