using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Client.Games.Cards.Services;

namespace TestProject1.Benchmarks;

[Config(typeof(InProcessConfig))]
public class FreeCellSolverBenchmarks
{
    private class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
            AddDiagnoser(MemoryDiagnoser.Default);
        }
    }

    [Benchmark]
    public void SolveThreeGames()
    {
        for (int gameId = 1; gameId <= 3; gameId++)
        {
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(gameId);
            var solver = new FreeCellSolver(gameService, loggerAction: null);
            solver.FindSolutionAsync().GetAwaiter().GetResult();
        }
    }
}