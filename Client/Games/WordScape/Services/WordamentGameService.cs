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
            DebugHelper.Log($"Creating new Wordament game - Duration: {settings.GameDurationMinutes}min, MinLength: {settings.MinWordLength}");

            var gameState = new WordamentGameState
            {
                GameStartTime = DateTime.Now,
                TimeRemaining = TimeSpan.FromMinutes(settings.GameDurationMinutes),
                IsGameActive = true,
                Score = 0,
                FoundWords = new HashSet<WordamentFoundWord>(),
                SelectedPath = new List<GridPosition>(),
                CurrentPath = ""
            };

            gameState.Grid.GenerateRandomGrid(_random, _dictionaryService);

            DebugHelper.Log($"Generated 4x4 grid for Wordament");
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
            // First check basic requirements
            if (string.IsNullOrEmpty(word) || word.Length < minLength)
                return false;

            // CRITICAL FIX: Check for non-alphabetic characters before calling dictionary
            // DictionaryLib throws "non alphabetic input" exception for any non-letter characters
            if (!word.All(char.IsLetter))
            {
                DebugHelper.Log($"Word validation: '{word}' contains non-alphabetic characters - skipping dictionary check");
                return false;
            }

            try
            {
                bool isValid = _dictionaryService.IsWord(word, DictionaryType.Small);
                DebugHelper.Log($"Word validation: '{word}' = {isValid}");
                return isValid;
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Dictionary error validating '{word}': {ex.Message}");
                return false;
            }
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

            if (!IsValidWord(word, settings.MinWordLength))
            {
                DebugHelper.Log($"'{word}' is not a valid dictionary word or too short");
                return null;
            }

            var score = CalculateWordScore(word, path, grid);
            var foundWord = new WordamentFoundWord
            {
                Word = word,
                Path = new List<GridPosition>(path),
                Score = score,
                FoundAt = DateTime.Now,
                IsRareWord = IsRareWord(word),
                IsLongestWord = false // Will be determined after all words are found
            };

            DebugHelper.Log($"Valid word submitted: '{word}' for {score} points");
            return foundWord;
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
            gameState.TimeRemaining = gameState.TimeRemaining.Subtract(elapsed);
            if (gameState.TimeRemaining <= TimeSpan.Zero)
            {
                gameState.TimeRemaining = TimeSpan.Zero;
                gameState.IsGameActive = false;
                MarkLongestWords(gameState);
                DebugHelper.Log("Game time expired - marking longest words");
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
                IsDebugEnabled = DebugHelper.IsDebugEnabled
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