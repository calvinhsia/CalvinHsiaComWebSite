using DictionaryLib;
using WordScapeBlazorWasm.Models;

namespace WordScapeBlazorWasm.Services
{
    public class WordScapeGameService
    {
        private readonly DictionaryLib.DictionaryLib _dictionarySmall;
        private readonly DictionaryLib.DictionaryLib _dictionaryLarge;
        private Random _random;

        public WordScapeGameService()
        {
            _dictionarySmall = new DictionaryLib.DictionaryLib(DictionaryType.Small);
            _dictionaryLarge = new DictionaryLib.DictionaryLib(DictionaryType.Large);
            InitializeRandom();
        }

        private void InitializeRandom()
        {
            if (DebugHelper.IsDebugEnabled)
            {
                // Use fixed seed for consistent debugging/testing results in DEBUG builds
                _random = new Random(1);
                DebugHelper.Log("Using DEBUG mode with fixed seed for consistent results", true);
            }
            else
            {
                // Use no seed for truly random results in RELEASE builds
                _random = new Random();
                DebugHelper.Log("Using RELEASE mode with random seed for varied gameplay", true);
            }
        }

        /// <summary>
        /// Reset the random seed when debug mode changes for consistent debugging results
        /// </summary>
        public void OnDebugModeChanged()
        {
            InitializeRandom();
            DebugHelper.Log($"Random seed reset due to debug mode change. Debug enabled: {DebugHelper.IsDebugEnabled}", true);
        }

        public async Task<PuzzleState> GeneratePuzzleAsync(GameSettings settings)
        {
            DebugHelper.Log($"GeneratePuzzleAsync called - MinLength: {settings.MinWordLength}, MaxLength: {settings.MaxWordLength}, GridSize: {settings.GridWidth}x{settings.GridHeight}");

            // Add yield points for WebAssembly single-threaded environment
            await Task.Yield(); // Yield to allow UI updates

            try
            {
                // UPDATED: Use dynamic grid sizing from settings (max 18x18 for Android optimization)
                var wordGenerationParms = new WordGenerationParms()
                {
                    LenTargetWord = settings.MaxWordLength,
                    MinSubWordLength = settings.MinWordLength,
                    MaxX = Math.Min(18, settings.GridWidth), // Increased from 15 to 18 for Android grid optimization
                    MaxY = Math.Min(18, settings.GridHeight), // Increased from 15 to 18 for Android grid optimization
                    _Random = _random
                };

                var wordScapePuzzle = await WordScapePuzzle.CreateNextPuzzleTask(wordGenerationParms);

                if (wordScapePuzzle?.genGrid == null || wordScapePuzzle?.wordContainer?.InitialWord == null)
                {
                    throw new InvalidOperationException("Failed to generate puzzle: grid or target word is null");
                }

                var genGrid = wordScapePuzzle.genGrid;
                var targetWord = wordScapePuzzle.wordContainer.InitialWord;

                DebugHelper.Log($"Generated puzzle with target word: '{targetWord}', Grid size: {genGrid._MaxX}x{genGrid._MaxY}");

                var allWords = genGrid._dictPlacedWords.Keys.ToList();
                var possibleWords = new HashSet<string>(allWords);

                var puzzle = new PuzzleState
                {
                    TargetWord = targetWord,
                    PossibleWords = possibleWords.ToList(),
                    Grid = genGrid,
                    CircleLetters = CreateCircleLetters(targetWord)
                };

                DebugHelper.Log($"Puzzle created with {possibleWords.Count} possible words");
                return puzzle;
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Error generating puzzle: {ex.Message}");
                return await CreateFallbackPuzzleAsync(settings);
            }
        }

        private async Task<PuzzleState> CreateFallbackPuzzleAsync(GameSettings settings)
        {
            DebugHelper.LogWarning($"CreateFallbackPuzzle: MaxLength={settings.MaxWordLength}, MinLength={settings.MinWordLength}, GridSize: {settings.GridWidth}x{settings.GridHeight}");

            // Yield periodically for WebAssembly UI responsiveness
            await Task.Yield();

            var targetWord = GetRandomWordOfLength(settings.MaxWordLength);
            DebugHelper.Log($"Selected target word: '{targetWord}'");

            // Yield before intensive subword finding
            await Task.Yield();

            var possibleWords = FindAllSubwords(targetWord, settings.MinWordLength);
            DebugHelper.Log($"Found {possibleWords.Count} subwords: {string.Join(", ", possibleWords.Take(10))}");

            // Yield before grid generation
            await Task.Yield();

            var puzzle = new PuzzleState
            {
                TargetWord = targetWord,
                PossibleWords = possibleWords,
                Grid = await GenerateCrosswordGridAsync(possibleWords, targetWord, settings),
                CircleLetters = CreateCircleLetters(targetWord)
            };

            DebugHelper.Log($"Fallback puzzle created successfully");
            return puzzle;
        }

