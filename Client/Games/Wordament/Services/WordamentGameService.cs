using DictionaryLib;
using WordScapeBlazorWasm.Models;
using System.Linq;

namespace WordScapeBlazorWasm.Services
{
    public class WordamentGameService
    {
        private readonly IDictionaryService _dictionaryService;
        private readonly DebugHelper _debugHelper;
        private Random _random;

        public WordamentGameService(IDictionaryService dictionaryService, DebugHelper debugHelper)
        {
            _dictionaryService = dictionaryService;
            _debugHelper = debugHelper;
            InitializeRandom();
            DebugHelper.Log("WordamentGameService: Using shared DictionaryService instance");
        }

        private void InitializeRandom()
        {
            if (DebugHelper.IsDebugEnabled)
            {
                _random = new Random(1); // Fixed seed for debugging
                DebugHelper.Log("Wordament using DEBUG mode with fixed seed");
            }
            else
            {
                _random = new Random();
                DebugHelper.Log("Wordament using random seed");
            }
        }

        public WordamentGameState CreateNewGame(WordamentSettings settings)
        {
            DebugHelper.Log($"Creating new Wordament game - Mode: {settings.GameMode}, Duration: {settings.GameDurationMinutes}min, MinLength: {settings.MinWordLength}");

            var gameState = new WordamentGameState
            {
                GameStartTime = DateTime.Now,
                IsGameActive = true, // Ensure the game is active
                OriginalWordFound = false, // Reset the flag
                Score = 0,
                FoundWords = new HashSet<WordamentFoundWord>(), // Clear any previous words
                SelectedPath = new List<GridPosition>(),
                CurrentPath = "",
                GameMode = settings.GameMode, // Set the game mode in the state
                HintsUsed = 0, // Reset hint counter
                CurrentHint = "" // Clear current hint
            };

            // Set timer based on game mode
            if (settings.GameMode == WordamentGameMode.Timer)
            {
                gameState.TimeRemaining = TimeSpan.FromMinutes(settings.GameDurationMinutes);
                gameState.ElapsedTime = TimeSpan.Zero;
            }
            else // LongWord mode
            {
                gameState.TimeRemaining = TimeSpan.Zero; // Not used in LongWord mode
                gameState.ElapsedTime = TimeSpan.Zero; // Count up from zero
            }

            // Generate the grid and set the original word
            gameState.Grid.GenerateRandomGrid(_random, _dictionaryService);
            gameState.OriginalWord = gameState.Grid.OriginalWord;

            DebugHelper.Log($"Generated 4x4 grid for Wordament with original word: {gameState.OriginalWord}");
            LogGrid(gameState.Grid);

            return gameState;
        }

        private void LogGrid(WordamentGrid grid)
        {
            if (!DebugHelper.IsDebugEnabled) return;

            DebugHelper.Log("Wordament Grid:");
            for (int y = 0; y < WordamentGrid.Size; y++)
            {
                var row = "";
                for (int x = 0; x < WordamentGrid.Size; x++)
                {
                    var cell = grid.Cells[x, y];
                    var special = cell.IsSpecial ? $"({cell.SpecialType.ToString().Substring(0, 2)})" : "   ";
                    row += $"{cell.Letter}{special} ";
                }
                DebugHelper.Log($"  {row}");
            }
        }

        public bool IsValidPath(List<GridPosition> path, WordamentGrid grid)
        {
            if (path.Count < 2) return true; // Single cell or empty path is always valid

            for (int i = 1; i < path.Count; i++)
            {
                if (!grid.AreAdjacent(path[i - 1], path[i]))
                {
                    DebugHelper.Log($"Invalid path: {path[i - 1]} and {path[i]} are not adjacent");
                    return false;
                }

                // Check for duplicate positions (can't reuse same cell)
                for (int j = 0; j < i; j++)
                {
                    if (path[j].X == path[i].X && path[j].Y == path[i].Y)
                    {
                        DebugHelper.Log($"Invalid path: Position {path[i]} used multiple times");
                        return false;
                    }
                }
            }

            return true;
        }

