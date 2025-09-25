using DictionaryLib;
using WordScapeBlazorWasm.Models;
using System.Linq;
using System.Collections.Concurrent;
using System.Text;

namespace WordScapeBlazorWasm.Services
{
    /// <summary>
    /// High-performance prefix trie for word validation and prefix pruning
    /// </summary>
    public class PrefixTrie
    {
        private readonly TrieNode _root;
        public int WordCount { get; private set; }

        public PrefixTrie()
        {
            _root = new TrieNode();
            WordCount = 0;
        }

        public void AddWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return;

            var current = _root;
            foreach (char c in word.ToUpper())
            {
                if (!current.Children.ContainsKey(c))
                {
                    current.Children[c] = new TrieNode();
                }
                current = current.Children[c];
            }
            
            if (!current.IsEndOfWord)
            {
                current.IsEndOfWord = true;
                WordCount++;
            }
        }

        public (bool HasPrefix, bool IsCompleteWord) SearchPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return (true, false);

            var current = _root;
            // PERFORMANCE: Use ReadOnlySpan to avoid string allocations
            var span = prefix.AsSpan();
            
            for (int i = 0; i < span.Length; i++)
            {
                var c = char.ToUpper(span[i]); // Ensure uppercase
                if (!current.Children.TryGetValue(c, out var nextNode))
                {
                    return (false, false);
                }
                current = nextNode;
            }