        private async Task<GenGrid> GenerateCrosswordGridAsync(List<string> possibleWords, string targetWord, GameSettings settings)
        {
            // FIXED: Use dynamic grid sizing from settings (max 18x18 for Android optimization)
            var gridWidth = Math.Min(18, settings.GridWidth);   // Increased from 15 to 18 for Android grid optimization
            var gridHeight = Math.Min(18, settings.GridHeight); // Increased from 15 to 18 for Android grid optimization
            
            var wordContainer = new WordContainer { InitialWord = targetWord, subwords = possibleWords };
            var genGrid = new GenGrid(gridWidth, gridHeight, wordContainer, _random );

            DebugHelper.LogGrid($"GenerateCrosswordGrid: Target='{targetWord}', PossibleWords={possibleWords.Count}, GridSize={gridWidth}x{gridHeight}");
            if (possibleWords.Count > 0)
            {
                DebugHelper.LogGrid($"Available words: {string.Join(", ", possibleWords.Take(10))}");
            }

            if (!possibleWords.Any()) return genGrid;

            // Yield before sorting (potentially CPU intensive for large word lists)
            await Task.Yield();

            // Smart word sorting: Balance density with placement opportunities
            // Instead of pure longest-first, use a multi-factor scoring system
            var sortedWords = SmartSortWordsForPlacement(possibleWords);
            var placedWords = new List<string>();

            // Place the first word horizontally in the center
            var firstWord = sortedWords.First();
            int startX = (genGrid._MaxX - firstWord.Length) / 2;
            int startY = genGrid._MaxY / 2;

            DebugHelper.LogGrid($"Placing first word '{firstWord}' at ({startX},{startY})");
            PlaceWordHorizontally(genGrid, firstWord, startX, startY);
            placedWords.Add(firstWord);

            // Yield before intensive word placement loop
            await Task.Yield();

            // Multi-pass placement strategy for better density
            await PlaceWordsWithMultiPassStrategy(genGrid, sortedWords.Skip(1).ToList(), placedWords);

            DebugHelper.LogGrid($"Final grid has {placedWords.Count} words: {string.Join(", ", placedWords)}");
            return genGrid;
        }

        private void PlaceWordHorizontally(GenGrid genGrid, string word, int startX, int startY)
        {
            for (int i = 0; i < word.Length; i++)
            {
                genGrid._chars[startX + i, startY] = word[i];
            }

            genGrid._dictPlacedWords[word] = new LtrPlaced
            {
                nX = startX,
                nY = startY,
                IsHoriz = true
            };
        }

        private void PlaceWordVertically(GenGrid genGrid, string word, int startX, int startY)
        {
            for (int i = 0; i < word.Length; i++)
            {
                genGrid._chars[startX, startY + i] = word[i];
            }

            genGrid._dictPlacedWords[word] = new LtrPlaced
            {
                nX = startX,
                nY = startY,
                IsHoriz = false
            };
        }

        private List<string> SmartSortWordsForPlacement(List<string> words)
        {
            DebugHelper.LogGrid($"Smart sorting {words.Count} words for optimal placement...");

            return words.OrderBy(word =>
            {
                // Multi-factor scoring: Lower score = higher priority
                double score = 0;

                // Factor 1: Word length (longer words generally harder to place later)
                score += (10 - word.Length) * 0.4;

                // Factor 2: Letter frequency (words with common letters easier to intersect)
                double letterCommonness = CalculateLetterCommonness(word);
                score += (1.0 - letterCommonness) * 0.3;

                // Factor 3: Vowel/consonant balance (balanced words more versatile)
                double balance = CalculateVowelConsonantBalance(word);
                score += (1.0 - balance) * 0.2;

                // Factor 4: Add slight randomization to avoid deterministic patterns
                score += _random.NextDouble() * 0.1;

                return score;
            }).ToList();
        }

        private async Task PlaceWordsWithMultiPassStrategy(GenGrid genGrid, List<string> remainingWords, List<string> placedWords)
        {
            DebugHelper.LogGrid($"Starting multi-pass placement for {remainingWords.Count} words...");

            // Pass 1: Standard placement
            await PlaceWordsWithStrategy(genGrid, remainingWords, placedWords, "standard", 12);

            // Pass 2: Relaxed placement
            var stillRemaining = remainingWords.Where(w => !placedWords.Contains(w)).ToList();
            await PlaceWordsWithStrategy(genGrid, stillRemaining, placedWords, "relaxed", 20);

            // Pass 3: Gap filling
            stillRemaining = remainingWords.Where(w => !placedWords.Contains(w) && w.Length <= 5).ToList();
            await PlaceWordsWithStrategy(genGrid, stillRemaining, placedWords, "gapfill", 25);

            DebugHelper.LogGrid($"Multi-pass complete: {placedWords.Count} total words placed");
        }

