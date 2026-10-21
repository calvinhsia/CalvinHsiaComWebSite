# 01-prerequisites: Verify prerequisites and update global.json

Verify that the .NET 10 SDK is installed and the `global.json` file is updated to allow net10.0 builds. The repository root has a `global.json` that pins the SDK version to .NET 8 — this must be updated to allow the .NET 10 SDK. Also install the `wasm-tools` workload for .NET 10, which is required by the `Client` project (`WasmEnableSIMD=false` depends on this workload).

Validate SDK availability with `dotnet --version` and `dotnet workload list`. If `global.json` is pinned to `8.x`, update it to `10.x` (or remove the pinned version constraint and rely on the installed SDK).

**Done when**: `dotnet --version` reports a .NET 10 SDK, `wasm-tools` workload is installed for .NET 10, and `global.json` no longer blocks `net10.0` builds.

## Research Findings

- **SDK**: .NET 10 SDK is installed (validated via `validate_dotnet_sdk_installation`)
- **global.json**: No `global.json` file exists in the repository — no action needed
- **wasm-tools**: `wasm-tools-net8` was present but `wasm-tools` (for net10.0) was not installed → installed successfully
- **Workload version**: 10.0.400.1, `wasm-tools` now installed
