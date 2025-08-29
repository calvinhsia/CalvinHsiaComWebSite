using System.Text.Json.Serialization;

namespace WordScapeBlazorWasm.Models
{
    public enum FoundWordType
    {
        SubWordInGrid,              // Word is in the puzzle grid (Dark Cyan)
        SubWordInLargeDictionary,   // Word is in large dictionary but not in grid (Light Sea Green) 
        SubWordNotInGrid,           // Word is in small dictionary but not in grid (Light Blue)
        SubWordNotAWord             // Word is not in any dictionary (Light Pink)
    }

    public class FoundWord
    {
        public string Word { get; set; } = "";
        public FoundWordType Type { get; set; }
    }

    public class GameSettings
    {
        public int MinWordLength { get; set; } = 3;
        public int MaxWordLength { get; set; } = 6;
        public bool IsDebugEnabled { get; set; } = false;
        public bool ShowCssRegions { get; set; } = false;
    }

    // UPDATED: Game state persistence model with grid data
    public class GameStateDto
    {
        public PuzzleState? PuzzleState { get; set; }
        public GameSettings? Settings { get; set; }
        public int HintCount { get; set; }
        public DateTime GameStartTime { get; set; }
        public bool GameCompleted { get; set; }
        public string? CurrentWord { get; set; }
        public string? CurrentWordStatusClass { get; set; }
        public string? Message { get; set; }
        public List<CircleLetter>? CircleLetters { get; set; }

        // ADDED: Store grid state explicitly
        public SerializableGridState? GridState { get; set; }
    }

    // ADDED: Serializable grid state to preserve revealed cells
    public class SerializableGridState
    {
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public List<SerializableGridCell> Cells { get; set; } = new();
        public Dictionary<string, SerializableWordPlacement> PlacedWords { get; set; } = new();
    }

    public class SerializableGridCell
    {
        public int X { get; set; }
        public int Y { get; set; }
        public char Letter { get; set; }
        public bool IsRevealed { get; set; }
        public bool IsBlank => Letter == CrosswordGrid.Blank || Letter == '_';
    }

    public class SerializableWordPlacement
    {
        public int StartX { get; set; }
        public int StartY { get; set; }
        public bool IsHorizontal { get; set; }
        public string Word { get; set; } = "";
    }

    public class PuzzleState
    {
        public string TargetWord { get; set; } = "";
        public List<string> PossibleWords { get; set; } = new();
        public HashSet<FoundWord> FoundWords { get; set; } = new();
        public List<char> CircleLetters { get; set; } = new();
        public string CurrentGuess { get; set; } = "";

        // FIXED: Game should only be complete when all grid words are found
        public bool IsComplete => FoundWords.Count(fw => fw.Type == FoundWordType.SubWordInGrid) == PossibleWords.Count;

        public int Score => FoundWords.Sum(fw => fw.Word.Length * 10);

        // Grid properties - using the original GenGrid system
        [JsonIgnore] // Don't serialize the complex GenGrid object
        public GenGrid? Grid { get; set; }

        // Cached legacy grid to maintain state
        private CrosswordGrid? _cachedLegacyGrid;

        // Compatibility properties for existing code
        [JsonIgnore]
        public CrosswordGrid LegacyGrid
        {
            get
            {
                if (_cachedLegacyGrid is null)
                {
                    _cachedLegacyGrid = ConvertToLegacyGrid();
                }
                return _cachedLegacyGrid;
            }
        }

