using Azure;
using Client.Games.Cards.Services;
using Microsoft.Playwright;

namespace TestProject1
{
    /// <summary>
    /// Helps move cards in playwright tests of FreeCell by interacting with the page's DOM elements.
    /// Handles all FreeCell card movements between tableau, free cells, and foundations.
    /// All indexes are 0-based (columns 0-7, free cells 0-3, foundations 0-3).
    /// Successful moves are applied to the provided FreeCellGameService to keep state in sync.
    /// </summary>
    public class FreeCellMover
    {
        public static async Task<FreeCellMover> CreateAsync(IPage page, bool isDebugging)
        {
            var mover = new FreeCellMover(page)
            {
                DebugFlag = isDebugging
            };
            mover.gameService = await mover.GetGameServiceFromPage();
            LogAction(mover.gameService.dumpAllToLog($"Initial layout game {mover.gameService.GameId}"));
            return mover;
        }
        private readonly IPage _page;
        private FreeCellGameService? _gameService;
        public FreeCellGameService gameService
        {
            get
            {
                if (_gameService == null)
                {
                    throw new InvalidOperationException("GameService is not initialized");
                }
                return _gameService;
            }
            private set
            {
                _gameService = value;
            }
        }
        public  int DefaultDelayMs = 120;
        private const int DefaultTimeoutMs = 3000;
        private bool DebugFlag = true;

        /// <summary>
        /// When true, automatically moves cards to foundations after each successful move.
        /// Default is true to match standard FreeCell behavior.
        /// </summary>
        public bool AutoMoveToFoundation { get; set; } = false;

        public FreeCellMover(IPage page)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
        }
        public async Task<FreeCellGameService> GetGameServiceFromPage()
        {
            var json = await _page.EvaluateAsync<string>("() => window.getFreeCellStateJson()");
            return FreeCellGameService.FromJson(json);
        }

        #region Tableau -> FreeCell

        /// <summary>
        /// Move the bottom card from a tableau column to a free cell.
        /// </summary>
        /// <param name="columnIndex">0-based tableau column index (0-7)</param>
        /// <param name="freeCellIndex">0-based free cell index (0-3)</param>
        public async Task<bool> MoveTableauToFreeCellAsync(int columnIndex, int freeCellIndex)
        {
            LogDebug($"MoveTableauToFreeCell: column {columnIndex} -> freeCell {freeCellIndex}");


            if (!ValidateTableauIndex(columnIndex) || !ValidateFreeCellIndex(freeCellIndex))
                return false;

            var source = GetTableauBottomCard(columnIndex);
            var dest = GetFreeCell(freeCellIndex);

            var success = await ExecuteClickMoveAsync(source, dest, $"tableau[{columnIndex}]->freeCell[{freeCellIndex}]");
            if (success)
            {
                await ApplyMoveToGameServiceAsync(SourceType.Tableau, columnIndex, SourceType.FreeCell, freeCellIndex);
            }
            return success;
        }

        #endregion

        #region FreeCell -> Tableau

        /// <summary>
        /// Move a card from a free cell to a tableau column.
        /// </summary>
        /// <param name="freeCellIndex">0-based free cell index (0-3)</param>
        /// <param name="columnIndex">0-based tableau column index (0-7)</param>
        public async Task<bool> MoveFreeCellToTableauAsync(int freeCellIndex, int columnIndex)
        {
            LogDebug($"MoveFreeCellToTableau: freeCell {freeCellIndex} -> column {columnIndex}");

            if (!ValidateFreeCellIndex(freeCellIndex) || !ValidateTableauIndex(columnIndex))
                return false;

            var source = GetFreeCellCard(freeCellIndex);
            var sourceCount = await source.CountAsync();
            if (sourceCount == 0)
            {
                LogError($"Free cell {freeCellIndex} is empty");
                return false;
            }

            var dest = GetTableauColumn(columnIndex);
            var success = await ExecuteClickMoveAsync(source.First, dest, $"freeCell[{freeCellIndex}]->tableau[{columnIndex}]");
            if (success)
            {
                await ApplyMoveToGameServiceAsync(SourceType.FreeCell, freeCellIndex, SourceType.Tableau, columnIndex);
            }
            return success;
        }

        #endregion

        #region Tableau -> Foundation