            return (true, current.IsEndOfWord);
        }

        private class TrieNode
        {
            public Dictionary<char, TrieNode> Children { get; }
            public bool IsEndOfWord { get; set; }

            public TrieNode()
            {
                Children = new Dictionary<char, TrieNode>();
                IsEndOfWord = false;
            }
        }
    }

    public class WordamentGameService
    {
        private readonly IDictionaryService _dictionaryService;
        private readonly DebugHelper _debugHelper;
        private Random _random;
        
        // PERFORMANCE: Cache tries to avoid rebuilding on every search
        private static PrefixTrie? _cachedSmallDictTrie;
        private static PrefixTrie? _cachedLargeDictTrie;
        private static readonly object _trieCacheLock = new object();

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
                    DebugHelper.Log($"Word validation: '{word}' contains non-alphabetic characters - marking as not a word");
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
            if (currentWord.Length >= minLength)
            {
                var partial = _dictionaryService.SeekWord(currentWord, out var compesult);
                if (!string.IsNullOrEmpty(partial) && compesult == 0)
                {
                    if (!foundWords.Contains(currentWord))
                    {
                        foundWords.Add(currentWord);
                    }
                }
                else
                {
                    if (!partial.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                    {
                        // No longer a valid prefix, backtrack
                        visited.Remove(pos);
                        currentPath.RemoveAt(currentPath.Count - 1);
                        return;
                    }
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
        /// HIGHLY OPTIMIZED: Find all valid words using trie-based prefix pruning for maximum performance
        /// Uses tree pruning to avoid exploring invalid prefixes, dramatically reducing search space
        /// </summary>
        public async Task<List<WordamentFoundWord>> FindAllValidWordsInGridAsync(WordamentGrid grid, int minLength = 3, int maxLength = 16)
        {
            var allFoundWords = new List<WordamentFoundWord>();
            var uniqueWords = new HashSet<string>();
            
            DebugHelper.Log($"Starting optimized grid word search with tree pruning (min: {minLength}, max: {maxLength})");
            
            try
            {
                // PERFORMANCE: Use cached tries instead of building every time
                var (smallDictTrie, largeDictTrie) = await GetCachedTriesAsync(minLength, maxLength);
                
                var processedStartPositions = 0;
                
                // Search from each cell as starting position
                for (int startX = 0; startX < WordamentGrid.Size; startX++)
                {
                    for (int startY = 0; startY < WordamentGrid.Size; startY++)
                    {
                        var startPos = new GridPosition(startX, startY);
                        var visited = new bool[WordamentGrid.Size, WordamentGrid.Size];
                        var currentPath = new List<GridPosition>();
                        
                        await SearchWithTriePruning(
                            startPos, grid, visited, currentPath,
                            allFoundWords, uniqueWords, minLength, maxLength,
                            smallDictTrie, largeDictTrie
                        );
                        
                        processedStartPositions++;
                        
                        // Yield every 4 starting positions to keep UI responsive
                        if (processedStartPositions % 4 == 0)
                        {
                            await Task.Yield();
                        }
                    }
                }
                
                DebugHelper.Log($"Optimized search complete: found {allFoundWords.Count} unique words from {processedStartPositions} starting positions");
                
                // Sort by score descending, then by length descending, then alphabetically
                var sortedWords = allFoundWords
                    .OrderByDescending(w => w.Score)
                    .ThenByDescending(w => w.Word.Length)
                    .ThenBy(w => w.Word)
                    .ToList();
                
                return sortedWords;
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Error in optimized grid word search: {ex.Message}");
                return allFoundWords;
            }
        }
        
        /// <summary>
        /// OPTIMIZED: Recursive search with trie-based prefix pruning - dramatically faster than dictionary lookups
        /// </summary>
        private async Task SearchWithTriePruning(
            GridPosition pos, WordamentGrid grid, bool[,] visited, List<GridPosition> currentPath,
            List<WordamentFoundWord> results, HashSet<string> uniqueWords, 
            int minLength, int maxLength, PrefixTrie smallTrie, PrefixTrie largeTrie)
        {
            if (pos.X < 0 || pos.X >= WordamentGrid.Size || pos.Y < 0 || pos.Y >= WordamentGrid.Size)
                return;
                
            if (visited[pos.X, pos.Y])
                return;
                
            // Add current position to path
            visited[pos.X, pos.Y] = true;
            currentPath.Add(pos);
            
            // Get current word prefix
            var currentWord = GetWordFromPath(currentPath, grid);
            
            // CRITICAL OPTIMIZATION: Use trie lookup instead of dictionary calls
            var smallTrieResult = smallTrie.SearchPrefix(currentWord);
            var largeTrieResult = largeTrie.SearchPrefix(currentWord);
            
            // If neither trie has this prefix, prune this entire branch
            if (smallTrieResult.HasPrefix == false && largeTrieResult.HasPrefix == false)
            {
                // Dead end - no words start with this prefix, so prune the entire subtree
                visited[pos.X, pos.Y] = false;
                currentPath.RemoveAt(currentPath.Count - 1);
                return;
            }
            
            // Check if current word is complete and valid
            if (currentWord.Length >= minLength && currentWord.Length <= maxLength)
            {
                FoundWordType? wordType = null;
                
                // Check small dictionary first (higher priority)
                if (smallTrieResult.IsCompleteWord)
                {
                    wordType = FoundWordType.SubWordNotInGrid;
                }
                else if (largeTrieResult.IsCompleteWord)
                {
                    wordType = FoundWordType.SubWordInLargeDictionary;
                }
                
                // Add word if it's valid and not already found
                if (wordType.HasValue && !uniqueWords.Contains(currentWord))
                {
                    uniqueWords.Add(currentWord);
                    
                    var foundWord = new WordamentFoundWord
                    {
                        Word = currentWord,
                        Path = new List<GridPosition>(currentPath),
                        Score = CalculateWordScore(currentWord, currentPath, grid),
                        FoundAt = DateTime.Now,
                        IsRareWord = IsRareWord(currentWord),
                        WordType = wordType.Value
                    };
                    
                    results.Add(foundWord);
                }
            }
            
            // Continue searching if we haven't reached max length and prefix is still valid
            if (currentPath.Count < maxLength && (smallTrieResult.HasPrefix || largeTrieResult.HasPrefix))
            {
                // Search all adjacent positions
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        var nextPos = new GridPosition(pos.X + dx, pos.Y + dy);
                        
                        if (nextPos.X >= 0 && nextPos.X < WordamentGrid.Size && 
                            nextPos.Y >= 0 && nextPos.Y < WordamentGrid.Size &&
                            !visited[nextPos.X, nextPos.Y])
                        {
                            await SearchWithTriePruning(
                                nextPos, grid, visited, currentPath, 
                                results, uniqueWords, minLength, maxLength, smallTrie, largeTrie
                            );
                        }
                    }
                }
            }
            
            // Backtrack
            visited[pos.X, pos.Y] = false;
            currentPath.RemoveAt(currentPath.Count - 1);
        }
        
        /// <summary>
        /// BUILD TRIE: Build or retrieve cached prefix tries
        /// </summary>
        private async Task<(PrefixTrie SmallTrie, PrefixTrie LargeTrie)> GetCachedTriesAsync(int minLength, int maxLength)
        {
            // Check if we have cached tries first
            lock (_trieCacheLock)
            {
                if (_cachedSmallDictTrie != null && _cachedLargeDictTrie != null)
                {
                    DebugHelper.Log($"Using cached prefix tries: Small={_cachedSmallDictTrie.WordCount}, Large={_cachedLargeDictTrie.WordCount}");
                    return (_cachedSmallDictTrie, _cachedLargeDictTrie);
                }
            }
            
            // Build tries outside the lock (async operations)
            DebugHelper.Log("Building prefix tries (will be cached for future use)...");
            var smallTrie = await BuildPrefixTrie(_dictionaryService.SmallDictionary.GetAllWords(), minLength, maxLength);
            var largeTrie = await BuildPrefixTrie(_dictionaryService.LargeDictionary.GetAllWords(), minLength, maxLength);
            
            // Cache the results
            lock (_trieCacheLock)
            {
                _cachedSmallDictTrie = smallTrie;
                _cachedLargeDictTrie = largeTrie;
            }
            
            DebugHelper.Log($"Built and cached prefix tries: Small={smallTrie.WordCount}, Large={largeTrie.WordCount}");
            return (smallTrie, largeTrie);
        }

        /// <summary>
        /// PERFORMANCE: Build an efficient prefix trie for fast prefix validation
        /// </summary>
        private async Task<PrefixTrie> BuildPrefixTrie(IEnumerable<string> words, int minLength, int maxLength)
        {
            var trie = new PrefixTrie();
            var processedWords = 0;
            
            foreach (var word in words)
            {
                var upperWord = word.ToUpper();
                if (upperWord.Length >= minLength && upperWord.Length <= maxLength && upperWord.All(char.IsLetter))
                {
                    trie.AddWord(upperWord);
                    processedWords++;
                    
                    // Yield periodically during trie construction
                    if (processedWords % 1000 == 0)
                    {
                        await Task.Yield();
                    }
                }
            }
            
            return trie;
        }

        /// <summary>
        /// ENHANCED: Performance-optimized subword calculation with parallel processing
        /// </summary>
        public async Task<List<WordamentFoundWord>> GetOriginalWordSubwordsAsync(string originalWord, int minLength = 3)
        {
            var subwords = new ConcurrentBag<WordamentFoundWord>();
            
            if (string.IsNullOrEmpty(originalWord))
            {
                return new List<WordamentFoundWord>();
            }

            DebugHelper.Log($"Finding subwords of '{originalWord}' using parallel processing...");

            try
            {
                var upperOriginalWord = originalWord.ToUpper();
                
                // Get all dictionary words in parallel
                var smallDictTask = Task.Run(() => 
                    _dictionaryService.SmallDictionary.GetAllWords()
                        .Where(word => word.Length >= minLength && word.Length <= originalWord.Length)
                        .Where(word => CanFormWordFromLettersOptimized(word.ToUpper(), upperOriginalWord))
                        .Select(word => new { Word = word.ToUpper(), Type = FoundWordType.SubWordNotInGrid })
                        .ToList()
                );
                
                var largeDictTask = Task.Run(() => 
                    _dictionaryService.LargeDictionary.GetAllWords()
                        .Where(word => word.Length >= minLength && word.Length <= originalWord.Length)
                        .Where(word => CanFormWordFromLettersOptimized(word.ToUpper(), upperOriginalWord))
                        .Select(word => new { Word = word.ToUpper(), Type = FoundWordType.SubWordInLargeDictionary })
                        .ToList()
                );
                
                await Task.WhenAll(smallDictTask, largeDictTask);
                
                var smallDictWords = await smallDictTask;
                var largeDictWords = await largeDictTask;
                
                // Combine results, preferring small dictionary classification
                var allCandidates = new Dictionary<string, FoundWordType>();
                
                foreach (var wordInfo in smallDictWords)
                {
                    allCandidates[wordInfo.Word] = wordInfo.Type;
                }
                
                foreach (var wordInfo in largeDictWords)
                {
                    if (!allCandidates.ContainsKey(wordInfo.Word))
                    {
                        allCandidates[wordInfo.Word] = wordInfo.Type;
                    }
                }
                
                DebugHelper.Log($"Found {allCandidates.Count} candidate subwords");
                
                // Process candidates in parallel batches
                var candidateList = allCandidates.ToList();
                var batchSize = Math.Max(1, candidateList.Count / Environment.ProcessorCount);
                
                // Create batches manually since Chunk might not be available in all .NET versions
                var batches = new List<List<KeyValuePair<string, FoundWordType>>>();
                for (int i = 0; i < candidateList.Count; i += batchSize)
                {
                    var batch = candidateList.Skip(i).Take(batchSize).ToList();
                    batches.Add(batch);
                }
                
                var tasks = batches.Select(batch => Task.Run(() =>
                {
                    foreach (var kvp in batch)
                    {
                        var word = kvp.Key;
                        var wordType = kvp.Value;
                        
                        var foundWord = new WordamentFoundWord
                        {
                            Word = word,
                            Path = new List<GridPosition>(), // No path for subwords
                            Score = 0, // No scoring for subword display  
                            FoundAt = DateTime.Now,
                            IsRareWord = IsRareWord(word),
                            IsLongestWord = false,
                            WordType = wordType
                        };
                        
                        subwords.Add(foundWord);
                    }
                }));
                
                await Task.WhenAll(tasks);
                
                // Apply filtering and return results
                var results = ApplyWordFiltering(subwords.ToList());
                
                DebugHelper.Log($"Parallel subword search complete: {results.Count} words after filtering");
                return results.OrderBy(w => w.Word.Length).ThenBy(w => w.Word).ToList();
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Error in parallel subword search: {ex.Message}");
                return new List<WordamentFoundWord>();
            }
        }
        
        /// <summary>
        /// OPTIMIZED: Find all subwords that can be formed from the original word letters using trie-based optimization
        /// This is different from FindAllValidWordsInGridAsync which searches for words by traversing the grid
        /// </summary>
        public async Task<List<WordamentFoundWord>> GetOriginalWordSubwordsOptimizedAsync(string originalWord, int minLength = 3)
        {
            if (string.IsNullOrEmpty(originalWord))
            {
                return new List<WordamentFoundWord>();
            }

            DebugHelper.Log($"Finding subwords of '{originalWord}' using optimized trie-based method...");

            try
            {
                var upperOriginalWord = originalWord.ToUpper();
                var maxLength = Math.Min(10, originalWord.Length); // Reasonable max length
                
                // Use cached tries if available, otherwise build them
                var (smallDictTrie, largeDictTrie) = await GetCachedTriesAsync(minLength, maxLength);
                
                DebugHelper.Log($"Using prefix tries for subword search: Small={smallDictTrie.WordCount} words, Large={largeDictTrie.WordCount} words");
                
                var foundWords = new List<WordamentFoundWord>();
                var uniqueWords = new HashSet<string>();
                
                // Create letter frequency map from original word
                var letterCounts = new Dictionary<char, int>();
                foreach (char c in upperOriginalWord)
                {
                    letterCounts[c] = letterCounts.GetValueOrDefault(c, 0) + 1;
                }
                
                // Search for all possible subwords using trie-guided generation
                await SearchSubwordsWithTrie("", letterCounts, minLength, maxLength, 
                    smallDictTrie, largeDictTrie, foundWords, uniqueWords);
                
                // Apply filtering to remove duplicate word forms
                var filteredWords = ApplyWordFiltering(foundWords);
                
                DebugHelper.Log($"Optimized subword search complete: found {filteredWords.Count} unique subwords from '{originalWord}'");
                
                return filteredWords.OrderBy(w => w.Word.Length).ThenBy(w => w.Word).ToList();
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Error in optimized subword search: {ex.Message}");
                return new List<WordamentFoundWord>();
            }
        }
        
        /// <summary>
        /// SEEKWORD HEURISTIC: Determine if we should continue searching from current word prefix
        /// This replaces trie prefix checking with heuristic rules similar to original CalcWordList
        /// </summary>
        private bool ShouldContinueSeekWordSearch(string currentWord, int pathLength, int maxLength)
        {
            // Always continue for very short prefixes
            if (pathLength <= 2) return true;
            
            // Stop if we've reached maximum length
            if (pathLength >= maxLength) return false;
            
            // HEURISTIC 1: Basic length and character validation
            if (currentWord.Length > 10) return false; // Reasonable upper bound
            if (!currentWord.All(char.IsLetter)) return false; // Must be all letters
            
            // HEURISTIC 2: Check for obviously invalid letter combinations
            if (currentWord.Length >= 2)
            {
                var lastTwo = currentWord.Length >= 2 ? currentWord.Substring(currentWord.Length - 2) : "";
                var invalidCombinations = new[] { "QZ", "XZ", "ZX", "JQ", "QQ", "XX", "ZZ" };
                if (invalidCombinations.Any(combo => lastTwo.Contains(combo)))
                {
                    return false;
                }
            }
            
            // HEURISTIC 3: For longer prefixes (4+ chars), do a quick sample validation
            if (currentWord.Length >= 4)
            {
                return IsLikelyValidPrefix(currentWord);
            }
            
            // HEURISTIC 4: Common word patterns that usually lead to words
            if (currentWord.Length >= 3)
            {
                var prefix3 = currentWord.Substring(0, Math.Min(3, currentWord.Length));
                // Check if the 3-letter prefix starts common words
                var commonPrefixes = new[] { "THE", "AND", "FOR", "ARE", "BUT", "NOT", "YOU", "ALL", "CAN", "HAD", "HER", "WAS", "ONE", "OUR", "OUT", "DAY", "GET", "USE", "MAN", "NEW", "NOW", "OLD", "SEE", "HIM", "TWO", "HOW", "ITS", "WHO" };
                return commonPrefixes.Any(word => word.StartsWith(prefix3));
            }
            
            return true; // Default to continuing for short prefixes
        }
        
        /// <summary>
        /// SEEKWORD VALIDATION: Quick heuristic check if a prefix is likely to lead to valid words
        /// Simulates the "SeekWord" concept without full dictionary traversal
        /// </summary>
        private bool IsLikelyValidPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix) || prefix.Length < 3) return true;
            
            // Sample approach: try adding common word endings to see if we get valid words
            var commonEndings = new[] { "S", "E", "D", "R", "T", "N", "ING", "ED", "ER", "EST", "LY" };
            
            foreach (var ending in commonEndings.Take(3)) // Only test first 3 for performance
            {
                var testWord = prefix + ending;
                if (testWord.Length >= 3 && testWord.Length <= 12)
                {
                    try
                    {
                        // Quick validation - if we can form a valid word by adding common endings,
                        // this prefix is likely to be part of other valid words
                        if (_dictionaryService.IsWord(testWord, DictionaryType.Small))
                        {
                            return true; // This prefix leads to at least one word
                        }
                    }
                    catch
                    {
                        // If dictionary check fails, continue with other endings
                        continue;
                    }
                }
            }
            
            // Additional check: common word patterns
            var vowels = "AEIOU";
            
            // Must have at least one vowel for words longer than 3 characters
            if (prefix.Length > 3 && !prefix.Any(c => vowels.Contains(c)))
            {
                return false;
            }
            
            // Alternating vowel/consonant pattern is usually good
            var hasGoodPattern = HasReasonableLetterPattern(prefix);
            
            return hasGoodPattern;
        }
        
        /// <summary>
        /// Check if a word prefix has a reasonable letter pattern (vowels/consonants)
        /// </summary>
        private bool HasReasonableLetterPattern(string word)
        {
            if (string.IsNullOrEmpty(word)) return true;
            
            var vowels = "AEIOU";
            int vowelCount = 0;
            int consonantCount = 0;
            int consecutiveConsonants = 0;
            int consecutiveVowels = 0;
            
            char lastChar = '\0';
            
            foreach (char c in word.ToUpper())
            {
                if (vowels.Contains(c))
                {
                    vowelCount++;
                    consecutiveVowels = vowels.Contains(lastChar) ? consecutiveVowels + 1 : 1;
                    consecutiveConsonants = 0;
                }
                else
                {
                    consonantCount++;
                    consecutiveConsonants = !vowels.Contains(lastChar) && lastChar != '\0' ? consecutiveConsonants + 1 : 1;
                    consecutiveVowels = 0;
                }
                
                // Too many consecutive consonants or vowels is suspicious
                if (consecutiveConsonants >= 3 || consecutiveVowels >= 3)
                {
                    return false;
                }
                
                lastChar = c;
            }
            
            // Reasonable balance between vowels and consonants
            return vowelCount >= 1 && consonantCount >= 1 && Math.Abs(vowelCount - consonantCount) <= 2;
        }
        
        /// <summary>
        /// OPTIMIZED: Check if a word can be formed from available letters using character frequency counting
        /// </summary>
        private bool CanFormWordFromLettersOptimized(string word, string availableLetters)
        {
            if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(availableLetters))
                return false;

            // Create frequency maps for both strings
            var availableFreq = new Dictionary<char, int>();
            var wordFreq = new Dictionary<char, int>();
            
            // Count available letters
            foreach (char c in availableLetters)
            {
                availableFreq[c] = availableFreq.GetValueOrDefault(c, 0) + 1;
            }
            
            // Count required letters
            foreach (char c in word)
            {
                wordFreq[c] = wordFreq.GetValueOrDefault(c, 0) + 1;
            }
            
            // Check if we have enough of each required letter
            foreach (var kvp in wordFreq)
            {
                char letter = kvp.Key;
                int requiredCount = kvp.Value;
                int availableCount = availableFreq.GetValueOrDefault(letter, 0);
                
                if (availableCount < requiredCount)
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Apply word filtering to remove duplicate word forms (plurals, etc.)
        /// Adapted from WordScape filtering logic
        /// </summary>
        private List<WordamentFoundWord> ApplyWordFiltering(List<WordamentFoundWord> words)
        {
            var filteredWords = new List<WordamentFoundWord>();
            var wordsSet = new HashSet<string>(words.Select(w => w.Word));

            foreach (var foundWord in words)
            {
                var word = foundWord.Word;
                bool shouldInclude = true;

                // Skip plurals if singular exists
                if (word.EndsWith("S") && word.Length > 3)
                {
                    var singular = word.Substring(0, word.Length - 1);
                    if (wordsSet.Contains(singular))
                    {
                        shouldInclude = false;
                    }
                }

                // Skip past tense -ED forms if root exists
                if (word.EndsWith("ED") && word.Length > 4)
                {
                    var root = word.Substring(0, word.Length - 2);
                    if (wordsSet.Contains(root))
                    {
                        shouldInclude = false;
                    }
                }

                // Skip gerund -ING forms if root exists
                if (word.EndsWith("ING") && word.Length > 5)
                {
                    var root = word.Substring(0, word.Length - 3);
                    if (wordsSet.Contains(root))
                    {
                        shouldInclude = false;
                    }
                }

                // Skip comparative -ER forms if root exists
                if (word.EndsWith("ER") && word.Length > 4)
                {
                    var root = word.Substring(0, word.Length - 2);
                    if (wordsSet.Contains(root))
                    {
                        shouldInclude = false;
                    }
                }

                // Skip superlative -EST forms if root exists
                if (word.EndsWith("EST") && word.Length > 5)
                {
                    var root = word.Substring(0, word.Length - 3);
                    if (wordsSet.Contains(root))
                    {
                        shouldInclude = false;
                    }
                }

                if (shouldInclude)
                {
                    filteredWords.Add(foundWord);
                }
            }

            return filteredWords;
        }

        /// <summary>
        /// Recursive trie-based subword search using available letters
        /// </summary>
        private async Task SearchSubwordsWithTrie(string currentWord, Dictionary<char, int> availableLetters, 
            int minLength, int maxLength, PrefixTrie smallTrie, PrefixTrie largeTrie, 
            List<WordamentFoundWord> results, HashSet<string> uniqueWords)
        {
            // Check if we should continue with current prefix
            var smallTrieResult = smallTrie.SearchPrefix(currentWord);
            var largeTrieResult = largeTrie.SearchPrefix(currentWord);
            
            // If no trie has this prefix, stop searching this branch
            if (!smallTrieResult.HasPrefix && !largeTrieResult.HasPrefix)
            {
                return;
            }
            
            // If current word is complete and meets length requirements
            if (currentWord.Length >= minLength && currentWord.Length <= maxLength)
            {
                FoundWordType? wordType = null;
                
                if (smallTrieResult.IsCompleteWord)
                {
                    wordType = FoundWordType.SubWordNotInGrid;
                }
                else if (largeTrieResult.IsCompleteWord)
                {
                    wordType = FoundWordType.SubWordInLargeDictionary;
                }
                
                if (wordType.HasValue && !uniqueWords.Contains(currentWord))
                {
                    uniqueWords.Add(currentWord);
                    
                    var foundWord = new WordamentFoundWord
                    {
                        Word = currentWord,
                        Path = new List<GridPosition>(), // No path for subwords
                        Score = 0, // No scoring for subword display
                        FoundAt = DateTime.Now,
                        IsRareWord = IsRareWord(currentWord),
                        IsLongestWord = false,
                        WordType = wordType.Value
                    };
                    
                    results.Add(foundWord);
                }
            }
            
            // Continue searching if we haven't reached max length and have valid prefixes
            if (currentWord.Length < maxLength && (smallTrieResult.HasPrefix || largeTrieResult.HasPrefix))
            {
                // Try adding each available letter
                var lettersCopy = new Dictionary<char, int>(availableLetters);
                
                foreach (var kvp in lettersCopy)
                {
                    char letter = kvp.Key;
                    int count = kvp.Value;
                    
                    if (count > 0)
                    {
                        // Use this letter
                        var newLetterCounts = new Dictionary<char, int>(availableLetters);
                        newLetterCounts[letter] = count - 1;
                        if (newLetterCounts[letter] == 0)
                        {
                            newLetterCounts.Remove(letter);
                        }
                        
                        var newWord = currentWord + letter;
                        
                        await SearchSubwordsWithTrie(newWord, newLetterCounts, minLength, maxLength,
                            smallTrie, largeTrie, results, uniqueWords);
                    }
                }
            }
            
            // Yield occasionally to keep UI responsive
            if (currentWord.Length == 0)
            {
                await Task.Yield();
            }
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
}