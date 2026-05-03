using DictionaryLib;
using BlazorWasm.Models;

namespace BlazorWasm.Services
{
    // Factory service that creates models with complex logic
    public class PuzzleStateFactory
    {
        private readonly WordScapeGameService _gameService;
        private readonly GameSettingsService _settingsService;
        private readonly IDictionaryService _dictionaryService;

        public PuzzleStateFactory(WordScapeGameService gameService, GameSettingsService settingsService, IDictionaryService dictionaryService)
        {
            _gameService = gameService;
            _settingsService = settingsService;
            _dictionaryService = dictionaryService;
            DebugHelper.Log("PuzzleStateFactory: Initialized with shared DictionaryService");
        }

        public async Task<PuzzleState> CreatePuzzleAsync()
        {
            var settings = await _settingsService.LoadSettingsAsync();
            return await _gameService.GeneratePuzzleAsync(settings);
        }

        public async Task<PuzzleState> CreatePuzzleWithCustomLogicAsync(Func<GameSettings, Task<PuzzleState>> customGenerator)
        {
            var settings = await _settingsService.LoadSettingsAsync();
            return await customGenerator(settings);
        }

        /// <summary>
        /// Create puzzle with enhanced word validation using both dictionaries
        /// </summary>
        public async Task<PuzzleState> CreateEnhancedPuzzleAsync(GameSettings? customSettings = null)
        {
            var settings = customSettings ?? await _settingsService.LoadSettingsAsync();
            
            DebugHelper.Log($"PuzzleStateFactory: Creating enhanced puzzle with shared dictionaries");
            
            // Use the shared dictionary service for enhanced word generation
            var puzzle = await _gameService.GeneratePuzzleAsync(settings);
            
            // Enhance the puzzle by validating words with large dictionary
            var enhancedWords = new List<string>();
            foreach (var word in puzzle.PossibleWords.ToList())
            {
                if (_dictionaryService.IsWord(word, DictionaryType.Large))
                {
                    enhancedWords.Add(word);
                }
            }
            
            if (enhancedWords.Any())
            {
                DebugHelper.Log($"Enhanced puzzle validated {enhancedWords.Count} words against large dictionary");
            }
            
            return puzzle;
        }
    }
}