        /// <summary>
        /// Move the bottom card from a tableau column to a foundation pile.
        /// </summary>
        /// <param name="columnIndex">0-based tableau column index (0-7)</param>
        /// <param name="foundationIndex">0-based foundation index (0-3)</param>
        public async Task<bool> MoveTableauToFoundationAsync(int columnIndex, int foundationIndex)
        {
            LogDebug($"MoveTableauToFoundation: column {columnIndex} -> foundation {foundationIndex}");

            if (!ValidateTableauIndex(columnIndex) || !ValidateFoundationIndex(foundationIndex))
                return false;

            var source = GetTableauBottomCard(columnIndex);
            var dest = GetFoundation(foundationIndex);

            var success = await ExecuteClickMoveAsync(source, dest, $"tableau[{columnIndex}]->foundation[{foundationIndex}]");
            if (success)
            {
                await ApplyMoveToGameServiceAsync(SourceType.Tableau, columnIndex, SourceType.Foundation, foundationIndex);
            }
            return success;
        }

        #endregion

        #region Foundation -> Tableau

        /// <summary>
        /// Move the top card from a foundation pile to a tableau column.
        /// This is a legal move in FreeCell.
        /// </summary>
        /// <param name="foundationIndex">0-based foundation index (0-3)</param>
        /// <param name="columnIndex">0-based tableau column index (0-7)</param>
        public async Task<bool> MoveFoundationToTableauAsync(int foundationIndex, int columnIndex)
        {
            LogDebug($"MoveFoundationToTableau: foundation {foundationIndex} -> column {columnIndex}");

            if (!ValidateFoundationIndex(foundationIndex) || !ValidateTableauIndex(columnIndex))
                return false;

            var source = GetFoundationTopCard(foundationIndex);
            var sourceCount = await source.CountAsync();
            if (sourceCount == 0)
            {
                LogError($"Foundation {foundationIndex} is empty");
                return false;
            }

            var dest = GetTableauColumn(columnIndex);
            var success = await ExecuteClickMoveAsync(source.Last, dest, $"foundation[{foundationIndex}]->tableau[{columnIndex}]");
            if (success)
            {
                await ApplyMoveToGameServiceAsync(SourceType.Foundation, foundationIndex, SourceType.Tableau, columnIndex);
            }
            return success;
        }

        #endregion

        #region FreeCell -> Foundation

        /// <summary>
        /// Move a card from a free cell to a foundation pile.
        /// </summary>
        /// <param name="freeCellIndex">0-based free cell index (0-3)</param>
        /// <param name="foundationIndex">0-based foundation index (0-3)</param>
        public async Task<bool> MoveFreeCellToFoundationAsync(int freeCellIndex, int foundationIndex)
        {
            LogDebug($"MoveFreeCellToFoundation: freeCell {freeCellIndex} -> foundation {foundationIndex}");

            if (!ValidateFreeCellIndex(freeCellIndex) || !ValidateFoundationIndex(foundationIndex))
                return false;

            var source = GetFreeCellCard(freeCellIndex);
            var sourceCount = await source.CountAsync();
            if (sourceCount == 0)
            {
                LogError($"Free cell {freeCellIndex} is empty");
                return false;
            }

            var dest = GetFoundation(foundationIndex);
            var success = await ExecuteClickMoveAsync(source.First, dest, $"freeCell[{freeCellIndex}]->foundation[{foundationIndex}]");
            if (success)
            {
                await ApplyMoveToGameServiceAsync(SourceType.FreeCell, freeCellIndex, SourceType.Foundation, foundationIndex);
            }
            return success;
        }

        #endregion

        #region Foundation -> FreeCell

        /// <summary>
        /// Move the top card from a foundation pile to a free cell.
        /// </summary>
        /// <param name="foundationIndex">0-based foundation index (0-3)</param>
        /// <param name="freeCellIndex">0-based free cell index (0-3)</param>
        public async Task<bool> MoveFoundationToFreeCellAsync(int foundationIndex, int freeCellIndex)
        {
            LogDebug($"MoveFoundationToFreeCell: foundation {foundationIndex} -> freeCell {freeCellIndex}");

            if (!ValidateFoundationIndex(foundationIndex) || !ValidateFreeCellIndex(freeCellIndex))
                return false;

            var source = GetFoundationTopCard(foundationIndex);
            var sourceCount = await source.CountAsync();
            if (sourceCount == 0)
            {
                LogError($"Foundation {foundationIndex} is empty");
                return false;
            }

            var dest = GetFreeCell(freeCellIndex);
            var success = await ExecuteClickMoveAsync(source.Last, dest, $"foundation[{foundationIndex}]->freeCell[{freeCellIndex}]");
            if (success)
            {
                await ApplyMoveToGameServiceAsync(SourceType.Foundation, foundationIndex, SourceType.FreeCell, freeCellIndex);
            }
            return success;
        }