        public string GetWordFromPath(List<GridPosition> path, WordamentGrid grid)
        {
            if (!IsValidPath(path, grid)) return "";

            var word = "";
            foreach (var pos in path)
            {
                var cell = grid.GetCell(pos.X, pos.Y);
                if (cell.X == -1) return ""; // Invalid position
                word += cell.Letter;
            }

            return word;
        }

        public bool IsValidWord(string word, int minLength = 3)
        {
            // Use the new validation method for UI feedback
            return IsValidWordForUI(word, minLength);
        }

        public WordamentFoundWord? SubmitWord(List<GridPosition> path, WordamentGrid grid, WordamentSettings settings)
        {
            if (path.Count == 0) return null;

            var word = GetWordFromPath(path, grid);
            if (string.IsNullOrEmpty(word))
            {
                DebugHelper.Log("Invalid path - no word formed");
                return null;
            }

            // Check minimum length requirement
            if (word.Length < settings.MinWordLength)
            {
                DebugHelper.Log($"'{word}' is too short (minimum length: {settings.MinWordLength})");
                return null; // Don't add words that are too short
            }

            // NEW: Use WordScape-style word validation - always add the word but classify it
            var wordType = ValidateWordType(word);
            var score = wordType != FoundWordType.SubWordNotAWord ? CalculateWordScore(word, path, grid) : 0;
            
            var foundWord = new WordamentFoundWord
            {
                Word = word,
                Path = new List<GridPosition>(path),
                Score = score,
                FoundAt = DateTime.Now,
                IsRareWord = IsRareWord(word),
                IsLongestWord = false, // Will be determined after all words are found
                WordType = wordType // NEW: Set the word type classification
            };

            // Check if this is the original word in LongWord mode
            if (settings.GameMode == WordamentGameMode.LongWord && 
                word.Equals(grid.OriginalWord, StringComparison.OrdinalIgnoreCase))
            {
                DebugHelper.Log($"ORIGINAL WORD FOUND! '{word}' matches grid original word '{grid.OriginalWord}'");
                foundWord.IsLongestWord = true; // Mark as special
            }

            DebugHelper.Log($"Word submitted: '{word}' classified as {wordType} for {score} points");
            return foundWord;
        }

        /// <summary>
        /// NEW: Validate word type using WordScape logic
        /// </summary>
        public FoundWordType ValidateWordType(string word)
        {
            DebugHelper.Log($"Validating word type: '{word}'");

            if (string.IsNullOrEmpty(word))
            {
                DebugHelper.Log($"Invalid - empty word");
                return FoundWordType.SubWordNotAWord;
            }

            // CRITICAL FIX: Check for non-alphabetic characters before calling dictionary
            // DictionaryLib throws "non alphabetic input" exception for any non-letter characters
            if (!word.All(char.IsLetter))
            {
                DebugHelper.Log($"Word validation: '{word}' contains non-alphabetic characters - marking as not a word");
                return FoundWordType.SubWordNotAWord;
            }

            try
            {
                // For Wordament, we don't have a "grid" concept like WordScape, so we skip SubWordInGrid
                // All valid words in Wordament go directly to dictionary classification

                // Check if word is in small dictionary
                var isInSmallDict = _dictionaryService.IsWord(word, DictionaryType.Small);
                if (isInSmallDict)
                {
                    DebugHelper.Log($"Found '{word}' in small dictionary");
                    return FoundWordType.SubWordNotInGrid; // Using "not in grid" for small dictionary words
                }

                // Check if word is in large dictionary
                var isInLargeDict = _dictionaryService.IsWord(word, DictionaryType.Large);
                if (isInLargeDict)
                {
                    DebugHelper.Log($"Found '{word}' in large dictionary");
                    return FoundWordType.SubWordInLargeDictionary;
                }

                DebugHelper.Log($"'{word}' not found in any dictionary");
                return FoundWordType.SubWordNotAWord;
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Dictionary error validating '{word}': {ex.Message}");
                return FoundWordType.SubWordNotAWord;
            }
        }

        /// <summary>
        /// NEW: Check if word is valid (in any dictionary) - for UI feedback
        /// </summary>
        public bool IsValidWordForUI(string word, int minLength = 3)
        {
            if (string.IsNullOrEmpty(word) || word.Length < minLength)
                return false;

            var wordType = ValidateWordType(word);
            return wordType != FoundWordType.SubWordNotAWord;
        }

