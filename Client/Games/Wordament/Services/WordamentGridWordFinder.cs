using DictionaryLib;
using WordScapeBlazorWasm.Models;

namespace WordScapeBlazorWasm.Services
{
    /// <summary>
    /// Dedicated service for finding all words in a Wordament grid using SeekWord optimization.
    /// Searches small dictionary first, then large dictionary excluding words already found.
    /// </summary>
    public class WordamentGridWordFinder
    {
        private readonly IDictionaryService _dictionaryService;

        public WordamentGridWordFinder(IDictionaryService dictionaryService)
        {
            _dictionaryService = dictionaryService;
        }

        /// <summary>
        /// Context class to reduce parameter passing in recursive search
        /// </summary>
        private class SearchContext
        {
            public WordamentGrid Grid { get; }
            public int MinLength { get; }
            public int MaxLength { get; }
            public DictionaryType DictionaryType { get; }
            public HashSet<string> FoundWords { get; }
            public List<WordamentFoundWord> AllResults { get; }
            public HashSet<string> ExcludeWords { get; }

            public SearchContext(
                WordamentGrid grid,
                int minLength,
                int maxLength,
                DictionaryType dictionaryType,
                HashSet<string> foundWords,
                List<WordamentFoundWord> allResults,
                HashSet<string> excludeWords = null)
            {
                Grid = grid;
                MinLength = minLength;
                MaxLength = maxLength;
                DictionaryType = dictionaryType;
                FoundWords = foundWords;
                AllResults = allResults;
                ExcludeWords = excludeWords;
            }
        }

        /// <summary>
        /// Find all valid words in the grid using SeekWord method from both small and large dictionaries.
        /// Searches small dictionary first, then large dictionary excluding words found in small dictionary.
        /// </summary>
        public async Task<List<WordamentFoundWord>> FindAllWordsInGridUsingSeekWordAsync(
            WordamentGrid grid, 
            int minLength = 3, 
            int maxLength = 16)
        {
            var allFoundWords = new List<WordamentFoundWord>();
            var smallDictWords = new HashSet<string>();
            var largeDictWords = new HashSet<string>();
            
            DebugHelper.Log($"Starting SeekWord-based grid word search (min: {minLength}, max: {maxLength})");
            
            try
            {
                // PHASE 1: Search small dictionary first
                DebugHelper.Log("Phase 1: Searching small dictionary...");
                await SearchDictionary(grid, minLength, maxLength, DictionaryType.Small, smallDictWords, allFoundWords);
                DebugHelper.Log($"Phase 1 complete: found {smallDictWords.Count} words in small dictionary");

                // PHASE 2: Search large dictionary, excluding words found in small dictionary
                DebugHelper.Log("Phase 2: Searching large dictionary (excluding small dict words)...");
                await SearchDictionary(grid, minLength, maxLength, DictionaryType.Large, largeDictWords, allFoundWords, smallDictWords);
                DebugHelper.Log($"Phase 2 complete: found {largeDictWords.Count} additional words in large dictionary");
                
                DebugHelper.Log($"SeekWord search complete: found {allFoundWords.Count} total unique words");
                
                // Sort alphabetically by word, then by length
                var sortedWords = allFoundWords
                    .OrderBy(w => w.Word)
                    .ThenBy(w => w.Word.Length)
                    .ToList();
                
                return sortedWords;
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Error in SeekWord grid word search: {ex.Message}");
                return allFoundWords;
            }
        }

        /// <summary>
        /// Search a specific dictionary for words in the grid
        /// </summary>
        private async Task SearchDictionary(
            WordamentGrid grid,
            int minLength,
            int maxLength,
            DictionaryType dictionaryType,
            HashSet<string> foundWords,
            List<WordamentFoundWord> allResults,
            HashSet<string> excludeWords = null)
        {
            var context = new SearchContext(grid, minLength, maxLength, dictionaryType, foundWords, allResults, excludeWords);
            var processedStartPositions = 0;
            
            // Search from each cell as starting position
            for (int startX = 0; startX < WordamentGrid.Size; startX++)
            {
                for (int startY = 0; startY < WordamentGrid.Size; startY++)
                {
                    var startPos = new GridPosition(startX, startY);
                    var visited = new bool[WordamentGrid.Size, WordamentGrid.Size];
                    var currentPath = new List<GridPosition>();
                    
                    await SearchWithSeekWord(startPos, visited, currentPath, context);
                    
                    processedStartPositions++;
                    
                    // Yield every 4 starting positions to keep UI responsive
                    if (processedStartPositions % 4 == 0)
                    {
                        await Task.Yield();
                    }
                }
            }
        }
        
