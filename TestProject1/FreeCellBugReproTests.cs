using Client.Games.Cards.Services;

namespace TestProject1;

[TestClass]
public class FreeCellBugReproTests
{
    private static void LogAction(string msg) => Console.WriteLine(msg);

    /// <summary>
    /// Reproduction test for bug: Game #605457 at move 64, solver reports "No moves found"
    /// when valid moves exist (3D to Foundation, 7D to Col0 on 8S).
    /// Tests with and without preVisitedStates/priorMoveHistory to isolate the cause.
    /// </summary>
    [TestMethod]
    [TestCategory("Automated")]
    public void Bug_Game605457_Move64_ShouldFindMoves()
    {
        var dumpStr = "Game #605457 Moves: 64\r\n" +
            " FreeCells:  KS  4H 10S  5H Foundations:  2D  5C  AS  AH BValue: 11\r\n" +
            "  KH  2S  QD  7H  KC  KD  8C 10D\r\n" +
            "  QS  3D  JS  3S  QH  QC  7D  9S\r\n" +
            "  JD     10H  6C  JC          8D\r\n" +
            " 10C      9C  JH              7C\r\n" +
            "  9H      8H  2H              6D\r\n" +
            "  8S      7S  9D                \r\n" +
            "          6H  6S                \r\n" +
            "          5S  5D                \r\n" +
            "          4D  4S                \r\n" +
            "              3H                \r\n" +
            "MoveHistory:\r\n" +
            "  AD:Col4>Fnd0\r\n" +
            "  5C:Col6>Col2\r\n" +
            "  6H:Col2>Col6x2\r\n" +
            "  10C:Col2>Free0\r\n" +
            "  5D:Col2>Col3\r\n" +
            "  AC:Col2>Fnd1\r\n" +
            "  AS:Col2>Fnd2\r\n" +
            "  4S:Col2>Col3\r\n" +
            "  4D:Col5>Col6\r\n" +
            "  3C:Col5>Col6\r\n" +
            "  2D:Col5>Fnd0\r\n" +
            "  6D:Col4>Col5\r\n" +
            "  AH:Col4>Fnd3\r\n" +
            "  QC:Col7>Col1\r\n" +
            "  5S:Col7>Col5\r\n" +
            "  9H:Col7>Free1\r\n" +
            "  10C:Free0>Col7\r\n" +
            "  9H:Free1>Col7\r\n" +
            "  JD:Col7>Col1x3\r\n" +
            "  4C:Col7>Free0\r\n" +
            "  3H:Col7>Free1\r\n" +
            "  3H:Free1>Col3\r\n" +
            "  4D:Col6>Col5x2\r\n" +
            "  8H:Col6>Col7x4\r\n" +
            "  10H:Col0>Free1\r\n" +
            "  9C:Col0>Free2\r\n" +
            "  10H:Free1>Col0\r\n" +
            "  9C:Free2>Col0\r\n" +
            "  JS:Col0>Col2x3\r\n" +
            "  8H:Col7>Col2x4\r\n" +
            "  7C:Col5>Col0x5\r\n" +
            "  4D:Col0>Col2x2\r\n" +
            "  8D:Col0>Col7x4\r\n" +
            "  2C:Col0>Fnd1\r\n" +
            "  3C:Col2>Fnd1\r\n" +
            "  4C:Free0>Fnd1\r\n" +
            "  4H:Col5>Col7\r\n" +
            "  4D:Col2>Free0\r\n" +
            "  5C:Col2>Fnd1\r\n" +
            "  4H:Col7>Free1\r\n" +
            "  QS:Col5>Free2\r\n" +
            "  4D:Free0>Col7\r\n" +
            "  10D:Col4>Col5\r\n" +
            "  8S:Col4>Col1\r\n" +
            "  QH:Col0>Col4\r\n" +
            "  QS:Free2>Col0\r\n" +
            "  10D:Col5>Free0\r\n" +
            "  10S:Col6>Col5\r\n" +
            "  5S:Col7>Col2x2\r\n" +
            "  8D:Col7>Col6x3\r\n" +
            "  10D:Free0>Col7\r\n" +
            "  9S:Col6>Col7x4\r\n" +
            "  4D:Col2>Free0\r\n" +
            "  10S:Col5>Free2\r\n" +
            "  4D:Free0>Col2\r\n" +
            "  KS:Col6>Free0\r\n" +
            "  JD:Col1>Col0x4\r\n" +
            "  KD:Col1>Col5x2\r\n" +
            "  8C:Col1>Col6\r\n" +
            "  7D:Col1>Col6\r\n" +
            "  JC:Col1>Col4\r\n" +
            "  5H:Col1>Free3\r\n" +
            "  5C:Fnd1>Col7\r\n" +
            "  5C:Col7>Fnd1";

        var gameService = FreeCellGameService.FromDumpString(dumpStr);
        LogAction(gameService.dumpAllToLog("Bug repro position"));

        // Build visited states by replaying from scratch (same as UI TrackCurrentState)
        var freshGame = new FreeCellGameService();
        freshGame.InitializeGame(605457);
        freshGame.UseNumericHash = true;
        freshGame.InitIncrementalHash();
        var visitedStates = new HashSet<ulong>();
        visitedStates.Add(freshGame.IncrementalHashValue);

        var moveHistory = FreeCellGameService.ParseMoveHistory(gameService.MoveHistory);
        LogAction($"Parsed {moveHistory.Count} moves from history");

        for (int i = 0; i < moveHistory.Count; i++)
        {
            var move = moveHistory[i];
            var applied = move.ApplyMoveFast(freshGame);
            Assert.IsTrue(applied, $"Failed to apply move {i}: {move}");
            freshGame.InitIncrementalHash();
            var hash = freshGame.IncrementalHashValue;
            var isNew = visitedStates.Add(hash);
            if (!isNew)
            {
                LogAction($"  Move {i}: {move} -> DUPLICATE hash {hash} (state already visited)");
            }
        }
        LogAction($"Total unique visited states: {visitedStates.Count}");

        // Verify hashes match between dump position and replayed position
        gameService.UseNumericHash = true;
        gameService.InitIncrementalHash();
        freshGame.InitIncrementalHash();
        LogAction($"Dump position hash:     {gameService.IncrementalHashValue}");
        LogAction($"Replayed position hash: {freshGame.IncrementalHashValue}");
        Assert.AreEqual(gameService.IncrementalHashValue, freshGame.IncrementalHashValue,
            "Replayed game hash should match dump position hash");

        // Test 1: WITHOUT preVisitedStates (baseline)
        var movesWithout = new FreeCellSolver(gameService,
            loggerAction: (msgf) => LogAction($"[NoHist] {msgf()}")).FindMoves();
        LogAction($"\nMoves WITHOUT history: {movesWithout.Count}");
        foreach (var m in movesWithout)
            LogAction($"  {m} mValue={m.mValue}");

        // Test 2: WITH preVisitedStates only
        var movesVisited = new FreeCellSolver(gameService,
            loggerAction: (msgf) => LogAction($"[Visited] {msgf()}"),
            preVisitedStates: visitedStates).FindMoves();
        LogAction($"\nMoves WITH visitedStates only: {movesVisited.Count}");
        foreach (var m in movesVisited)
            LogAction($"  {m} mValue={m.mValue}");

        // Test 3: WITH priorMoveHistory only
        var movesHistory = new FreeCellSolver(gameService,
            loggerAction: (msgf) => LogAction($"[History] {msgf()}"),
            priorMoveHistory: moveHistory).FindMoves();
        LogAction($"\nMoves WITH moveHistory only: {movesHistory.Count}");
        foreach (var m in movesHistory)
            LogAction($"  {m} mValue={m.mValue}");

        // Test 4: WITH both (like real CreateSolverWithHistory)
        var movesBoth = new FreeCellSolver(gameService,
            loggerAction: (msgf) => LogAction($"[Both] {msgf()}"),
            preVisitedStates: visitedStates,
            priorMoveHistory: moveHistory).FindMoves();
        LogAction($"\nMoves WITH both: {movesBoth.Count}");
        foreach (var m in movesBoth)
            LogAction($"  {m} mValue={m.mValue}");

        // Assertions
        Assert.IsTrue(movesWithout.Count > 0, "Baseline should find moves");
        Assert.IsTrue(movesBoth.Count > 0,
            $"With history should find moves. Baseline={movesWithout.Count}, " +
            $"Visited={movesVisited.Count}, History={movesHistory.Count}");
    }
}
