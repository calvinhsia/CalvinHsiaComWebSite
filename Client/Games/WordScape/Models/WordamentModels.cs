using System.Text.Json.Serialization;
using WordScapeBlazorWasm.Services;

namespace WordScapeBlazorWasm.Models
{
    // Wordament-specific game models for 4x4 grid gameplay
    public class WordamentGameState
    {
        public WordamentGrid Grid { get; set; } = new();
        public HashSet<WordamentFoundWord> FoundWords { get; set; } = new();
        public string CurrentPath { get; set; } = "";
        public List<GridPosition> SelectedPath { get; set; } = new();
        public DateTime GameStartTime { get; set; } = DateTime.Now;
        public int Score { get; set; } = 0;
        public bool IsGameActive { get; set; } = true;
        public TimeSpan TimeRemaining { get; set; } = TimeSpan.FromMinutes(3); // Default 3-minute game

        [JsonIgnore]
        public bool IsGameComplete => TimeRemaining <= TimeSpan.Zero || !IsGameActive;
    }

    public class WordamentGrid
    {
        public const int Size = 4;
        
        [JsonIgnore] // Don't serialize the multidimensional array directly
        public WordamentCell[,] Cells { get; set; } = new WordamentCell[Size, Size];
        
        public int ScoreMultiplier { get; set; } = 1;

        public WordamentGrid()
        {
            InitializeGrid();
        }