        /// <summary>
        /// Recursive search using SeekWord method for prefix validation
        /// OPTIMIZED: Reduced parameter count from 10 to 4 to minimize stack pressure
        /// </summary>
        private async Task SearchWithSeekWord(
            GridPosition pos,
            bool[,] visited,
            List<GridPosition> currentPath,
            SearchContext context)
        {
            // Early exit conditions - no cleanup needed
            if (pos.X < 0 || pos.X >= WordamentGrid.Size || pos.Y < 0 || pos.Y >= WordamentGrid.Size)
                return;
                
            if (visited[pos.X, pos.Y])
                return;
                
            // Add current position to path - from this point on, we need cleanup
            visited[pos.X, pos.Y] = true;
            currentPath.Add(pos);
            
            try
            {
                // Get current word prefix
                var currentWord = GetWordFromPath(currentPath, context.Grid);
                
                // Skip if this word should be excluded (already found in previous dictionary search)
                if (context.ExcludeWords != null && context.ExcludeWords.Contains(currentWord))
                {
                    return; // Will be handled by finally block
                }
                
                // Use SeekWord to check if we should continue and if current word is valid
                bool continueSearch = false;
                bool isValidWord = false;
                
                try
                {
                    // Check the specified dictionary
                    var seekResult = _dictionaryService.SeekWord(currentWord, out var compResult, context.DictionaryType);
                    if (!string.IsNullOrEmpty(seekResult))
                    {
                        if (compResult == 0)
                        {
                            // Exact match found
                            isValidWord = true;
                            continueSearch = true; // Continue to find longer words
                        }
                        else if (seekResult.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                        {
                            // Current word is a valid prefix
                            continueSearch = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // If SeekWord fails, don't continue searching this path
                    DebugHelper.LogError($"SeekWord error for '{currentWord}' in {context.DictionaryType}: {ex.Message}");
                    return; // Will be handled by finally block
                }
                
                // If dictionary doesn't have this as a prefix, prune this branch
                if (!continueSearch)
                {
                    return; // Will be handled by finally block
                }
                
                // Check if current word is complete and valid
                if (currentWord.Length >= context.MinLength && 
                    currentWord.Length <= context.MaxLength && 
                    isValidWord &&
                    !context.FoundWords.Contains(currentWord))
                {
                    // Skip if this word should be excluded (already found in previous dictionary search)
                    if (context.ExcludeWords == null || !context.ExcludeWords.Contains(currentWord))
                    {
                        context.FoundWords.Add(currentWord);
                        
                        // Determine word type based on dictionary
                        var wordType = context.DictionaryType == DictionaryType.Small 
                            ? FoundWordType.SubWordNotInGrid  // Small dictionary words
                            : FoundWordType.SubWordInLargeDictionary; // Large dictionary words
                        
                        var foundWord = new WordamentFoundWord
                        {
                            Word = currentWord,
                            Path = new List<GridPosition>(currentPath), // Create copy of current path
                            Score = CalculateWordScore(currentWord, currentPath, context.Grid),
                            FoundAt = DateTime.Now,
                            IsRareWord = IsRareWord(currentWord),
                            WordType = wordType
                        };
                        
                        context.AllResults.Add(foundWord);
                    }
                }
                
                // Continue searching if we haven't reached max length and prefix is still valid
                if (currentPath.Count < context.MaxLength && continueSearch)
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
                                await SearchWithSeekWord(nextPos, visited, currentPath, context);
                            }
                        }
                    }
                }
            }
            finally
            {
                // Single point of cleanup - backtrack
                visited[pos.X, pos.Y] = false;
                currentPath.RemoveAt(currentPath.Count - 1);
            }
        }

        /// <summary>
        /// Get word from path (helper method moved from WordamentGameService)
        /// </summary>
        private string GetWordFromPath(List<GridPosition> path, WordamentGrid grid)
        {
            var word = "";
            foreach (var pos in path)
            {
                var cell = grid.GetCell(pos.X, pos.Y);
                if (cell.X == -1) return ""; // Invalid position
                word += cell.Letter;
            }
            return word;
        }

        /// <summary>
        /// Calculate word score (helper method moved from WordamentGameService)
        /// </summary>
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
            
            return Math.Max(totalScore, word.Length); // Minimum score equals word length
        }

        /// <summary>
        /// Check if word is rare (helper method moved from WordamentGameService)
        /// </summary>
        private bool IsRareWord(string word)
        {
            // Simple heuristic: words with uncommon letters or long words are considered rare
            var rareLetters = "JQXZ";
            bool hasRareLetter = word.Any(c => rareLetters.Contains(c));
            bool isLong = word.Length >= 7;
            
            return hasRareLetter || isLong;
        }
    }
}