using Microsoft.JSInterop;
using System.Text.Json;
using WordScapeBlazorWasm.Models;

namespace WordScapeBlazorWasm.Services
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
}