        private async Task PlaceWordsWithStrategy(GenGrid genGrid, List<string> words, List<string> placedWords, string strategy, int maxWords)
        {
            int attempts = 0;
            int successCount = 0;

            foreach (var word in words)
            {
                attempts++;
                if (placedWords.Count >= maxWords) break;

                // Yield every few attempts to prevent UI blocking
                if (attempts % 5 == 0)
                {
                    await Task.Yield();
                }

                // Strategy-specific debugging
                bool showDetailedDebug = strategy == "standard" && attempts <= 3;
                if (showDetailedDebug) DebugHelper.LogGrid($"{strategy} attempt {attempts}: Trying '{word}'...");

                bool placed = strategy switch
                {
                    "standard" => TryPlaceIntersectingWord(genGrid, word, placedWords, showDetailedDebug),
                    "relaxed" => TryPlaceIntersectingWord(genGrid, word, placedWords, false) || TryPlaceWordWithForce(genGrid, word, placedWords),
                    "gapfill" => TryPlaceIntersectingWord(genGrid, word, placedWords, false) || TryPlaceWordWithForce(genGrid, word, placedWords) || TryPlaceWordInGap(genGrid, word, placedWords),
                    _ => false
                };

                if (placed)
                {
                    placedWords.Add(word);
                    successCount++;
                    DebugHelper.LogGrid($"{strategy}: Placed '{word}' (pass total: {successCount}/{attempts})");
                }
                else if (showDetailedDebug)
                {
                    DebugHelper.LogGrid($"{strategy}: Failed to place '{word}'");
                }
            }

            DebugHelper.LogGrid($"{strategy} pass complete: {successCount}/{attempts} words placed");
        }