        private int CalculateWordScore(string word, List<GridPosition> path, WordamentGrid grid)
        {
            int letterScore = 0;
            int wordMultiplier = 1;

            // Calculate letter scores with multipliers
            for (int i = 0; i < path.Count; i++)
            {
                var pos = path[i];
                var cell = grid.GetCell(pos.X, pos.Y);
                
                int basePoints = cell.GetPointValue();
                int letterMultiplier = cell.GetScoreMultiplier();
                int cellWordMultiplier = cell.GetWordMultiplier();

                letterScore += basePoints * letterMultiplier;
                wordMultiplier *= cellWordMultiplier;
            }

            // Apply word length bonus
            int lengthBonus = word.Length >= 6 ? (word.Length - 5) * 5 : 0;
            
            int totalScore = (letterScore + lengthBonus) * wordMultiplier;

            DebugHelper.Log($"Score calculation for '{word}': base={letterScore}, length_bonus={lengthBonus}, multiplier={wordMultiplier}, total={totalScore}");
            
            return Math.Max(totalScore, word.Length); // Minimum score equals word length
        }

        private bool IsRareWord(string word)
        {
            // Simple heuristic: words with uncommon letters or long words are considered rare
            var rareLetters = "JQXZ";
            bool hasRareLetter = word.Any(c => rareLetters.Contains(c));
            bool isLong = word.Length >= 7;
            
            return hasRareLetter || isLong;
        }

        public void UpdateGameTimer(WordamentGameState gameState, TimeSpan elapsed)
        {
            if (gameState.IsGameActive)
            {
                // Update elapsed time (always counting up)
                gameState.ElapsedTime = gameState.ElapsedTime.Add(elapsed);
                
                // Update remaining time only for Timer mode
                if (gameState.TimeRemaining > TimeSpan.Zero) // Timer mode
                {
                    gameState.TimeRemaining = gameState.TimeRemaining.Subtract(elapsed);
                    if (gameState.TimeRemaining <= TimeSpan.Zero)
                    {
                        gameState.TimeRemaining = TimeSpan.Zero;
                        gameState.IsGameActive = false;
                        MarkLongestWords(gameState);
                        DebugHelper.Log("Game time expired - marking longest words");
                    }
                }
                // For LongWord mode, game continues until original word is found
            }
        }

        public void CheckLongWordGameComplete(WordamentGameState gameState, WordamentSettings settings)
        {
            if (settings.GameMode == WordamentGameMode.LongWord && !gameState.OriginalWordFound)
            {
                // Check if the original word was found
                var originalWordFound = gameState.FoundWords.Any(w => 
                    w.Word.Equals(gameState.OriginalWord, StringComparison.OrdinalIgnoreCase));
                
                if (originalWordFound)
                {
                    gameState.OriginalWordFound = true;
                    gameState.IsGameActive = false;
                    DebugHelper.Log($"LongWord game complete! Original word '{gameState.OriginalWord}' was found.");
                }
            }
        }

        private void MarkLongestWords(WordamentGameState gameState)
        {
            if (gameState.FoundWords.Count == 0) return;

            var maxLength = gameState.FoundWords.Max(w => w.Word.Length);
            var longestWords = gameState.FoundWords.Where(w => w.Word.Length == maxLength);
            
            foreach (var word in longestWords)
            {
                word.IsLongestWord = true;
            }

            DebugHelper.Log($"Marked {longestWords.Count()} words of length {maxLength} as longest");
        }

        public List<GridPosition> GetAdjacentPositions(GridPosition position, WordamentGrid grid, List<GridPosition> excludePositions)
        {
            var adjacent = new List<GridPosition>();

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue; // Skip the current position

                    int newX = position.X + dx;
                    int newY = position.Y + dy;

                    if (newX >= 0 && newX < WordamentGrid.Size && 
                        newY >= 0 && newY < WordamentGrid.Size)
                    {
                        var newPos = new GridPosition(newX, newY);
                        if (!excludePositions.Any(p => p.X == newPos.X && p.Y == newPos.Y))
                        {
                            adjacent.Add(newPos);
                        }
                    }
                }
            }

