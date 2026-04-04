# FreeCell Solver Benchmarks

This project uses [BenchmarkDotNet](https://benchmarkdotnet.org/) to measure the performance of the FreeCell solver.

## Prerequisites

- .NET 8 SDK (see root `global.json`)
- Build the solution at least once: `dotnet build -c Release`

## Running Benchmarks

### From the Command Line

```bash
# Run all benchmarks
cd BenchmarkSuite1
dotnet run -c Release

# Run a specific benchmark by filter
dotnet run -c Release -- --filter "*SolveThreeGames*"
```

> **Note:** The benchmark project uses `InProcessEmitToolchain` to avoid rebuilding the
> full solution dependency chain (which includes the Azure Functions `Api` project).
> This means benchmarks execute in-process rather than in an isolated child process.

### From Visual Studio

1. Set **BenchmarkSuite1** as the startup project
2. Switch to **Release** configuration (benchmarks should always run in Release)
3. Press **Ctrl+F5** (Start Without Debugging)
4. If prompted by `BenchmarkSwitcher`, select the benchmark to run

### Interpreting Results

BenchmarkDotNet produces a summary table like:

| Method          | Mean     | Error     | StdDev    | Gen0     | Gen1    | Allocated |
|---------------- |---------:|----------:|----------:|---------:|--------:|----------:|
| SolveThreeGames | 1.028 ms | 0.0173 ms | 0.0153 ms | 189.4531 | 25.3906 |   1.14 MB |

- **Mean** — average execution time across all iterations
- **Error** — half of the 99.9% confidence interval
- **Gen0/Gen1** — GC collection counts per 1,000 operations
- **Allocated** — managed memory allocated per invocation

Full HTML/CSV/Markdown reports are written to `BenchmarkSuite1/BenchmarkDotNet.Artifacts/results/`.

## Available Benchmarks

| Benchmark | Description |
|-----------|-------------|
| `SolveThreeGames` | Solves FreeCell games #1, #2, and #3 end-to-end. These are quick games (~1 ms total) useful for measuring solver overhead and move-generation cost. |

## Adding a New Benchmark

1. Add a new `[Benchmark]` method to `FreeCellSolverBenchmarks.cs` (or create a new class)
2. Use `[GlobalSetup]` for expensive one-time initialization
3. Keep benchmark methods synchronous (use `.GetAwaiter().GetResult()` for async code)
4. Run and verify: `dotnet run -c Release -- --filter "*YourNewBenchmark*"`

---

## Using the Visual Studio Diagsession Profiler

The VS Performance Profiler captures `.diagsession` files with detailed CPU, memory, and other diagnostics. This is useful for drill-down analysis beyond what BenchmarkDotNet provides.

### Profiling a Unit Test

1. Open **Test Explorer** (Test → Test Explorer)
2. Right-click the test you want to profile (e.g., `AutoSolve_FindSolution`)
3. Select **Profile** → choose **CPU Usage**, **Memory Usage**, or other tool
4. The test runs under the profiler and a `.diagsession` file opens when complete
5. Use the **Hot Path** and **Call Tree** views to identify bottlenecks

### Profiling the Benchmark Executable

1. Set **BenchmarkSuite1** as startup project, **Release** configuration
2. Go to **Debug → Performance Profiler** (Alt+F2)
3. Check **CPU Usage** (and optionally **.NET Object Allocation Tracking**)
4. Click **Start** — the benchmark runs and a `.diagsession` opens when done
5. Click the hot time range in the timeline, then drill into **Hot Path** or **Caller/Callee**

### Reading a .diagsession File

- **Summary view** — timeline of CPU usage with selectable ranges
- **Top Functions** — functions sorted by total/self CPU time
- **Hot Path** — the deepest call chain consuming the most CPU (look for `[HOT]` markers)
- **Call Tree** — full tree of all calls with inclusive/exclusive times
- **Caller/Callee** — shows who calls a function and what it calls

### Tips

- Always profile in **Release** mode — Debug builds include extra checks and no inlining
- Use **Ctrl+Click** on a time range in the summary to zoom into a specific interval
- Right-click a function → **View Source** to jump to the hot code
- Save `.diagsession` files to compare before/after optimization runs
- If a `.diagsession` is already open in VS, you can see it under the open tabs

---

## Using GitHub Copilot for Performance Profiling

GitHub Copilot in Visual Studio includes a profiling agent that can capture traces, create benchmarks, and suggest optimizations — all from the chat window.

### Capturing a CPU Profile

1. Open **GitHub Copilot Chat** (View → GitHub Copilot Chat)
2. Start a debug session for your application or test
3. Ask Copilot: *"Why is my code slow?"* or *"Profile the CPU usage"*
4. Copilot uses `run_profiler` to capture a CPU or memory trace
5. It returns the **hot path** and **top functions** ranked by CPU time

### Running Existing Benchmarks through Copilot

1. Ask: *"Run the SolveThreeGames benchmark"*
2. Copilot discovers benchmarks with `get_benchmarks`, then executes with `run_benchmark`
3. Results are displayed inline with a markdown table of Mean, Error, Allocated, etc.

### Profiling a Specific Unit Test

1. Ask: *"Profile the AutoSolve_FindSolution test for CPU"*
2. Copilot uses `profile_unit_test` to run the test under the profiler
3. It returns the hot path and top functions, similar to a VS diagsession

### Optimization Workflow with Copilot

A typical Copilot-assisted optimization loop:

1. **Baseline** — *"Run the SolveThreeGames benchmark"* → records Mean and Allocated
2. **Identify** — *"What are the CPU hotspots?"* → Copilot shows top functions
3. **Optimize** — *"Optimize `FindMoveAnyTableauToTableauOrFoundation`"* → Copilot suggests and applies code changes
4. **Verify** — *"Run the SolveThreeGames benchmark again"* → compare Mean/Allocated with baseline
5. **Iterate** — repeat until satisfied with the improvement

### Example Copilot Prompts

| Prompt | What It Does |
|--------|-------------|
| *"Run the SolveThreeGames benchmark"* | Executes the BenchmarkDotNet benchmark and shows results |
| *"Profile AutoSolve_FindSolution for CPU"* | Captures CPU trace of the unit test |
| *"Why is FindSolutionAsync slow?"* | Analyzes hot path and suggests optimizations |
| *"What benchmarks are available?"* | Lists all benchmarks in the solution |
| *"Create a benchmark for solving game 227"* | Creates a new BenchmarkDotNet benchmark |

### Tips

- Copilot profiling works best when the solution builds successfully in Release
- Always establish a baseline before making changes
- Copilot will refuse to modify benchmark code — it only optimizes production code
- Ask Copilot to re-run the same benchmark after changes to measure the delta