        // UPDATED: Method to restore grid state from serialized data
        public void RestoreGridState(SerializableGridState gridState)
        {
            if (gridState == null) return;

            _cachedLegacyGrid = new CrosswordGrid
            {
                MaxX = gridState.MaxX,
                MaxY = gridState.MaxY,
                Letters = new char[gridState.MaxX, gridState.MaxY],
                PlacedWords = new Dictionary<string, WordPlacement>(),
                Cells = new List<GridCell>()
            };

            // Restore placed words
            foreach (var kvp in gridState.PlacedWords)
            {
                _cachedLegacyGrid.PlacedWords[kvp.Key] = new WordPlacement
                {
                    StartX = kvp.Value.StartX,
                    StartY = kvp.Value.StartY,
                    IsHorizontal = kvp.Value.IsHorizontal,
                    Word = kvp.Value.Word
                };
            }

            // Restore cells with their revealed state
            foreach (var cellData in gridState.Cells)
            {
                _cachedLegacyGrid.Letters[cellData.X, cellData.Y] = cellData.Letter;

                var cell = new GridCell
                {
                    X = cellData.X,
                    Y = cellData.Y,
                    Letter = cellData.Letter,
                    IsRevealed = cellData.IsRevealed
                };
                _cachedLegacyGrid.Cells.Add(cell);
            }
        }

        // UPDATED: Method to serialize current grid state
        public SerializableGridState SerializeGridState()
        {
            var legacyGrid = LegacyGrid;
            return new SerializableGridState
            {
                MaxX = legacyGrid.MaxX,
                MaxY = legacyGrid.MaxY,
                Cells = legacyGrid.Cells.Select(c => new SerializableGridCell
                {
                    X = c.X,
                    Y = c.Y,
                    Letter = c.Letter,
                    IsRevealed = c.IsRevealed
                }).ToList(),
                PlacedWords = legacyGrid.PlacedWords.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new SerializableWordPlacement
                    {
                        StartX = kvp.Value.StartX,
                        StartY = kvp.Value.StartY,
                        IsHorizontal = kvp.Value.IsHorizontal,
                        Word = kvp.Value.Word
                    }
                )
            };
        }

        private CrosswordGrid ConvertToLegacyGrid()
        {
            if (Grid is null)
                return new CrosswordGrid { MaxX = 15, MaxY = 15, Cells = new List<GridCell>() };

            var legacyGrid = new CrosswordGrid
            {
                MaxX = Grid._MaxX,
                MaxY = Grid._MaxY,
                Letters = Grid._chars,
                PlacedWords = new Dictionary<string, WordPlacement>()
            };

            // Convert placed words
            foreach (var kvp in Grid._dictPlacedWords)
            {
                var word = kvp.Key;
                var ltrPlaced = kvp.Value;
                legacyGrid.PlacedWords[word] = new WordPlacement
                {
                    StartX = ltrPlaced.nX,
                    StartY = ltrPlaced.nY,
                    IsHorizontal = ltrPlaced.IsHoriz,
                    Word = word
                };
            }

            // Convert cells
            legacyGrid.Cells = new List<GridCell>();
            for (int y = 0; y < legacyGrid.MaxY; y++)
            {
                for (int x = 0; x < legacyGrid.MaxX; x++)
                {
                    var cell = new GridCell
                    {
                        X = x,
                        Y = y,
                        Letter = legacyGrid.Letters[x, y],
                        IsRevealed = false
                    };
                    legacyGrid.Cells.Add(cell);
                }
            }

            return legacyGrid;
        }
    }

    public class CircleLetter
    {
        public char Letter { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public bool IsSelected { get; set; }
        public int Index { get; set; }
    }

    // Legacy grid classes for backward compatibility
    public class CrosswordGrid
    {
        public const char Blank = '_';
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public char[,] Letters { get; set; } = new char[0, 0];
        public Dictionary<string, WordPlacement> PlacedWords { get; set; } = new();
        public List<GridCell> Cells { get; set; } = new();
    }

    public class WordPlacement
    {
        public int StartX { get; set; }
        public int StartY { get; set; }
        public bool IsHorizontal { get; set; }
        public string Word { get; set; } = "";
    }

    public class GridCell
    {
        public int X { get; set; }
        public int Y { get; set; }
        public char Letter { get; set; }
        public bool IsBlank => Letter == CrosswordGrid.Blank || Letter == '_';
        public bool IsRevealed { get; set; }
    }

    public enum WordStatus
    {
        IsAlreadyInGrid,
        IsShownInGridForFirstTime,
        IsNotInGrid
    }
}

