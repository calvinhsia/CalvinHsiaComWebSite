# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0

## Source Control
- **Source Branch**: main
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)

## Upgrade Options
| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | All-at-Once | 5 projects, all modern .NET; small scope, mechanical TFM bump |
| Package Management | Central Package Management (CPM) | All SDK-style, same ecosystem, ≥5 projects |
| Unsupported API Handling | Fix Inline | Modern-to-modern; changes minor, no stubs needed |

## Strategy
**Selected**: All-at-Once
**Rationale**: 5 projects, all modern .NET (net8.0 → net10.0); small scope, mechanical TFM bump.

### Execution Constraints
- Single atomic upgrade — all projects updated together
- Validate full solution build after all TFMs and packages are updated
- Keep `WasmEnableSIMD=false` in Client.csproj throughout
- Fix all API/behavioral issues inline (no stubs)
- CPM migration must be validated (build passes) before TFM upgrade begins

## Key Requirements
- Keep `WasmEnableSIMD=false` in Client.csproj (iPad compatibility)
- Run `dotnet workload install wasm-tools` after TFM updates
- Do NOT create markdown documentation files for the changes