            return adjacent;
        }

        public void UpdateSelection(List<GridPosition> path, WordamentGrid grid)
        {
            ClearSelection(grid);

            for (int i = 0; i < path.Count; i++)
            {
                var pos = path[i];
                var cell = grid.GetCell(pos.X, pos.Y);
                if (cell.X != -1)
                {
                    cell.IsSelected = true;
                    cell.SelectionOrder = i; // Track selection order for visual feedback
                }
            }
        }

        public void HighlightValidMoves(GridPosition currentPosition, List<GridPosition> currentPath, WordamentGrid grid)
        {
            // Clear all highlights first
            for (int x = 0; x < WordamentGrid.Size; x++)
            {
                for (int y = 0; y < WordamentGrid.Size; y++)
                {
                    grid.Cells[x, y].IsHighlighted = false;
                    grid.Cells[x, y].IsValidMove = false;
                }
            }

            if (currentPosition.X == -1) return;

            var validMoves = GetAdjacentPositions(currentPosition, grid, currentPath);
            foreach (var pos in validMoves)
            {
                var cell = grid.GetCell(pos.X, pos.Y);
                if (cell.X != -1)
                {
                    cell.IsHighlighted = true;
                    cell.IsValidMove = true; // Mark as valid move for touch/drag UI
                }
            }
        }

        public void ClearSelection(WordamentGrid grid)
        {
            for (int x = 0; x < WordamentGrid.Size; x++)
            {
                for (int y = 0; y < WordamentGrid.Size; y++)
                {
                    grid.Cells[x, y].ResetState(); // Use new reset method
                }
            }
        }

        public async Task<List<string>> FindAllValidWordsAsync(WordamentGrid grid, int minLength = 3)
        {
            var allWords = new HashSet<string>();
            
            // Search from each cell as starting position
            for (int startX = 0; startX < WordamentGrid.Size; startX++)
            {
                for (int startY = 0; startY < WordamentGrid.Size; startY++)
                {
                    var startPos = new GridPosition(startX, startY);
                    var foundWords = await SearchWordsFromPosition(startPos, grid, minLength);
                    foreach (var word in foundWords)
                    {
                        allWords.Add(word);
                    }
                }
            }

            DebugHelper.Log($"Found {allWords.Count} total valid words in grid");
            return allWords.OrderBy(w => w.Length).ThenBy(w => w).ToList();
        }

        private async Task<List<string>> SearchWordsFromPosition(GridPosition startPos, WordamentGrid grid, int minLength)
        {
            var foundWords = new List<string>();
            var visited = new HashSet<GridPosition>();
            var currentPath = new List<GridPosition>();
            
            await SearchRecursive(startPos, grid, visited, currentPath, foundWords, minLength);
            
            return foundWords;
        }

        private async Task SearchRecursive(GridPosition pos, WordamentGrid grid, HashSet<GridPosition> visited,
            List<GridPosition> currentPath, List<string> foundWords, int minLength)
        {
            if (visited.Contains(pos)) return;

            visited.Add(pos);
            currentPath.Add(pos);

            var currentWord = GetWordFromPath(currentPath, grid);
            
            // Check if current word is valid and long enough
            if (currentWord.Length >= minLength && IsValidWord(currentWord, minLength))
            {
                if (!foundWords.Contains(currentWord))
                {
                    foundWords.Add(currentWord);
                }
            }

            // Continue searching if we haven't reached maximum reasonable length
            if (currentPath.Count < 10)
            {
                var adjacentPositions = GetAdjacentPositions(pos, grid, new List<GridPosition>());
                foreach (var nextPos in adjacentPositions)
                {
                    if (!visited.Contains(nextPos))
                    {
                        await SearchRecursive(nextPos, grid, visited, currentPath, foundWords, minLength);
                    }
                }
            }

            // Backtrack
            visited.Remove(pos);
            currentPath.RemoveAt(currentPath.Count - 1);

            // Yield occasionally to prevent UI blocking
            if (currentPath.Count == 0)
            {
                await Task.Yield();
            }
        }

        public WordamentSettings GetDefaultSettings()
        {
            return new WordamentSettings
            {
                GameDurationMinutes = 3,
                MinWordLength = 3,
                ShowTimer = true,
                AllowDiagonalMovement = true,
                ShowWordScores = true,
                IsDebugEnabled = DebugHelper.IsDebugEnabled,
                GameMode = WordamentGameMode.Timer
            };
        }

        /// <summary>
        /// Reset random seed when debug mode changes for consistent results
        /// </summary>
        public void OnDebugModeChanged()
        {
            InitializeRandom();
            DebugHelper.Log($"Wordament random seed reset. Debug enabled: {DebugHelper.IsDebugEnabled}");
        }

        /// <summary>
        /// Check if a position can be added to the current path (for touch/drag support)
        /// </summary>
        public bool CanAddToPath(GridPosition position, List<GridPosition> currentPath, WordamentGrid grid)
        {
            if (currentPath.Count == 0) return true; // Can always start with any position

            var lastPosition = currentPath.Last();
            
            // Can't add if position is already in path (except for backtracking)
            if (currentPath.Contains(position))
            {
                // Allow backtracking to the previous position
                return currentPath.Count > 1 && currentPath[currentPath.Count - 2].Equals(position);
            }

            // Must be adjacent to the last position
            return grid.AreAdjacent(lastPosition, position);
        }

        /// <summary>
        /// Get visual hints for valid next moves (for touch/drag UI feedback)
        /// </summary>
        public List<GridPosition> GetValidNextMoves(List<GridPosition> currentPath, WordamentGrid grid)
        {
            if (currentPath.Count == 0) 
            {
                // If no path started, all positions are valid starting points
                var allPositions = new List<GridPosition>();
                for (int x = 0; x < WordamentGrid.Size; x++)
                {
                    for (int y = 0; y < WordamentGrid.Size; y++)
                    {
                        allPositions.Add(new GridPosition(x, y));
                    }
                }
                return allPositions;
            }

            var lastPosition = currentPath.Last();
            var validMoves = GetAdjacentPositions(lastPosition, grid, currentPath);
            
            // Also include the previous position for backtracking
            if (currentPath.Count > 1)
            {
                var previousPosition = currentPath[currentPath.Count - 2];
                if (!validMoves.Contains(previousPosition))
                {
                    validMoves.Add(previousPosition);
                }
            }

            return validMoves;
        }

        /// <summary>
        /// Get statistics about the current word being formed (for UI feedback)
        /// </summary>
        public WordFormationInfo GetWordFormationInfo(List<GridPosition> path, WordamentGrid grid, WordamentSettings settings)
        {
            var word = GetWordFromPath(path, grid);
            var isValid = !string.IsNullOrEmpty(word) && IsValidWord(word, settings.MinWordLength);
            var score = 0;

            if (isValid && path.Count > 0)
            {
                score = CalculateWordScore(word, path, grid);
            }

            return new WordFormationInfo
            {
                Word = word,
                IsValid = isValid,
                Score = score,
                Length = word.Length,
                IsMinLength = word.Length >= settings.MinWordLength,
                IsRare = !string.IsNullOrEmpty(word) && IsRareWord(word)
            };
        }

        /// <summary>
        /// Check if a word path creates a valid connection pattern
        /// </summary>
        public bool IsValidConnectionPattern(List<GridPosition> path, WordamentGrid grid)
        {
            if (path.Count <= 1) return true;

            // Check that each step in the path connects to the next
            for (int i = 1; i < path.Count; i++)
            {
                if (!grid.AreAdjacent(path[i - 1], path[i]))
                {
                    return false;
                }
            }

            // Check that no position is used more than once
            var uniquePositions = new HashSet<GridPosition>(path);
            return uniquePositions.Count == path.Count;
        }

        /// <summary>
        /// Enhanced drag support - handle mouse/touch coordinate to cell conversion
        /// </summary>
        public GridPosition? GetCellFromScreenCoordinates(double screenX, double screenY, object gridBounds)
        {
            // This method would typically be called from JavaScript
            // The actual implementation is in JavaScript getWordamentCellFromCoordinates
            // This is a placeholder for the C# interface
            return null;
        }

        /// <summary>
        /// Optimize path for better user experience during drag operations
        /// </summary>
        public List<GridPosition> OptimizeDragPath(List<GridPosition> rawPath, WordamentGrid grid)
        {
            if (rawPath.Count <= 2) return rawPath;

            var optimizedPath = new List<GridPosition> { rawPath[0] };

            for (int i = 1; i < rawPath.Count; i++)
            {
                var lastInOptimized = optimizedPath.Last();
                var current = rawPath[i];

                // Only add if it's actually adjacent and not already in optimized path
                if (grid.AreAdjacent(lastInOptimized, current) && 
                    !optimizedPath.Contains(current))
                {
                    optimizedPath.Add(current);
                }
                else if (optimizedPath.Count > 1 && optimizedPath[optimizedPath.Count - 2].Equals(current))
                {
                    // Allow backtracking to previous position
                    optimizedPath.RemoveAt(optimizedPath.Count - 1);
                }
            }

            return optimizedPath;
        }

        /// <summary>
        /// Get detailed feedback for drag operations
        /// </summary>
        public DragFeedback GetDragFeedback(List<GridPosition> currentPath, WordamentGrid grid, WordamentSettings settings)
        {
            var word = GetWordFromPath(currentPath, grid);
            var isValidPath = IsValidPath(currentPath, grid);
            var isValidWord = !string.IsNullOrEmpty(word) && IsValidWord(word, settings.MinWordLength);
            var nextMoves = GetValidNextMoves(currentPath, grid);

            return new DragFeedback
            {
                CurrentWord = word,
                IsValidPath = isValidPath,
                IsValidWord = isValidWord,
                PathLength = currentPath.Count,
                ValidNextMoves = nextMoves,
                Score = isValidWord ? CalculateWordScore(word, currentPath, grid) : 0,
                CanSubmit = isValidWord && currentPath.Count >= settings.MinWordLength
            };
        }

        /// <summary>
        /// Use a hint to reveal part of the original word
        /// </summary>
        public string UseHint(WordamentGameState gameState)
        {
            if (gameState.GameMode != WordamentGameMode.LongWord || string.IsNullOrEmpty(gameState.OriginalWord))
            {
                return ""; // Hints only available in LongWord mode
            }

            gameState.HintsUsed++;
            var hintsToShow = Math.Min(gameState.HintsUsed, gameState.OriginalWord.Length);
            var hint = gameState.OriginalWord.Substring(0, hintsToShow);
            gameState.CurrentHint = hint;
            
            DebugHelper.Log($"Hint used: showing first {hintsToShow} letters of '{gameState.OriginalWord}' -> '{hint}'");
            return hint;
        }

        /// <summary>
        /// Check if hints are available for the current game mode
        /// </summary>
        public bool AreHintsAvailable(WordamentGameState gameState)
        {
            return gameState.GameMode == WordamentGameMode.LongWord && 
                   !string.IsNullOrEmpty(gameState.OriginalWord) &&
                   gameState.HintsUsed < gameState.OriginalWord.Length;
        }

        /// <summary>
        /// Get the current hint text for display
        /// </summary>
        public string GetCurrentHint(WordamentGameState gameState)
        {
            if (gameState.GameMode != WordamentGameMode.LongWord || gameState.HintsUsed == 0)
            {
                return "";
            }
            return gameState.CurrentHint;
        }
    }

    /// <summary>
    /// Information about word formation in progress (for UI feedback)
    /// </summary>
    public class WordFormationInfo
    {
        public string Word { get; set; } = "";
        public bool IsValid { get; set; }
        public int Score { get; set; }
        public int Length { get; set; }
        public bool IsMinLength { get; set; }
        public bool IsRare { get; set; }
    }

    /// <summary>
    /// Feedback for drag operations (for enhanced desktop support)
    /// </summary>
    public class DragFeedback
    {
        public string CurrentWord { get; set; } = "";
        public bool IsValidPath { get; set; }
        public bool IsValidWord { get; set; }
        public int PathLength { get; set; }
        public List<GridPosition> ValidNextMoves { get; set; } = new();
        public int Score { get; set; }
        public bool CanSubmit { get; set; }
    }
}