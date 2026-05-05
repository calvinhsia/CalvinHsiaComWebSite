using Microsoft.JSInterop;
using System.Text.Json;
using BlazorWasm.Models;

namespace BlazorWasm.Services
{
    public class GameStateService
    {
        private readonly IJSRuntime _jsRuntime;
        private const string WORDSCAPE_STATE_KEY = "wordscape_game_state";
        private const string WORDAMENT_STATE_KEY = "wordament_game_state";

        public GameStateService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        // WordScape State Management
        public async Task SaveWordScapeStateAsync(WordScapePersistentState state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", WORDSCAPE_STATE_KEY, json);
                DebugHelper.Log($"WordScape state saved - Target: {state.TargetWord}, Score: {state.Score}");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"SaveWordScapeStateAsync error: {ex.Message}");
            }
        }

        public async Task<WordScapePersistentState?> LoadWordScapeStateAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", WORDSCAPE_STATE_KEY);
                if (!string.IsNullOrEmpty(json))
                {
                    var state = JsonSerializer.Deserialize<WordScapePersistentState>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    DebugHelper.Log($"WordScape state loaded - Target: {state?.TargetWord}, Score: {state?.Score}");
                    return state;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"LoadWordScapeStateAsync error: {ex.Message}");
            }
            return null;
        }

        public async Task ClearWordScapeStateAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", WORDSCAPE_STATE_KEY);
                DebugHelper.Log("WordScape state cleared");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"ClearWordScapeStateAsync error: {ex.Message}");
            }
        }

        // Wordament State Management
        public async Task SaveWordamentStateAsync(WordamentGameStateDto state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", WORDAMENT_STATE_KEY, json);
                DebugHelper.Log($"Wordament state saved - Score: {state.GameState?.Score}, Words: {state.GameState?.FoundWords.Count}");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"SaveWordamentStateAsync error: {ex.Message}");
            }
        }

        public async Task<WordamentGameStateDto?> LoadWordamentStateAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", WORDAMENT_STATE_KEY);
                if (!string.IsNullOrEmpty(json))
                {
                    var state = JsonSerializer.Deserialize<WordamentGameStateDto>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    DebugHelper.Log($"Wordament state loaded - Score: {state?.GameState?.Score}, Words: {state?.GameState?.FoundWords.Count}");
                    return state;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"LoadWordamentStateAsync error: {ex.Message}");
            }
            return null;
        }

        public async Task ClearWordamentStateAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", WORDAMENT_STATE_KEY);
                DebugHelper.Log("Wordament state cleared");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"ClearWordamentStateAsync error: {ex.Message}");
            }
        }

        // Wordament Settings Management
        public async Task SaveWordamentSettingsAsync(WordamentSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "wordament_settings", json);
                DebugHelper.Log($"Wordament settings saved - GameMode: {settings.GameMode}");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"SaveWordamentSettingsAsync error: {ex.Message}");
            }
        }

        public async Task<WordamentSettings?> LoadWordamentSettingsAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "wordament_settings");
                if (!string.IsNullOrEmpty(json))
                {
                    var settings = JsonSerializer.Deserialize<WordamentSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    DebugHelper.Log($"Wordament settings loaded - GameMode: {settings?.GameMode}");
                    return settings;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"LoadWordamentSettingsAsync error: {ex.Message}");
            }
            return null;
        }

        // Minesweeper State Management
        private const string MINESWEEPER_STATE_KEY = "minesweeper_game_state";
        private const string MINESWEEPER_SETTINGS_KEY = "minesweeper_settings";

        public async Task SaveMinesweeperStateAsync(MinesweeperPersistentState state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", MINESWEEPER_STATE_KEY, json);
                DebugHelper.Log($"Minesweeper state saved - Difficulty: {state.Difficulty}, Status: {state.GameStatus}");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"SaveMinesweeperStateAsync error: {ex.Message}");
            }
        }

        public async Task<MinesweeperPersistentState?> LoadMinesweeperStateAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", MINESWEEPER_STATE_KEY);
                if (!string.IsNullOrEmpty(json))
                {
                    var state = JsonSerializer.Deserialize<MinesweeperPersistentState>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    DebugHelper.Log($"Minesweeper state loaded - Difficulty: {state?.Difficulty}, Status: {state?.GameStatus}");
                    return state;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"LoadMinesweeperStateAsync error: {ex.Message}");
            }
            return null;
        }

        public async Task ClearMinesweeperStateAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", MINESWEEPER_STATE_KEY);
                DebugHelper.Log("Minesweeper state cleared");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"ClearMinesweeperStateAsync error: {ex.Message}");
            }
        }

        public async Task SaveMinesweeperSettingsAsync(string difficulty)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", MINESWEEPER_SETTINGS_KEY, difficulty);
                DebugHelper.Log($"Minesweeper settings saved - Difficulty: {difficulty}");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"SaveMinesweeperSettingsAsync error: {ex.Message}");
            }
        }

        public async Task<string?> LoadMinesweeperSettingsAsync()
        {
            try
            {
                var difficulty = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", MINESWEEPER_SETTINGS_KEY);
                if (!string.IsNullOrEmpty(difficulty))
                {
                    DebugHelper.Log($"Minesweeper settings loaded - Difficulty: {difficulty}");
                    return difficulty;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"LoadMinesweeperSettingsAsync error: {ex.Message}");
            }
            return null;
        }

        // General utility methods
        public async Task ClearAllGameStatesAsync()
        {
            await ClearWordScapeStateAsync();
            await ClearWordamentStateAsync();
        }

        public async Task<bool> HasWordScapeStateAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", WORDSCAPE_STATE_KEY);
                return !string.IsNullOrEmpty(json);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> HasWordamentStateAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", WORDAMENT_STATE_KEY);
                return !string.IsNullOrEmpty(json);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> HasMinesweeperStateAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", MINESWEEPER_STATE_KEY);
                return !string.IsNullOrEmpty(json);
            }
            catch
            {
                return false;
            }
        }

        // FreeCell State Management
        private const string FREECELL_STATE_KEY = "freecell_game_state";

        public async Task SaveFreeCellStateAsync(Client.Games.Cards.Services.FreeCellGameState state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", FREECELL_STATE_KEY, json);
                DebugHelper.Log($"FreeCell state saved - GameId: {state.GameId}, MoveCount: {state.MoveCount}");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"SaveFreeCellStateAsync error: {ex.Message}");
            }
        }

        public async Task<Client.Games.Cards.Services.FreeCellGameState?> LoadFreeCellStateAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", FREECELL_STATE_KEY);
                if (!string.IsNullOrEmpty(json))
                {
                    var state = JsonSerializer.Deserialize<Client.Games.Cards.Services.FreeCellGameState>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    DebugHelper.Log($"FreeCell state loaded - GameId: {state?.GameId}, MoveCount: {state?.MoveCount}");
                    return state;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"LoadFreeCellStateAsync error: {ex.Message}");
            }
            return null;
        }

        public async Task ClearFreeCellStateAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", FREECELL_STATE_KEY);
                DebugHelper.Log("FreeCell state cleared");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"ClearFreeCellStateAsync error: {ex.Message}");
            }
        }

        public async Task<bool> HasFreeCellStateAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", FREECELL_STATE_KEY);
                return !string.IsNullOrEmpty(json);
            }
            catch
            {
                return false;
            }
        }

        // FreeCell Settings Management
        private const string FREECELL_SETTINGS_KEY = "freecell_settings";

        public async Task SaveFreeCellSettingsAsync(FreeCellSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", FREECELL_SETTINGS_KEY, json);
                DebugHelper.Log($"FreeCell settings saved - AutoMoveToFoundation: {settings.AutoMoveToFoundation}");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"SaveFreeCellSettingsAsync error: {ex.Message}");
            }
        }

        public async Task<FreeCellSettings?> LoadFreeCellSettingsAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", FREECELL_SETTINGS_KEY);
                if (!string.IsNullOrEmpty(json))
                {
                    var settings = JsonSerializer.Deserialize<FreeCellSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    DebugHelper.Log($"FreeCell settings loaded - AutoMoveToFoundation: {settings?.AutoMoveToFoundation}");
                    return settings;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"LoadFreeCellSettingsAsync error: {ex.Message}");
            }
            return null;
        }

        // Solitaire Settings Management
        private const string SOLITAIRE_SETTINGS_KEY = "solitaire_settings";

        public async Task SaveSolitaireSettingsAsync(SolitaireSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SOLITAIRE_SETTINGS_KEY, json);
                DebugHelper.Log($"Solitaire settings saved - AutoMoveToFoundation: {settings.AutoMoveToFoundation}");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"SaveSolitaireSettingsAsync error: {ex.Message}");
            }
        }

        public async Task<SolitaireSettings?> LoadSolitaireSettingsAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", SOLITAIRE_SETTINGS_KEY);
                if (!string.IsNullOrEmpty(json))
                {
                    var settings = JsonSerializer.Deserialize<SolitaireSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    DebugHelper.Log($"Solitaire settings loaded - AutoMoveToFoundation: {settings?.AutoMoveToFoundation}");
                    return settings;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"LoadSolitaireSettingsAsync error: {ex.Message}");
            }
            return null;
        }
    }

    /// <summary>
    /// FreeCell game settings that persist across sessions
    /// </summary>
    public class FreeCellSettings
    {
        public bool AutoMoveToFoundation { get; set; } = false;
        public bool AutoShowHints { get; set; } = false;
        public int AutoSolveDelay { get; set; } = 500;
        public bool DebugMode { get; set; } = false;
    }

    /// <summary>
    /// Solitaire game settings that persist across sessions
    /// </summary>
    public class SolitaireSettings
    {
        public bool AutoMoveToFoundation { get; set; } = true; // Default to true for Solitaire
    }

    // Enhanced WordScape persistent state model
    public class WordScapePersistentState
    {
        public string TargetWord { get; set; } = "";
        public List<string> PossibleWords { get; set; } = new();
        public List<char> CircleLetters { get; set; } = new();
        public List<FoundWord> FoundWords { get; set; } = new();
        public int Score { get; set; }
        public int HintCount { get; set; }
        public DateTime GameStartTime { get; set; }
        public bool GameCompleted { get; set; }
        public string CurrentWord { get; set; } = "";
        public List<int> SelectedLetters { get; set; } = new();
        public bool IsSelecting { get; set; }
        public GameSettings Settings { get; set; } = new();
        
        /// <summary>
        /// Total number of subwords generated from the target word (before grid placement filtering)
        /// </summary>
        public int TotalSubwordsCount { get; set; }
        
        // Grid state - store complete grid layout
        public List<GridCellState> RevealedCells { get; set; } = new();
        public SerializableGridState? GridState { get; set; } // Complete grid structure
        public DateTime LastSaved { get; set; } = DateTime.Now;
    }

    public class GridCellState
    {
        public int X { get; set; }
        public int Y { get; set; }
        public char Letter { get; set; }
        public bool IsRevealed { get; set; }
        public bool IsBlank { get; set; }
    }

    // Minesweeper persistent state model
    public class MinesweeperPersistentState
    {
        public string Difficulty { get; set; } = "easy";
        public int Rows { get; set; } = 9;
        public int Cols { get; set; } = 9;
        public int MineCount { get; set; } = 10;
        public int FlaggedCount { get; set; }
        public int RevealedCount { get; set; }
        public int ElapsedTime { get; set; }
        public string GameStatus { get; set; } = "Ready";
        public bool GameOver { get; set; }
        public bool GameWon { get; set; }
        public bool FirstClick { get; set; } = true;
        public List<MinesweeperCellState> Cells { get; set; } = new();
        public DateTime LastSaved { get; set; } = DateTime.Now;
    }

    public class MinesweeperCellState
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public bool IsMine { get; set; }
        public int State { get; set; } // 0=Hidden, 1=Revealed, 2=Flagged
        public int AdjacentMines { get; set; }
    }
}