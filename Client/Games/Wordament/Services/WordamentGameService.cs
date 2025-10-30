using DictionaryLib;
using WordScapeBlazorWasm.Models;
using System.Linq;
using System.Collections.Concurrent;
using System.Text;

namespace WordScapeBlazorWasm.Services
{
    public class WordamentGameService
    {
        private readonly IDictionaryService _dictionaryService;
        private readonly DebugHelper _debugHelper;
        private readonly WordamentGridWordFinder _gridWordFinder;
        private readonly RandomService _randomService; // ?? Inject centralized Random service
        private Random _random;
        private bool _isDebugEnabled = false; // Track current debug state
        private int _gameCounter = 0; // Counter to ensure unique games even in debug mode

        public WordamentGameService(IDictionaryService dictionaryService, DebugHelper debugHelper, WordamentGridWordFinder gridWordFinder, RandomService randomService)
        {
            _dictionaryService = dictionaryService;
            _debugHelper = debugHelper;
            _gridWordFinder = gridWordFinder;
            _randomService = randomService;
            _isDebugEnabled = DebugHelper.IsDebugEnabled;
            
            // ?? CRITICAL FIX: Get shared Random instance from centralized service
            _random = _randomService.GetRandom();
            
            LogDebug($"?? WordamentGameService initialized with debug mode: {_isDebugEnabled}");
            LogDebug($"?? Using shared Random: {_randomService.GetStateDescription()}");
        }

        /// <summary>
        /// Called when debug mode changes to update random seed
        /// </summary>
        public void OnDebugModeChanged()
        {
            _isDebugEnabled = DebugHelper.IsDebugEnabled;
            _randomService.Reset(); // ?? Reset centralized Random service
            _random = _randomService.GetRandom(); // ?? Get fresh instance
            
            LogDebug($"WordamentGameService: Debug mode changed to {_isDebugEnabled}");
            LogDebug($"WordamentGameService: {_randomService.GetStateDescription()}");
        }

        public WordamentGameState CreateNewGame(WordamentSettings settings)
        {
            // Increment game counter for tracking
            _gameCounter++;
            
            // Update debug state if it changed
            if (_isDebugEnabled != settings.IsDebugEnabled)
            {
                _isDebugEnabled = settings.IsDebugEnabled;
                DebugHelper.SetDebugMode(_isDebugEnabled);
            }
            
            // CRITICAL FIX: Always reset to seed 1 in debug mode to ensure reproducibility
            if (_isDebugEnabled)
            {
                _random = new Random(1); // Force seed 1 for each game in debug mode
                LogDebug($"Creating new Wordament game #{_gameCounter} with DEBUG SEED 1 for reproducibility");
            }
            else
            {
                _random = new Random(); // Random seed for normal gameplay
                LogDebug($"Creating new Wordament game #{_gameCounter} with random seed");
            }
            
            LogDebug($"Creating new Wordament game #{_gameCounter} - Mode: {settings.GameMode}, Duration: {settings.GameDurationMinutes}min, MinLength: {settings.MinWordLength}, Debug: {_isDebugEnabled}");

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

            LogDebug($"Generated 4x4 grid for Wordament with original word: {gameState.OriginalWord}");
            LogGrid(gameState.Grid);

            return gameState;
        }

        private void LogGrid(WordamentGrid grid)
        {
            if (!_isDebugEnabled) return;

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
                    LogDebug($"Invalid path: {path[i - 1]} and {path[i]} are not adjacent");
                    return false;
                }

                // Check for duplicate positions (can't reuse same cell)
                for (int j = 0; j < i; j++)
                {
                    if (path[j].X == path[i].X && path[j].Y == path[i].Y)
                    {
                        LogDebug($"Invalid path: Position {path[i]} used multiple times");
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
                LogDebug("Invalid path - no word formed");
                return null;
            }

            // Check minimum length requirement
            if (word.Length < settings.MinWordLength)
            {
                LogDebug($"'{word}' is too short (minimum length: {settings.MinWordLength})");
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
                LogDebug($"ORIGINAL WORD FOUND! '{word}' matches grid original word '{grid.OriginalWord}'");
                foundWord.IsLongestWord = true; // Mark as special
            }

            LogDebug($"Word submitted: '{word}' classified as {wordType} for {score} points");
            return foundWord;
        }
        