        private void InitializeGrid()
        {
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    Cells[x, y] = new WordamentCell { X = x, Y = y, Letter = ' ' };
                }
            }
        }
        public void GenerateRandomGrid(Random random)
        {


        }

        public void GenerateRandomGridx(Random random)
        {
            // Letter frequency distribution similar to original Wordament
            var letterDistribution = new Dictionary<char, int>
            {
                ['A'] = 8, ['B'] = 2, ['C'] = 3, ['D'] = 4, ['E'] = 12, ['F'] = 2, ['G'] = 3,
                ['H'] = 2, ['I'] = 9, ['J'] = 1, ['K'] = 1, ['L'] = 4, ['M'] = 2, ['N'] = 6,
                ['O'] = 8, ['P'] = 2, ['Q'] = 1, ['R'] = 6, ['S'] = 4, ['T'] = 6, ['U'] = 4,
                ['V'] = 2, ['W'] = 2, ['X'] = 1, ['Y'] = 2, ['Z'] = 1
            };

            var availableLetters = new List<char>();
            foreach (var kvp in letterDistribution)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    availableLetters.Add(kvp.Key);
                }
            }

            // Fill the 4x4 grid with random letters
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    var randomIndex = random.Next(availableLetters.Count);
                    var letter = availableLetters[randomIndex];
                    
                    Cells[x, y] = new WordamentCell 
                    { 
                        X = x, 
                        Y = y, 
                        Letter = letter,
                        //IsSpecial = random.Next(100) < 10 // 10% chance for special cells
                    };
                    
                    // Assign special cell types
                    if (Cells[x, y].IsSpecial)
                    {
                        var specialTypes = Enum.GetValues<SpecialCellType>().Where(t => t != SpecialCellType.None).ToArray();
                        Cells[x, y].SpecialType = specialTypes[random.Next(specialTypes.Length)];
                    }
                }
            }
        }

        public bool AreAdjacent(GridPosition pos1, GridPosition pos2)
        {
            if (pos1.X == pos2.X && pos1.Y == pos2.Y) return false; // Same cell
            
            int xDiff = Math.Abs(pos1.X - pos2.X);
            int yDiff = Math.Abs(pos1.Y - pos2.Y);
            
            // Adjacent includes diagonal neighbors
            return xDiff <= 1 && yDiff <= 1;
        }

        public WordamentCell GetCell(int x, int y)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size)
                return new WordamentCell { X = -1, Y = -1, Letter = ' ' };
            return Cells[x, y];
        }

        // Serialize grid state for persistence
        public SerializableWordamentGrid SerializeGrid()
        {
            var serializedCells = new List<SerializableWordamentCell>();
            
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    var cell = Cells[x, y];
                    serializedCells.Add(new SerializableWordamentCell
                    {
                        X = cell.X,
                        Y = cell.Y,
                        Letter = cell.Letter,
                        IsSpecial = cell.IsSpecial,
                        SpecialType = cell.SpecialType
                    });
                }
            }

            return new SerializableWordamentGrid
            {
                Cells = serializedCells,
                ScoreMultiplier = ScoreMultiplier
            };
        }

        // Restore grid state from serialized data
        public void RestoreGrid(SerializableWordamentGrid gridState)
        {
            if (gridState?.Cells == null) return;

            ScoreMultiplier = gridState.ScoreMultiplier;

            // Clear current grid
            InitializeGrid();

            // Restore cells from saved state
            foreach (var savedCell in gridState.Cells)
            {
                if (savedCell.X >= 0 && savedCell.X < Size && savedCell.Y >= 0 && savedCell.Y < Size)
                {
                    Cells[savedCell.X, savedCell.Y] = new WordamentCell
                    {
                        X = savedCell.X,
                        Y = savedCell.Y,
                        Letter = savedCell.Letter,
                        IsSpecial = savedCell.IsSpecial,
                        SpecialType = savedCell.SpecialType
                    };
                }
            }
        }
    }

    public class WordamentCell
    {
        public int X { get; set; }
        public int Y { get; set; }
        public char Letter { get; set; }
        public bool IsSpecial { get; set; } = false;
        public SpecialCellType SpecialType { get; set; } = SpecialCellType.None;
        public bool IsSelected { get; set; } = false;
        public bool IsHighlighted { get; set; } = false;
        public bool IsValidMove { get; set; } = false; // For touch/drag UI feedback
        public int SelectionOrder { get; set; } = -1; // Order in which cell was selected (for path visualization)

        public int GetPointValue()
        {
            // Letter point values similar to Scrabble/Wordament
            return Letter switch
            {
                'A' or 'E' or 'I' or 'L' or 'N' or 'O' or 'R' or 'S' or 'T' or 'U' => 1,
                'D' or 'G' => 2,
                'B' or 'C' or 'M' or 'P' => 3,
                'F' or 'H' or 'V' or 'W' or 'Y' => 4,
                'K' => 5,
                'J' or 'X' => 8,
                'Q' or 'Z' => 10,
                _ => 1
            };
        }

        public int GetScoreMultiplier()
        {
            return SpecialType switch
            {
                SpecialCellType.DoubleLetter => 2,
                SpecialCellType.TripleLetter => 3,
                SpecialCellType.DoubleWord => 1, // Word multiplier handled separately
                SpecialCellType.TripleWord => 1, // Word multiplier handled separately
                _ => 1
            };
        }

        public int GetWordMultiplier()
        {
            return SpecialType switch
            {
                SpecialCellType.DoubleWord => 2,
                SpecialCellType.TripleWord => 3,
                _ => 1
            };
        }

        /// <summary>
        /// Reset all selection and highlight states
        /// </summary>
        public void ResetState()
        {
            IsSelected = false;
            IsHighlighted = false;
            IsValidMove = false;
            SelectionOrder = -1;
        }

        /// <summary>
        /// Get CSS classes for visual representation
        /// </summary>
        public string GetCssClasses()
        {
            var classes = new List<string>();
            
            if (IsSelected) classes.Add("selected");
            if (IsHighlighted) classes.Add("highlighted");
            if (IsValidMove) classes.Add("valid-move");
            
            if (IsSpecial)
            {
                classes.Add("special");
                classes.Add(SpecialType switch
                {
                    SpecialCellType.DoubleLetter => "double-letter",
                    SpecialCellType.TripleLetter => "triple-letter",
                    SpecialCellType.DoubleWord => "double-word",
                    SpecialCellType.TripleWord => "triple-word",
                    _ => ""
                });
            }
            
            return string.Join(" ", classes.Where(c => !string.IsNullOrEmpty(c)));
        }
    }

    public enum SpecialCellType
    {
        None,
        DoubleLetter,   // DL - Double letter score
        TripleLetter,   // TL - Triple letter score
        DoubleWord,     // DW - Double word score
        TripleWord      // TW - Triple word score
    }

    public class GridPosition
    {
        public int X { get; set; }
        public int Y { get; set; }

        public GridPosition() { }

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override bool Equals(object? obj)
        {
            return obj is GridPosition pos && X == pos.X && Y == pos.Y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override string ToString()
        {
            return $"({X},{Y})";
        }
    }

    public class WordamentFoundWord
    {
        public string Word { get; set; } = "";
        public List<GridPosition> Path { get; set; } = new();
        public int Score { get; set; }
        public bool IsLongestWord { get; set; } = false;
        public bool IsRareWord { get; set; } = false;
        public DateTime FoundAt { get; set; } = DateTime.Now;

        public string GetDisplayClass()
        {
            if (IsLongestWord) return "longest-word";
            if (IsRareWord) return "rare-word";
            if (Word.Length >= 6) return "long-word";
            return "normal-word";
        }
    }

    // Settings specific to Wordament gameplay
    public class WordamentSettings
    {
        public int GameDurationMinutes { get; set; } = 3;
        public int MinWordLength { get; set; } = 3;
        public bool ShowTimer { get; set; } = true;
        public bool AllowDiagonalMovement { get; set; } = true;
        public bool ShowWordScores { get; set; } = true;
        public bool IsDebugEnabled { get; set; } = false;
    }

    // Game state persistence model for Wordament
    public class WordamentGameStateDto
    {
        public WordamentGameState? GameState { get; set; }
        public WordamentSettings? Settings { get; set; }
        public bool IsPaused { get; set; }
        public string? LastPlayedGrid { get; set; } // Serialized grid state
        public SerializableWordamentGrid? GridState { get; set; } // Complete grid structure with cells
    }

    // Serializable grid state for Wordament
    public class SerializableWordamentGrid
    {
        public List<SerializableWordamentCell> Cells { get; set; } = new();
        public int ScoreMultiplier { get; set; } = 1;
    }

    public class SerializableWordamentCell
    {
        public int X { get; set; }
        public int Y { get; set; }
        public char Letter { get; set; }
        public bool IsSpecial { get; set; }
        public SpecialCellType SpecialType { get; set; }
    }
}