        #endregion

        #region Tableau -> Tableau (Stack Move)

        /// <summary>
        /// Move one or more cards from one tableau column to another.
        /// </summary>
        /// <param name="srcColumnIndex">0-based source column index (0-7)</param>
        /// <param name="destColumnIndex">0-based destination column index (0-7)</param>
        /// <param name="cardCount">Number of cards to move (1 = bottom card only, >1 = stack from bottom)</param>
        public async Task<bool> MoveTableauToTableauAsync(int srcColumnIndex, int destColumnIndex, int cardCount = 1)
        {
            LogDebug($"MoveTableauToTableau: column {srcColumnIndex} -> column {destColumnIndex}, cardCount={cardCount}");

            if (!ValidateTableauIndex(srcColumnIndex) || !ValidateTableauIndex(destColumnIndex))
                return false;

            if (srcColumnIndex == destColumnIndex)
            {
                LogError("Source and destination columns cannot be the same");
                return false;
            }

            if (cardCount < 1)
            {
                LogError($"Card count must be >= 1, got {cardCount}");
                return false;
            }

            var cards = GetTableauCards(srcColumnIndex);
            var totalCards = await cards.CountAsync();

            if (totalCards == 0)
            {
                LogError($"Source column {srcColumnIndex} is empty");
                return false;
            }

            if (cardCount > totalCards)
            {
                LogError($"Cannot move {cardCount} cards from column {srcColumnIndex} which only has {totalCards} cards");
                return false;
            }

            // Get expected counts after move
            var destCards = GetTableauCards(destColumnIndex);
            var destCardsBefore = await destCards.CountAsync();
            var expectedSrcCount = totalCards - cardCount;
            var expectedDestCount = destCardsBefore + cardCount;

            // Calculate the index of the card to drag (0-based from top)
            var cardIndexFromTop = totalCards - cardCount;
            var source = cards.Nth(cardIndexFromTop);

            var dest = GetTableauColumn(destColumnIndex);
            var dragSuccess = await ExecuteDragMoveAsync(source, dest, $"tableau[{srcColumnIndex}]({cardCount} cards)->tableau[{destColumnIndex}]");

            if (!dragSuccess)
            {
                return false;
            }

            // can't match count because of auto move to foundation, so just apply the move to the game service and trust the page state is correct after the drag
            //// Verify the move actually happened on the page by checking card counts changed
            //var moveVerified = await VerifyMoveOnPageAsync(
            //    srcColumnIndex, expectedSrcCount,
            //    destColumnIndex, expectedDestCount,
            //    $"tableau[{srcColumnIndex}]->tableau[{destColumnIndex}]");

            //if (moveVerified)
            //{
            //}
            //else
            //{
            //    LogError($"Drag appeared to succeed but page state didn't change - move was rejected");
            //    return false;
            //}
            await ApplyTableauToTableauMoveAsync(srcColumnIndex, destColumnIndex, cardCount);
            return true;
        }