        /// <summary>
        /// NEW: Update game state with last submitted word information for persistent display
        /// </summary>
        public void UpdateLastSubmittedWord(WordamentGameState gameState, string word, FoundWordType wordType)
        {
            gameState.LastSubmittedWord = word;
            gameState.LastSubmittedWordType = wordType;
            gameState.LastSubmittedAt = DateTime.Now;
            gameState.LastSubmittedWordWasAlreadyFound = false; // Normal submission
            
            LogDebug($"Updated last submitted word: '{word}' with type {wordType}");
        }
        
        /// <summary>
        /// NEW: Update game state for already found words (white background like WordScape)
        /// </summary>
        public void UpdateLastSubmittedWordAsAlreadyFound(WordamentGameState gameState, string word)
        {
            gameState.LastSubmittedWord = word;
            gameState.LastSubmittedWordType = FoundWordType.SubWordNotAWord; // Not relevant for already found words
            gameState.LastSubmittedAt = DateTime.Now;
            gameState.LastSubmittedWordWasAlreadyFound = true; // Mark as already found for white background
            
            LogDebug($"Updated last submitted word as already found: '{word}'");
        }

        /// <summary>
        /// OPTIMIZED: Validate word type using WordScape logic with performance enhancements
        /// </summary>
        public FoundWordType ValidateWordType(string word)
        {
            // FAST PATH: Early validation checks
            if (string.IsNullOrEmpty(word))
            {
                return FoundWordType.SubWordNotAWord;
            }

            // PERFORMANCE: Check length and alphabetic in one pass
            if (word.Length < 2 || word.Length > 20) // Reasonable bounds
            {
                return FoundWordType.SubWordNotAWord;
            }

            // CRITICAL FIX: Check for non-alphabetic characters before calling dictionary
            // DictionaryLib throws "non alphabetic input" exception for any non-letter characters
            for (int i = 0; i < word.Length; i++)
            {
                if (!char.IsLetter(word[i]))
                {
                    LogDebug($"Word validation: '{word}' contains non-alphabetic characters - marking as not a word");
                    return FoundWordType.SubWordNotAWord;
                }
            }

            try
            {
                // For Wordament, we don't have a "grid" concept like WordScape, so we skip SubWordInGrid
                // All valid words in Wordament go directly to dictionary classification

                // Check if word is in small dictionary first (more common)
                var isInSmallDict = _dictionaryService.IsWord(word, DictionaryType.Small);
                if (isInSmallDict)
                {
                    LogDebug($"Found '{word}' in small dictionary");
                    return FoundWordType.SubWordNotInGrid; // Using "not in grid" for small dictionary words
                }

                // Check if word is in large dictionary
                var isInLargeDict = _dictionaryService.IsWord(word, DictionaryType.Large);
                if (isInLargeDict)
                {
                    LogDebug($"Found '{word}' in large dictionary");
                    return FoundWordType.SubWordInLargeDictionary;
                }

                LogDebug($"'{word}' not found in any dictionary");
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

            LogDebug($"Score calculation for '{word}': base={letterScore}, length_bonus={lengthBonus}, multiplier={wordMultiplier}, total={totalScore}");
            
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
                        LogDebug("Game time expired - marking longest words");
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
                    LogDebug($"LongWord game complete! Original word '{gameState.OriginalWord}' was found.");
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

            LogDebug($"Marked {longestWords.Count()} words of length {maxLength} as longest");
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

        public WordamentSettings GetDefaultSettings()
        {
            return new WordamentSettings
            {
                GameDurationMinutes = 3,
                MinWordLength = 3,
                ShowTimer = true,
                ShowWordScores = true,
                IsDebugEnabled = DebugHelper.IsDebugEnabled,
                GameMode = WordamentGameMode.LongWord // Changed from Timer to LongWord
            };
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
        /// FIND ALL WORDS: Find all valid words in the grid using SeekWord method from both small and large dictionaries
        /// Now delegates to WordamentGridWordFinder for optimized search
        /// </summary>
        public async Task<List<WordamentFoundWord>> FindAllWordsInGridUsingSeekWordAsync(WordamentGrid grid, int minLength = 3, int maxLength = 16)
        {
            return await _gridWordFinder.FindAllWordsInGridUsingSeekWordAsync(grid, minLength, maxLength);
        }

        /// <summary>
        /// Get feedback for drag operations (for enhanced desktop support)
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
            
            LogDebug($"Hint used: showing first {hintsToShow} letters of '{gameState.OriginalWord}' -> '{hint}'");
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

        /// <summary>
        /// Conditional logging method for debug messages
        /// </summary>
        private void LogDebug(string message)
        {
            if (_isDebugEnabled)
            {
                DebugHelper.Log(message);
            }
        }
    }
}