        private bool TryPlaceIntersectingWord(GenGrid genGrid, string newWord, List<string> placedWords, bool showDebug = false)
        {
            // Try to intersect with each placed word
            foreach (var placedWord in placedWords)
            {
                var placement = genGrid._dictPlacedWords[placedWord];
                if (showDebug) DebugHelper.LogGrid($"   Checking intersection with '{placedWord}' at ({placement.nX},{placement.nY}) IsHoriz={placement.IsHoriz}");

                // Find common letters
                for (int newIdx = 0; newIdx < newWord.Length; newIdx++)
                {
                    for (int placedIdx = 0; placedIdx < placedWord.Length; placedIdx++)
                    {
                        if (newWord[newIdx] == placedWord[placedIdx])
                        {
                            if (showDebug) DebugHelper.LogGrid($"   Found common letter '{newWord[newIdx]}' at newWord[{newIdx}] and placedWord[{placedIdx}]");

                            // Try to place the new word intersecting at this letter
                            if (placement.IsHoriz)
                            {
                                // Place new word vertically
                                int newStartX = placement.nX + placedIdx;
                                int newStartY = placement.nY - newIdx;

                                if (showDebug) DebugHelper.LogGrid($"   Trying vertical placement at ({newStartX},{newStartY})");
                                if (CanPlaceWordVertically(genGrid, newWord, newStartX, newStartY, showDebug))
                                {
                                    if (showDebug) DebugHelper.LogGrid($"   Can place '{newWord}' vertically at ({newStartX},{newStartY})");
                                    PlaceWordVertically(genGrid, newWord, newStartX, newStartY);
                                    return true;
                                }
                                else
                                {
                                    if (showDebug) DebugHelper.LogGrid($"   Cannot place '{newWord}' vertically at ({newStartX},{newStartY}) - bounds or conflict");
                                }
                            }
                            else
                            {
                                // Place new word horizontally
                                int newStartX = placement.nX - newIdx;
                                int newStartY = placement.nY + placedIdx;

                                if (showDebug) DebugHelper.LogGrid($"   Trying horizontal placement at ({newStartX},{newStartY})");
                                if (CanPlaceWordHorizontally(genGrid, newWord, newStartX, newStartY, showDebug))
                                {
                                    if (showDebug) DebugHelper.LogGrid($"   Can place '{newWord}' horizontally at ({newStartX},{newStartY})");
                                    PlaceWordHorizontally(genGrid, newWord, newStartX, newStartY);
                                    return true;
                                }
                                else
                                {
                                    if (showDebug) DebugHelper.LogGrid($"   Cannot place '{newWord}' horizontally at ({newStartX},{newStartY}) - bounds or conflict");
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private double CalculateLetterCommonness(string word)
        {
            var letterFreq = new Dictionary<char, double>
            {
                ['E'] = 0.127, ['T'] = 0.091, ['A'] = 0.082, ['O'] = 0.075, ['I'] = 0.070,
                ['N'] = 0.067, ['S'] = 0.063, ['H'] = 0.061, ['R'] = 0.060, ['D'] = 0.043,
                ['L'] = 0.040, ['C'] = 0.028, ['U'] = 0.028, ['M'] = 0.024, ['W'] = 0.024,
                ['F'] = 0.022, ['G'] = 0.020, ['Y'] = 0.020, ['P'] = 0.019, ['B'] = 0.015,
                ['V'] = 0.010, ['K'] = 0.008, ['J'] = 0.002, ['X'] = 0.002, ['Q'] = 0.001, ['Z'] = 0.001
            };

            double totalFreq = 0;
            foreach (char c in word)
            {
                totalFreq += letterFreq.GetValueOrDefault(c, 0.001);
            }
            return totalFreq / word.Length;
        }

        private double CalculateVowelConsonantBalance(string word)
        {
            var vowels = "AEIOU";
            int vowelCount = word.Count(c => vowels.Contains(c));
            double vowelRatio = (double)vowelCount / word.Length;
            double ideal = 0.4;
            double distance = Math.Abs(vowelRatio - ideal);
            return 1.0 - distance;
        }

        private bool TryPlaceWordWithForce(GenGrid genGrid, string word, List<string> placedWords) => false;
        private bool TryPlaceWordInGap(GenGrid genGrid, string word, List<string> placedWords) => false;

        private bool CanPlaceWordHorizontally(GenGrid genGrid, string word, int startX, int startY, bool showDebug = false) => 
            TryPlaceWord(genGrid, word, startX, startY, true, showDebug);

        private bool CanPlaceWordVertically(GenGrid genGrid, string word, int startX, int startY, bool showDebug = false) => 
            TryPlaceWord(genGrid, word, startX, startY, false, showDebug);

        private bool TryPlaceWord(GenGrid genGrid, string word, int startX, int startY, bool isHorizontal, bool showDebug = false)
        {
            int endX = isHorizontal ? startX + word.Length - 1 : startX;
            int endY = isHorizontal ? startY : startY + word.Length - 1;

            if (startX < 0 || startY < 0 || endX >= genGrid._MaxX || endY >= genGrid._MaxY)
            {
                if (showDebug) DebugHelper.LogGrid($"     Bounds check failed: word='{word}' at ({startX},{startY})");
                return false;
            }

            for (int i = 0; i < word.Length; i++)
            {
                int x = isHorizontal ? startX + i : startX;
                int y = isHorizontal ? startY : startY + i;
                char existingChar = genGrid._chars[x, y];

                if (existingChar != GenGrid.Blank && existingChar != word[i])
                {
                    if (showDebug) DebugHelper.LogGrid($"     Conflict at ({x},{y}): existing='{existingChar}', new='{word[i]}'");
                    return false;
                }
            }

            var tempGrid = CopyGrid(genGrid);
            for (int i = 0; i < word.Length; i++)
            {
                int x = isHorizontal ? startX + i : startX;
                int y = isHorizontal ? startY : startY + i;
                tempGrid[x, y] = word[i];
            }

            for (int y = Math.Max(0, startY - 1); y <= Math.Min(genGrid._MaxY - 1, endY + 1); y++)
            {
                if (!ValidateHorizontalSequences(tempGrid, genGrid._MaxX, y, showDebug))
                {
                    if (showDebug) DebugHelper.LogGrid($"     Invalid horizontal sequence created at row {y}");
                    return false;
                }
            }

            for (int x = Math.Max(0, startX - 1); x <= Math.Min(genGrid._MaxX - 1, endX + 1); x++)
            {
                if (!ValidateVerticalSequences(tempGrid, genGrid._MaxY, x, showDebug))
                {
                    if (showDebug) DebugHelper.LogGrid($"     Invalid vertical sequence created at column {x}");
                    return false;
                }
            }

            if (showDebug) DebugHelper.LogGrid($"     Word '{word}' can be placed at ({startX},{startY}) {(isHorizontal ? "horizontally" : "vertically")}");
            return true;
        }

        private char[,] CopyGrid(GenGrid genGrid)
        {
            var copy = new char[genGrid._MaxX, genGrid._MaxY];
            for (int x = 0; x < genGrid._MaxX; x++)
            {
                for (int y = 0; y < genGrid._MaxY; y++)
                {
                    copy[x, y] = genGrid._chars[x, y];
                }
            }
            return copy;
        }

        private bool ValidateHorizontalSequences(char[,] grid, int maxX, int row, bool showDebug)
        {
            int sequenceStart = -1;

            for (int x = 0; x <= maxX; x++)
            {
                bool hasLetter = x < maxX && grid[x, row] != GenGrid.Blank;

                if (hasLetter && sequenceStart == -1)
                {
                    sequenceStart = x;
                }
                else if (!hasLetter && sequenceStart != -1)
                {
                    int length = x - sequenceStart;
                    if (length > 1)
                    {
                        string sequence = ExtractHorizontalSequence(grid, sequenceStart, row, length);
                        if (!_dictionarySmall.IsWord(sequence))
                        {
                            if (showDebug) DebugHelper.LogGrid($"     Invalid horizontal sequence: '{sequence}' at ({sequenceStart},{row})");
                            return false;
                        }
                    }
                    sequenceStart = -1;
                }
            }
            return true;
        }

        private bool ValidateVerticalSequences(char[,] grid, int maxY, int column, bool showDebug)
        {
            int sequenceStart = -1;

            for (int y = 0; y <= maxY; y++)
            {
                bool hasLetter = y < maxY && grid[column, y] != GenGrid.Blank;

                if (hasLetter && sequenceStart == -1)
                {
                    sequenceStart = y;
                }
                else if (!hasLetter && sequenceStart != -1)
                {
                    int length = y - sequenceStart;
                    if (length > 1)
                    {
                        string sequence = ExtractVerticalSequence(grid, column, sequenceStart, length);
                        if (!_dictionarySmall.IsWord(sequence))
                        {
                            if (showDebug) DebugHelper.LogGrid($"     Invalid vertical sequence: '{sequence}' at ({column},{sequenceStart})");
                            return false;
                        }
                    }
                    sequenceStart = -1;
                }
            }
            return true;
        }

        private string ExtractHorizontalSequence(char[,] grid, int startX, int y, int length)
        {
            var sequence = new char[length];
            for (int i = 0; i < length; i++)
            {
                sequence[i] = grid[startX + i, y];
            }
            return new string(sequence);
        }

        private string ExtractVerticalSequence(char[,] grid, int x, int startY, int length)
        {
            var sequence = new char[length];
            for (int i = 0; i < length; i++)
            {
                sequence[i] = grid[x, startY + i];
            }
            return new string(sequence);
        }

        public string? GetWordAtPosition(int x, int y, PuzzleState puzzle)
        {
            foreach (var kvp in puzzle.Grid?._dictPlacedWords ?? new Dictionary<string, LtrPlaced>())
            {
                var word = kvp.Key;
                var placement = kvp.Value;

                if (puzzle.FoundWords.Any(fw => fw.Word == word))
                {
                    continue;
                }

                bool isWithinWord = false;
                if (placement.IsHoriz)
                {
                    if (y == placement.nY && x >= placement.nX && x < placement.nX + word.Length)
                        isWithinWord = true;
                }
                else
                {
                    if (x == placement.nX && y >= placement.nY && y < placement.nY + word.Length)
                        isWithinWord = true;
                }

                if (isWithinWord)
                {
                    return word;
                }
            }
            return null;
        }

        public void TemporarilyRevealWord(string word, PuzzleState puzzle)
        {
            DebugHelper.LogGrid($"TemporarilyRevealWord: '{word}'");
            if (puzzle.Grid?._dictPlacedWords.TryGetValue(word, out var placement) == true)
            {
                DebugHelper.LogGrid($"   Found placement: ({placement.nX},{placement.nY}) IsHoriz={placement.IsHoriz}");
                for (int i = 0; i < word.Length; i++)
                {
                    int x = placement.IsHoriz ? placement.nX + i : placement.nX;
                    int y = placement.IsHoriz ? placement.nY : placement.nY + i;
                    var cell = puzzle.LegacyGrid.Cells.FirstOrDefault(c => c.X == x && c.Y == y);
                    if (cell is not null)
                    {
                        DebugHelper.LogGrid($"   Revealing cell at ({x},{y}) with letter '{word[i]}'");
                        cell.IsRevealed = true;
                    }
                }
            }
        }

        public void HideWord(string word, PuzzleState puzzle)
        {
            DebugHelper.LogGrid($"HideWord: '{word}'");
            if (puzzle.Grid?._dictPlacedWords.TryGetValue(word, out var placement) == true)
            {
                // Hide letters that are not part of already found words
                for (int i = 0; i < word.Length; i++)
                {
                    int x = placement.IsHoriz ? placement.nX + i : placement.nX;
                    int y = placement.IsHoriz ? placement.nY : placement.nY + i;

                    var cell = puzzle.LegacyGrid.Cells.FirstOrDefault(c => c.X == x && c.Y == y);
                    if (cell is not null)
                    {
                        // Check if this cell is part of any found word
                        bool isPartOfFoundWord = false;
                        foreach (var foundWord in puzzle.FoundWords)
                        {
                            if (puzzle.Grid?._dictPlacedWords.TryGetValue(foundWord.Word, out var foundPlacement) == true)
                            {
                                for (int j = 0; j < foundWord.Word.Length; j++)
                                {
                                    int foundX = foundPlacement.IsHoriz ? foundPlacement.nX + j : foundPlacement.nX;
                                    int foundY = foundPlacement.IsHoriz ? foundPlacement.nY : foundPlacement.nY + j;

                                    if (foundX == x && foundY == y)
                                    {
                                        isPartOfFoundWord = true;
                                        break;
                                    }
                                }
                            }
                            if (isPartOfFoundWord) break;
                        }

                        // Only hide if not part of a found word
                        if (!isPartOfFoundWord)
                        {
                            cell.IsRevealed = false;
                        }
                    }
                }
            }
        }

        public WordStatus ShowWordInGrid(string word, PuzzleState puzzle)
        {
            DebugHelper.LogGrid($"ShowWordInGrid: '{word}'");

            // First, try to find the word placement from the Grid or LegacyGrid
            LtrPlaced? placement = null;

            // Check if the word is in the main Grid
            if (puzzle.Grid?._dictPlacedWords?.TryGetValue(word, out placement) == true)
            {
                DebugHelper.LogGrid($"   Found '{word}' in Grid at ({placement.nX},{placement.nY}) IsHoriz={placement.IsHoriz}");
            }
            // Check if the word is in the restored LegacyGrid's PlacedWords
            else if (puzzle.LegacyGrid?.PlacedWords?.TryGetValue(word, out var legacyPlacement) == true)
            {
                DebugHelper.LogGrid($"   Found '{word}' in LegacyGrid at ({legacyPlacement.StartX},{legacyPlacement.StartY}) IsHorizontal={legacyPlacement.IsHorizontal}");
                // Convert WordPlacement to LtrPlaced for consistency
                placement = new LtrPlaced
                {
                    nX = legacyPlacement.StartX,
                    nY = legacyPlacement.StartY,
                    IsHoriz = legacyPlacement.IsHorizontal
                };
            }

            if (placement != null)
            {
                bool wasAlreadyRevealed = true;

                // Reveal all letters of this word
                for (int i = 0; i < word.Length; i++)
                {
                    int x = placement.IsHoriz ? placement.nX + i : placement.nX;
                    int y = placement.IsHoriz ? placement.nY : placement.nY + i;

                    var cell = puzzle.LegacyGrid.Cells.FirstOrDefault(c => c.X == x && c.Y == y);
                    if (cell is not null && !cell.IsRevealed)
                    {
                        DebugHelper.LogGrid($"   Revealing cell at ({x},{y}) with letter '{word[i]}'");
                        wasAlreadyRevealed = false;
                        cell.IsRevealed = true;
                    }
                    else if (cell is not null)
                    {
                        DebugHelper.LogGrid($"   Cell at ({x},{y}) already revealed with letter '{cell.Letter}'");
                    }
                    else
                    {
                        DebugHelper.LogError($"   Could not find cell at ({x},{y})");
                    }
                }

                DebugHelper.LogGrid($"   ShowWordInGrid completed for '{word}', wasAlreadyRevealed={wasAlreadyRevealed}");
                return wasAlreadyRevealed ? WordStatus.IsAlreadyInGrid : WordStatus.IsShownInGridForFirstTime;
            }

            DebugHelper.LogError($"   Word '{word}' not found in either Grid or LegacyGrid");
            return WordStatus.IsNotInGrid;
        }

        private string GetRandomWordOfLength(int length)
        {
            // Use words that are likely to have many subwords
            var goodTargetWords = new Dictionary<int, string[]>
            {
                [3] = new[] { "THE", "AND", "FOR", "ARE", "BUT", "NOT", "YOU", "ALL", "CAN", "HER", "WAS", "ONE", "OUR", "HAD", "HAS" },
                [4] = new[] { "THAT", "WITH", "HAVE", "THIS", "WILL", "YOUR", "FROM", "THEY", "KNOW", "WANT", "BEEN", "GOOD", "MUCH", "SOME", "TIME" },
                [5] = new[] { "GREAT", "THINK", "THERE", "OTHER", "AFTER", "FIRST", "NEVER", "THESE", "WHERE", "BEING", "EVERY", "MIGHT", "SHALL", "HEART", "EARTH" },
                [6] = new[] { "PLANET", "MASTER", "GARDEN", "THREAD", "STREAM", "MOTHER", "FATHER", "FRIEND", "CHANGE", "ORANGE", "STRONG", "SIMPLE", "HEARTS" },
                [7] = new[] { "THREADS", "STREAMS", "MASTERS", "GARDENS", "PLANETS", "READING", "HEATING", "EARING", "TEACHER", "CREATES", "LARGEST", "STRANGE" },
                [8] = new[] { "CREATION", "STRENGTH", "LEARNING", "STREAMED", "THREADED", "MASTERED", "GARDENED", "PLANETED", "TEACHERS", "STRONGER", "TOGETHER", "BUSINESS" },
                [9] = new[] { "SOMETHING", "STREAMING", "THREADING", "MASTERING", "GARDENING", "THREATING", "SEARCHING", "BREATHING", "CREATIONS", "GREATNESS", "STRONGMAN" },
                [10] = new[] { "EVERYTHING", "STRENGTHEN", "STREAMLINE", "THREADLIKE", "MASTERMIND", "SEARCHABLE", "BREATHLESS", "CREATIONIST", "GREATENING", "STRONGHOLD" }
            };

            // Always try good target words first
            if (goodTargetWords.ContainsKey(length))
            {
                var words = goodTargetWords[length];
                var shuffled = words.OrderBy(x => _random.Next()).ToArray();

                foreach (var word in shuffled)
                {
                    if (_dictionarySmall.IsWord(word))
                    {
                        DebugHelper.Log($"Selected good target word: '{word}' (length {length})");
                        return word;
                    }
                }
            }

            // If no good words work, return a default that should have subwords
            DebugHelper.LogWarning($"Using fallback for length {length}");
            return length switch
            {
                3 => "THE",
                4 => "THAT",
                5 => "GREAT",
                6 => "PLANET",
                7 => "THREADS",
                8 => "CREATION",
                9 => "SOMETHING",
                10 => "EVERYTHING",
                _ => "SOMETHING"
            };
        }

        private List<string> FindAllSubwords(string targetWord, int minLength)
        {
            var validWords = new HashSet<string>();
            var letters = targetWord.ToCharArray();

            // Generate all possible permutations of different lengths
            for (int len = minLength; len <= targetWord.Length; len++)
            {
                GeneratePermutations("", letters.ToList(), len, validWords);
            }

            // Filter valid dictionary words and ensure they can be formed from target letters
            var result = validWords.Where(word =>
                word.Length >= minLength &&
                _dictionarySmall.IsWord(word) &&
                CanFormWordFromLetters(word, targetWord))
                .OrderBy(w => w.Length)
                .ThenBy(w => w)
                .ToList();

            // Apply filtering to remove plural/gerund/past tense duplicates (from original source)
            result = IgnorePluralGerundPastTenseWords(result);

            // Ensure we have at least some words by adding common subwords if needed
            if (result.Count < 5)
            {
                var commonWords = new[] { "THE", "AND", "FOR", "ARE", "BUT", "NOT", "YOU", "ALL", "CAN", "HER", "WAS", "ONE", "HAD", "HAS", "GET", "USE", "MAN", "NEW", "NOW", "OLD", "SEE", "HIM", "TWO", "HOW", "ITS", "WHO", "OIL", "SIT", "SET", "RUN", "EAT", "FAR", "SEA", "EYE", "RED", "TOP", "ARM", "TOO", "END", "WHY", "LET", "TRY" };
                foreach (var word in commonWords)
                {
                    if (word.Length >= minLength && CanFormWordFromLetters(word, targetWord) && _dictionarySmall.IsWord(word))
                    {
                        result.Add(word);
                        if (result.Count >= 10) break;
                    }
                }
            }

            return result.Distinct().OrderBy(w => w.Length).ThenBy(w => w).ToList();
        }

        private void GeneratePermutations(string current, List<char> remaining, int targetLength, HashSet<string> results)
        {
            if (current.Length == targetLength)
            {
                results.Add(current);
                return;
            }

            if (current.Length >= targetLength) return;

            for (int i = 0; i < remaining.Count; i++)
            {
                var nextChar = remaining[i];
                var nextRemaining = new List<char>(remaining);
                nextRemaining.RemoveAt(i);
                GeneratePermutations(current + nextChar, nextRemaining, targetLength, results);
            }
        }

        private bool CanFormWordFromLetters(string word, string availableLetters)
        {
            var available = availableLetters.ToCharArray().ToList();

            foreach (char c in word)
            {
                if (!available.Remove(c))
                {
                    return false;
                }
            }
            return true;
        }

        private List<char> CreateCircleLetters(string word)
        {
            var letters = word.ToCharArray().ToList();

            // Randomize the order of letters in the circle
            for (int i = letters.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (letters[i], letters[j]) = (letters[j], letters[i]);
            }

            DebugHelper.Log($"Randomized circle letters: {string.Join("", letters)} (from word: {word})");
            return letters;
        }

        /// <summary>
        /// Shuffle circle letters using the service's random instance to maintain debug consistency
        /// </summary>
        public List<char> ShuffleCircleLetters(List<char> letters)
        {
            var shuffledLetters = letters.ToList();
            
            for (int i = shuffledLetters.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (shuffledLetters[i], shuffledLetters[j]) = (shuffledLetters[j], shuffledLetters[i]);
            }
            
            DebugHelper.Log($"Shuffled circle letters using {'{'}{(DebugHelper.IsDebugEnabled ? "fixed" : "random")}{'}'} seed");
            return shuffledLetters;
        }

        public FoundWordType ValidateWord(string guess, PuzzleState puzzle)
        {
            DebugHelper.Log($"Validating word: '{guess}'");

            if (string.IsNullOrEmpty(guess) || guess.Length < 3)
            {
                DebugHelper.Log($"Invalid - too short or empty");
                return FoundWordType.SubWordNotAWord;
            }

            var canFormWord = CanFormWordFromLetters(guess, puzzle.TargetWord);
            if (!canFormWord)
            {
                DebugHelper.Log($"Cannot form from target letters");
                return FoundWordType.SubWordNotAWord;
            }

            // Check if word is in the puzzle grid (highest priority)
            var isPossible = puzzle.PossibleWords.Contains(guess);
            if (isPossible)
            {
                DebugHelper.Log($"Found in puzzle grid");
                return FoundWordType.SubWordInGrid;
            }

            // Check if word is in small dictionary
            var isInSmallDict = _dictionarySmall.IsWord(guess);
            if (isInSmallDict)
            {
                DebugHelper.Log($"Found in small dictionary");
                return FoundWordType.SubWordNotInGrid;
            }

            // Check if word is in large dictionary
            var isInLargeDict = _dictionaryLarge.IsWord(guess);
            if (isInLargeDict)
            {
                DebugHelper.Log($"Found in large dictionary");
                return FoundWordType.SubWordInLargeDictionary;
            }

            DebugHelper.Log($"Not found in any dictionary");
            return FoundWordType.SubWordNotAWord;
        }

        public bool IsValidGuess(string guess, PuzzleState puzzle)
        {
            var wordType = ValidateWord(guess, puzzle);
            // Accept words that are in grid or in any dictionary and can be formed
            return wordType != FoundWordType.SubWordNotAWord;
        }

        public bool TryAddWord(string word, PuzzleState puzzle)
        {
            var wordType = ValidateWord(word, puzzle);
            if (wordType != FoundWordType.SubWordNotAWord)
            {
                var foundWord = new FoundWord { Word = word, Type = wordType };
                if (!puzzle.FoundWords.Any(fw => fw.Word == word))
                {
                    puzzle.FoundWords.Add(foundWord);
                    if (wordType == FoundWordType.SubWordInGrid)
                    {
                        ShowWordInGrid(word, puzzle);
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Filter out plural, gerund, and past tense forms when the root word is also present.
        /// From original Xamarin WordScape source to prevent duplicate word forms.
        /// </summary>
        private List<string> IgnorePluralGerundPastTenseWords(List<string> words)
        {
            var filteredWords = new List<string>();
            var wordsSet = new HashSet<string>(words);

            foreach (var word in words)
            {
                bool shouldInclude = true;

                // Skip plurals if singular exists
                if (word.EndsWith("S") && word.Length > 3)
                {
                    var singular = word.Substring(0, word.Length - 1);
                    if (wordsSet.Contains(singular))
                    {
                        DebugHelper.LogGrid($"   Skipping plural '{word}' because singular '{singular}' exists");
                        shouldInclude = false;
                    }
                }

                // Skip past tense -ED forms if root exists
                if (word.EndsWith("ED") && word.Length > 4)
                {
                    var root = word.Substring(0, word.Length - 2);
                    if (wordsSet.Contains(root))
                    {
                        DebugHelper.LogGrid($"   Skipping past tense '{word}' because root '{root}' exists");
                        shouldInclude = false;
                    }
                }

                // Skip gerund -ING forms if root exists
                if (word.EndsWith("ING") && word.Length > 5)
                {
                    var root = word.Substring(0, word.Length - 3);
                    if (wordsSet.Contains(root))
                    {
                        DebugHelper.LogGrid($"   Skipping gerund '{word}' because root '{root}' exists");
                        shouldInclude = false;
                    }
                }

                // Skip comparative -ER forms if root exists
                if (word.EndsWith("ER") && word.Length > 4)
                {
                    var root = word.Substring(0, word.Length - 2);
                    if (wordsSet.Contains(root))
                    {
                        DebugHelper.LogGrid($"   Skipping comparative '{word}' because root '{root}' exists");
                        shouldInclude = false;
                    }
                }

                // Skip superlative -EST forms if root exists
                if (word.EndsWith("EST") && word.Length > 5)
                {
                    var root = word.Substring(0, word.Length - 3);
                    if (wordsSet.Contains(root))
                    {
                        DebugHelper.LogGrid($"   Skipping superlative '{word}' because root '{root}' exists");
                        shouldInclude = false;
                    }
                }

                if (shouldInclude)
                {
                    filteredWords.Add(word);
                }
            }

            DebugHelper.LogGrid($"   Filtered words from {words.Count} to {filteredWords.Count}");
            return filteredWords;
        }
    }
}
