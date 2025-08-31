using DictionaryLib;
using WordScapeBlazorWasm.Models;

namespace WordScapeBlazorWasm.Services
{
    public class WordamentGameService
    {
        private readonly DictionaryLib.DictionaryLib _dictionary;
        private readonly DebugHelper _debugHelper;
        private Random _random;

        public WordamentGameService(DebugHelper debugHelper)
        {
            _dictionary = new DictionaryLib.DictionaryLib(DictionaryType.Small);
            _debugHelper = debugHelper;
            InitializeRandom();
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

            gameState.Grid.GenerateRandomGrid(_random);

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
            if (string.IsNullOrEmpty(word) || word.Length < minLength)
                return false;

            bool isValid = _dictionary.IsWord(word);
            DebugHelper.Log($"Word validation: '{word}' = {isValid}");
            return isValid;
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

        public void ClearSelection(WordamentGrid grid)
        {
            for (int x = 0; x < WordamentGrid.Size; x++)
            {
                for (int y = 0; y < WordamentGrid.Size; y++)
                {
                    grid.Cells[x, y].IsSelected = false;
                    grid.Cells[x, y].IsHighlighted = false;
                }
            }
        }

        public void UpdateSelection(List<GridPosition> path, WordamentGrid grid)
        {
            ClearSelection(grid);

            foreach (var pos in path)
            {
                var cell = grid.GetCell(pos.X, pos.Y);
                if (cell.X != -1)
                {
                    cell.IsSelected = true;
                }
            }
        }

        public void HighlightValidMoves(GridPosition currentPosition, List<GridPosition> currentPath, WordamentGrid grid)
        {
            ClearHighlights(grid);

            if (currentPosition.X == -1) return;

            var validMoves = GetAdjacentPositions(currentPosition, grid, currentPath);
            foreach (var pos in validMoves)
            {
                var cell = grid.GetCell(pos.X, pos.Y);
                if (cell.X != -1)
                {
                    cell.IsHighlighted = true;
                }
            }
        }

        private void ClearHighlights(WordamentGrid grid)
        {
            for (int x = 0; x < WordamentGrid.Size; x++)
            {
                for (int y = 0; y < WordamentGrid.Size; y++)
                {
                    grid.Cells[x, y].IsHighlighted = false;
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
    }
}