        /// <summary>
        /// Verifies that a tableau-to-tableau move actually occurred on the page.
        /// Retries with backoff to handle async UI updates.
        /// </summary>
        private async Task<bool> VerifyMoveOnPageAsync(
            int srcColumnIndex, int expectedSrcCount,
            int destColumnIndex, int expectedDestCount,
            string moveDescription,
            int maxRetries = 5, int initialDelayMs = 50)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(initialDelayMs * (1 << attempt));
                }

                var srcCards = await GetTableauCards(srcColumnIndex).CountAsync();
                var destCards = await GetTableauCards(destColumnIndex).CountAsync();

                if (srcCards == expectedSrcCount && destCards == expectedDestCount)
                {
                    if (attempt > 0)
                    {
                        LogDebug($"Move verified on page after {attempt + 1} attempts");
                    }
                    return true;
                }

                LogDebug($"VerifyMoveOnPage attempt {attempt + 1}/{maxRetries}: src={srcCards} (expected {expectedSrcCount}), dest={destCards} (expected {expectedDestCount})");
            }

            LogError($"Move {moveDescription} not reflected on page after {maxRetries} attempts");
            return false;
        }

        #endregion

        #region GameService State Updates

        /// <summary>
        /// Syncs the local game service state from the page.
        /// This is the source of truth - always trust the page state.
        /// </summary>
        private async Task SyncGameServiceFromPageAsync(string moveDescription)
        {
            // Wait a bit for any async operations (like auto-move) to complete
            await Task.Delay(150);

            var pageState = await GetGameServiceFromPage();

            // Log what changed
            var localMoveCount = gameService.MoveCount;
            var pageMoveCount = pageState.MoveCount;

            if (pageMoveCount != localMoveCount)
            {
                LogDebug($"After {moveDescription}: MoveCount changed {localMoveCount} -> {pageMoveCount}");
            }

            // Replace local state with page state
            gameService = pageState;
        }

        /// <summary>
        /// Applies a single-card move to the game service by syncing from page.
        /// </summary>
        private async Task ApplyMoveToGameServiceAsync(SourceType sourceType, int sourceIndex, SourceType targetType, int targetIndex)
        {
            await SyncGameServiceFromPageAsync($"{sourceType}[{sourceIndex}] -> {targetType}[{targetIndex}]");
        }

        /// <summary>
        /// Applies a tableau-to-tableau stack move to the game service by syncing from page.
        /// </summary>
        private async Task ApplyTableauToTableauMoveAsync(int srcColumnIndex, int destColumnIndex, int cardCount)
        {
            await SyncGameServiceFromPageAsync($"Tableau[{srcColumnIndex}] ({cardCount} cards) -> Tableau[{destColumnIndex}]");
        }

        #endregion

        #region Locator Helpers (all 0-based, converted to 1-based for CSS nth-child)

        private ILocator GetTableauColumn(int columnIndex) =>
            _page.Locator($".tableau-column:nth-child({columnIndex + 1})");

        private ILocator GetTableauCards(int columnIndex) =>
            _page.Locator($".tableau-column:nth-child({columnIndex + 1}) .playing-card");

        private ILocator GetTableauBottomCard(int columnIndex) =>
            GetTableauCards(columnIndex).Last;

        private ILocator GetFreeCell(int freeCellIndex) =>
            _page.Locator($".free-cell:nth-child({freeCellIndex + 1})");

        private ILocator GetFreeCellCard(int freeCellIndex) =>
            _page.Locator($".free-cell:nth-child({freeCellIndex + 1}) .playing-card");

        private ILocator GetFoundation(int foundationIndex) =>
            _page.Locator(".foundation-pile").Nth(foundationIndex);

        private ILocator GetFoundationTopCard(int foundationIndex) =>
            GetFoundation(foundationIndex).Locator(".playing-card");

        #endregion

        #region Validation Helpers

        private bool ValidateTableauIndex(int index)
        {
            if (index < 0 || index > 7)
            {
                LogError($"Invalid tableau column index: {index}. Must be 0-7.");
                return false;
            }
            return true;
        }

        private bool ValidateFreeCellIndex(int index)
        {
            if (index < 0 || index > 3)
            {
                LogError($"Invalid free cell index: {index}. Must be 0-3.");
                return false;
            }
            return true;
        }

        private bool ValidateFoundationIndex(int index)
        {
            if (index < 0 || index > 3)
            {
                LogError($"Invalid foundation index: {index}. Must be 0-3.");
                return false;
            }
            return true;
        }

        #endregion

        #region Move Execution

        private async Task<bool> ExecuteClickMoveAsync(ILocator source, ILocator dest, string moveDescription)
        {
            try
            {
                if (DebugFlag) await LogBoundingBoxes(source, dest, moveDescription);

                LogDebug($"Clicking source for {moveDescription}");
                await source.ClickAsync(new LocatorClickOptions { Force = true });
                await Task.Delay(DefaultDelayMs);

                LogDebug($"Clicking destination for {moveDescription}");
                await dest.ClickAsync(new LocatorClickOptions { Force = true });
                await Task.Delay(DefaultDelayMs);

                LogDebug($"Click move completed: {moveDescription}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Click move failed for {moveDescription}: {ex.GetType().Name}: {ex.Message}");
                await TakeFailureScreenshot(moveDescription);
                return false;
            }
        }

        private async Task<bool> ExecuteDragMoveAsync(ILocator source, ILocator dest, string moveDescription)
        {
            try
            {
                if (DebugFlag) await LogBoundingBoxes(source, dest, moveDescription);

                // Get bounding boxes first - we need them for the mouse drag
                var sBox = await source.BoundingBoxAsync();
                var dBox = await dest.BoundingBoxAsync();

                if (sBox == null || dBox == null)
                {
                    LogError($"Cannot get bounding boxes for drag: source={sBox != null}, dest={dBox != null}");
                    return false;
                }

                // For stacked cards, click near the TOP of the card (visible portion)
                // Cards overlap from top to bottom, so the top ~30px of each card is visible
                // Use 15px from top to ensure we're in the visible area
                var startX = sBox.X + sBox.Width / 2;
                var startY = sBox.Y + 15; // Near top of card (visible portion)
                var endX = dBox.X + dBox.Width / 2;
                var endY = dBox.Y + dBox.Height / 2;

                LogDebug($"Mouse drag from ({startX:F1},{startY:F1}) to ({endX:F1},{endY:F1}) [top-of-card drag]");

                await _page.Mouse.MoveAsync(startX, startY);
                await _page.Mouse.DownAsync();
                await _page.Mouse.MoveAsync(endX, endY, new MouseMoveOptions { Steps = 10 });
                await _page.Mouse.UpAsync();
                await Task.Delay(DefaultDelayMs);

                LogDebug($"Mouse drag completed: {moveDescription}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Drag move failed for {moveDescription}: {ex.GetType().Name}: {ex.Message}");
                await TakeFailureScreenshot(moveDescription);
                return false;
            }
        }

        #endregion

        #region Diagnostics

        private async Task LogBoundingBoxes(ILocator source, ILocator dest, string moveDescription)
        {
            if (!DebugFlag) return;
            try
            {
                var sBox = await source.BoundingBoxAsync();
                var dBox = await dest.BoundingBoxAsync();
                LogDebug($"BBox for {moveDescription}: src={FormatBox(sBox)}, dest={FormatBox(dBox)}");
            }
            catch (Exception ex)
            {
                LogDebug($"Could not get bounding boxes: {ex.Message}");
            }
        }

        private async Task TakeFailureScreenshot(string moveDescription)
        {
            try
            {
                var safeName = moveDescription.Replace("->", "-to-").Replace("[", "").Replace("]", "").Replace(" ", "_");
                var file = $"freecell-move-failure-{safeName}-{DateTime.UtcNow:yyyyMMddHHmmss}.png";
                await _page.ScreenshotAsync(new PageScreenshotOptions { Path = file, FullPage = true });
                LogError($"Screenshot saved: {file}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to take screenshot: {ex.Message}");
            }
        }

        private static string FormatBox(LocatorBoundingBoxResult? box)
        {
            if (box == null) return "null";
            return $"x={box.X:F1},y={box.Y:F1},w={box.Width:F1},h={box.Height:F1}";
        }

        /// <summary>
        /// Configurable log action - defaults to Console.WriteLine
        /// Tests can set this to InteractiveTestBase.Log for unified output
        /// </summary>
        public static Action<string> LogAction { get; set; } = Console.WriteLine;

        private void LogDebug(string message)
        {
            if (DebugFlag)
                LogAction($"[FreeCellMover] {message}");
        }

        private static void LogError(string message) =>
            LogAction($"[FreeCellMover ERROR] {message}");

        internal async Task Undo()
        {
            LogDebug($"Performing undo, Move count = {gameService.MoveCount}");
            var undoButton = _page.Locator("button:has-text('Undo')");
            await undoButton.ClickAsync();
            await SyncGameServiceFromPageAsync("Undo");
        }
        // we can verify or we can update the local copy.
        // the order of the foundations is not necessarily the same
        public async Task VerifyGameServiceCorrect(int maxRetries = 5, int initialDelayMs = 100)
        {
            string? lastError = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    // Exponential backoff: 100, 200, 400, 800, 1600ms
                    await Task.Delay(initialDelayMs * (1 << attempt));
                }

                var pgGameService = await GetGameServiceFromPage();
                lastError = CompareGameStates(pgGameService);

                if (lastError == null)
                {
                    if (attempt > 0)
                    {
                        LogDebug($"VerifyGameServiceCorrect succeeded after {attempt + 1} attempts");
                    }
                    return; // Success!
                }

                LogDebug($"VerifyGameServiceCorrect attempt {attempt + 1}/{maxRetries} failed: {lastError}");
            }

            throw new Exception($"Page game service mismatch after {maxRetries} attempts: {lastError}");
        }

        /// <summary>
        /// Compares local game state with page game state.
        /// Returns null if they match, or an error description if they don't.
        /// </summary>
        private string? CompareGameStates(FreeCellGameService pgGameService)
        {
            if (gameService.FreeCells.Count(c => c == null) != pgGameService.FreeCells.Count(c => c == null))
            {
                return $"FreeCellCount mismatch: local={gameService.FreeCells.Count(c => c == null)}, page={pgGameService.FreeCells.Count(c => c == null)}";
            }
            for (int i = 0; i < gameService.Tableau.Count; i++)
            {
                var column = gameService.Tableau[i];
                var pgColumn = pgGameService.Tableau[i];
                if (column.Count != pgColumn.Count)
                {
                    return $"Column {i} count mismatch: local={column.Count}, page={pgColumn.Count}";
                }
                for (int ndx = 0; ndx < column.Count; ndx++)
                {
                    if (column[ndx].ToString() != pgColumn[ndx].ToString())
                    {
                        return $"Column {i} card {ndx} mismatch: local={column[ndx]}, page={pgColumn[ndx]}";
                    }
                }
            }
            // now check foundations
            for (int i = 0; i < gameService.Foundations.Count; i++)
            {
                var foundation = gameService.Foundations[i];
                var pgFoundation = pgGameService.Foundations[i];
                if (foundation.Count != pgFoundation.Count)
                {
                    return $"Foundation {i} count mismatch: local={foundation.Count}, page={pgFoundation.Count}";
                }
            }
            return null; // Match!
        }

        /// <summary>
        /// Loads a custom game state into the browser page and syncs the local game service.
        /// </summary>
        public async Task LoadGameStateAsync(FreeCellGameService customGameService)
        {
            var state = customGameService.SerializeState();
            var json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            var result = await _page.EvaluateAsync<bool>("(json) => window.setFreeCellStateJson(json)", json);
            if (!result)
            {
                throw new InvalidOperationException("Failed to load game state into page via setFreeCellStateJson");
            }
            await Task.Delay(500); // Wait for Blazor UI to update
            gameService = await GetGameServiceFromPage();
            LogAction(gameService.dumpAllToLog("Loaded custom position"));
        }

        public async Task doMoveAsync(FreeCellMove move)
        {
            switch (move.sourceType)
            {
                case SourceType.Tableau:
                    switch (move.targetType)
                    {
                        case SourceType.FreeCell:
                            await MoveTableauToFreeCellAsync(move.sourceIndex, move.targetIndex);
                            break;
                        case SourceType.Foundation:
                            await MoveTableauToFoundationAsync(move.sourceIndex, move.targetIndex);
                            break;
                        case SourceType.Tableau:
                            await MoveTableauToTableauAsync(move.sourceIndex, move.targetIndex, move.cardCount);
                            break;
                    }
                    break;
                case SourceType.FreeCell:
                    switch (move.targetType)
                    {
                        case SourceType.Tableau:
                            await MoveFreeCellToTableauAsync(move.sourceIndex, move.targetIndex);
                            break;
                        case SourceType.Foundation:
                            await MoveFreeCellToFoundationAsync(move.sourceIndex, move.targetIndex);
                            break;
                    }
                    break;
                case SourceType.Foundation:
                    switch (move.targetType)
                    {
                        case SourceType.Tableau:
                            await MoveFoundationToTableauAsync(move.sourceIndex, move.targetIndex);
                            break;
                        case SourceType.FreeCell:
                            await MoveFoundationToFreeCellAsync(move.sourceIndex, move.targetIndex);
                            break;
                    }
                    break;

            }
        }
        #endregion
    }
}
