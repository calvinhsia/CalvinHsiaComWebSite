using Azure;
using Client.Games.Cards.Services;
using Microsoft.Playwright;

namespace TestProject1
{
    /// <summary>
    /// Handles all FreeCell card movements between tableau, free cells, and foundations.
    /// All indexes are 0-based (columns 0-7, free cells 0-3, foundations 0-3).
    /// Successful moves are applied to the provided FreeCellGameService to keep state in sync.
    /// </summary>
    public class FreeCellMover
    {
        public static async Task<FreeCellMover> CreateAsync(IPage page)
        {
            var mover = new FreeCellMover(page);
            var json = await page.EvaluateAsync<string>("() => window.getFreeCellStateJson()");
            mover.gameService = FreeCellGameService.FromJson(json);
            mover.dumpAllToLog($"Initial layout game {mover.gameService.GameId}"); ;
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
        private const int DefaultDelayMs = 120;
        private const int DefaultTimeoutMs = 3000;
        private bool DebugFlag = true;

        /// <summary>
        /// When true, automatically moves cards to foundations after each successful move.
        /// Default is true to match standard FreeCell behavior.
        /// </summary>
        public bool AutoMoveToFoundation { get; set; } = true;

        public FreeCellMover(IPage page)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
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
                ApplyMoveToGameService(SourceType.Tableau, columnIndex, SourceType.FreeCell, freeCellIndex);
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
                ApplyMoveToGameService(SourceType.FreeCell, freeCellIndex, SourceType.Tableau, columnIndex);
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
                ApplyMoveToGameService(SourceType.Tableau, columnIndex, SourceType.Foundation, foundationIndex);
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
                ApplyMoveToGameService(SourceType.Foundation, foundationIndex, SourceType.Tableau, columnIndex);
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
                ApplyMoveToGameService(SourceType.FreeCell, freeCellIndex, SourceType.Foundation, foundationIndex);
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
                ApplyMoveToGameService(SourceType.Foundation, foundationIndex, SourceType.FreeCell, freeCellIndex);
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

            // Calculate the index of the card to drag (0-based from top)
            var cardIndexFromTop = totalCards - cardCount;
            var source = cards.Nth(cardIndexFromTop);

            var dest = GetTableauColumn(destColumnIndex);
            var success = await ExecuteDragMoveAsync(source, dest, $"tableau[{srcColumnIndex}]({cardCount} cards)->tableau[{destColumnIndex}]");
            if (success)
            {
                ApplyTableauToTableauMove(srcColumnIndex, destColumnIndex, cardCount);
            }
            return success;
        }

        /// <summary>
        /// Move the bottom card only from one tableau column to another.
        /// </summary>
        public async Task<bool> MoveTableauCardToTableauAsync(int srcColumnIndex, int destColumnIndex)
        {
            return await MoveTableauToTableauAsync(srcColumnIndex, destColumnIndex, cardCount: 1);
        }

        #endregion

        #region GameService State Updates

        /// <summary>
        /// Applies a single-card move to the game service.
        /// </summary>
        private void ApplyMoveToGameService(SourceType sourceType, int sourceIndex, SourceType targetType, int targetIndex)
        {
            // Calculate card index for tableau (always bottom card)
            int cardIndex = sourceType == SourceType.Tableau && gameService.Tableau[sourceIndex].Count > 0
                ? gameService.Tableau[sourceIndex].Count - 1
                : 0;

            gameService.Select(sourceType, sourceIndex, cardIndex);
            var moved = gameService.TryMove(targetType, targetIndex);

            if (moved)
            {
                LogDebug($"GameService state updated: {sourceType}[{sourceIndex}] -> {targetType}[{targetIndex}]");
                PerformAutoMoveToFoundations();
            }
            else
            {
                LogError($"GameService rejected move: {sourceType}[{sourceIndex}] -> {targetType}[{targetIndex}]");
            }
        }

        /// <summary>
        /// Applies a tableau-to-tableau stack move to the game service.
        /// </summary>
        private void ApplyTableauToTableauMove(int srcColumnIndex, int destColumnIndex, int cardCount)
        {
            var column = gameService.Tableau[srcColumnIndex];
            if (column.Count == 0)
            {
                LogError($"GameService: Source column {srcColumnIndex} is empty");
                return;
            }

            // Card index is the first card in the stack to move
            int cardIndex = column.Count - cardCount;
            if (cardIndex < 0) cardIndex = 0;

            gameService.Select(SourceType.Tableau, srcColumnIndex, cardIndex);
            var moved = gameService.TryMove(SourceType.Tableau, destColumnIndex);

            if (moved)
            {
                LogDebug($"GameService state updated: Tableau[{srcColumnIndex}] ({cardCount} cards) -> Tableau[{destColumnIndex}]");
                PerformAutoMoveToFoundations();
            }
            else
            {
                LogError($"GameService rejected move: Tableau[{srcColumnIndex}] ({cardCount} cards) -> Tableau[{destColumnIndex}]");
            }
        }

        /// <summary>
        /// Performs auto-move to foundations if enabled.
        /// </summary>
        private void PerformAutoMoveToFoundations()
        {
            if (!AutoMoveToFoundation) return;

            var autoMoves = gameService.AutoMoveToFoundations();
            if (autoMoves > 0)
            {
                LogDebug($"Auto-moved {autoMoves} card(s) to foundations");
            }
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

        private void LogDebug(string message)
        {
            if (DebugFlag)
                Console.WriteLine($"[FreeCellMover] {message}");
        }

        private static void LogError(string message) =>
            Console.WriteLine($"[FreeCellMover ERROR] {message}");

        /// <summary>
        /// Dump the freecells, tableau and foundation from the gameservice similar to the visual layout for easy verification
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal void dumpAllToLog(string desc = "")
        {
            // Dump the freecells, tableau and foundation from the gameservice similar to the visual layout for easy verification
            // first showt he freecells, then the tableau columns, then the foundations
            Console.Write($"{desc} FreeCells:");
            for (int i = 0; i < gameService.FreeCells.Count; i++)
            {
                var card = gameService.FreeCells[i]?.ToString() ?? "    ";
                Console.Write($"  {card}");
            }
            Console.WriteLine();
            Console.WriteLine("Tableau:");
            var cnt = gameService.Tableau.Max(c => c.Count);
            for (int row = 0; row < cnt; row++)
            {
                for (int col = 0; col < gameService.Tableau.Count; col++)
                {
                    var card = row < gameService.Tableau[col].Count ? gameService.Tableau[col][row].ToString() : "   ";
                    Console.Write($"{card} ");
                }
                Console.WriteLine();
            }
            Console.Write("Foundations:");
            for (int i = 0; i < gameService.Foundations.Count; i++)
            {
                var cards = gameService.Foundations[i];
                var cardStr = cards.Count > 0 ? string.Join(",", cards.Select(c => c.ToString())) : "  ";
                Console.Write($" {cardStr}");
            }
            Console.WriteLine();
        }

        internal async Task Undo()
        {
            if (!this.gameService.CanUndo)
            {
                throw new Exception($"Can't undo, no moves to undo");
            }
            LogDebug($"Performing undo, Move count = {gameService.MoveCount}");
            var undoButton = _page.Locator("button:has-text('Undo')");
            await undoButton.ClickAsync();
            gameService.Undo();
            await Task.Delay(DefaultDelayMs);

        }

        #endregion

    }
}
