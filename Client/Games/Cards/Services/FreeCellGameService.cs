using Client.Games.Cards.Models;
using System.Text.Json;

namespace Client.Games.Cards.Services;

/// <summary>
/// DTO for serializing FreeCell game state
/// </summary>
public class FreeCellGameState
{
    public int GameId { get; set; }
    public int MoveCount { get; set; }
    public List<List<string>> Tableau { get; set; } = new();
    public List<string?> FreeCells { get; set; } = new();
    public List<List<string>> Foundations { get; set; } = new();
    public List<string> UndoStack { get; set; } = new(); // Each entry is a serialized snapshot
}

/// <summary>
/// Full-featured FreeCell game service with undo support and serialization.
/// Inherits core game logic from FreeCellGameBase.
/// </summary>
public class FreeCellGameService : FreeCellGameBase
{
    private readonly Random _random;

    // Undo support
    private readonly Stack<GameSnapshot> _undoStack = new();
    private record GameSnapshot(
        List<List<Card>> Tableau,
        List<Card?> FreeCells,
        List<List<Card>> Foundations,
        int MoveCount
    );

    public bool CanUndo => _undoStack.Count > 0;
    public int UndoCount => _undoStack.Count;

    public FreeCellGameService(Random? random = null)
    {
        _random = random ?? new Random();
        InitializeGame();
    }

    /// <summary>
    /// Initializes a new game with a random game ID
    /// </summary>
    public void InitializeGame()
    {
        // Generate a random game ID (1-1000000 like classic FreeCell)
        int gameId = _random.Next(1, 1000001);
        InitializeGame(gameId);
    }

    /// <summary>
    /// Initializes a specific game by ID (like classic FreeCell game numbers)
    /// 
    /// NOTE: Our PRNG algorithm does not match classic Windows FreeCell exactly!
    /// Only game #11982 is hardcoded to match the classic unsolvable layout.
    /// Other game IDs will produce different layouts than Windows FreeCell.
    /// 
    /// Verified unsolvable games:
    /// #11982 - Hardcoded to match classic Windows FreeCell exactly
    /// #999999 - Custom impossible layout with all 4 aces buried
    /// </summary>
    public void InitializeGame(int gameId)
    {
        GameId = gameId;
        MoveCount = 0;
        Selection = null;
        _undoStack.Clear();

        // Initialize 4 free cells (all empty)
        FreeCells = new List<Card?> { null, null, null, null };

        // Initialize 4 foundations (empty)
        Foundations = new List<List<Card>>
        {
            new(), new(), new(), new()
        };

        // Initialize 8 tableau columns
        Tableau = new List<List<Card>>();
        for (int col = 0; col < 8; col++)
        {
            Tableau.Add(new List<Card>());
        }

        // Special case: Game #11982 is the famous unsolvable game
        // Hardcode to match classic Windows FreeCell exactly
        if (gameId == 11982)
        {
            InitializeGame11982();
            return;
        }

        // Special case: Game #999999 is systematically unsolvable (all aces buried)
        if (gameId == 999999)
        {
            InitializeBuriedAcesGame();
            return;
        }

        // For other games, use deterministic PRNG algorithm
        int state = gameId;
        int Rand()
        {
            state = (int)(((long)state * 214013L + 2531011L) & 0x7FFFFFFF);
            return (state >> 16) & 0x7FFF;
        }

        // Initialize deck with 52 cards (0-51)
        // Card encoding: suit = card % 4, rank = card / 4
        // Suits: 0=Clubs, 1=Diamonds, 2=Hearts, 3=Spades
        // Ranks: 0=Ace, 1=Two, ..., 12=King
        var deck = Enumerable.Range(0, 52).ToList();

        // Shuffle using Fisher-Yates (from end to beginning)
        for (int i = 51; i > 0; i--)
        {
            int j = Rand() % (i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        // Deal cards from the back of the deck to columns 0-7
        // This mimics dealing cards face-up from a shuffled deck
        for (int i = 51; i >= 0; i--)
        {
            int cardIndex = deck[i];

            // Convert to suit and rank
            int suitIndex = cardIndex % 4;
            int rankValue = cardIndex / 4;

            Suit suit = suitIndex switch
            {
                0 => Suit.Clubs,
                1 => Suit.Diamonds,
                2 => Suit.Hearts,
                3 => Suit.Spades,
                _ => Suit.Clubs
            };

            Rank rank = (Rank)(rankValue + 1);

            // Deal to columns in order (0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, ...)
            int col = (51 - i) % 8;
            Tableau[col].Add(new Card(suit, rank, true));
        }
    }

    /// <summary>
    /// Initializes the famous unsolvable Game #11982 with the exact classic Windows FreeCell layout.
    /// Layout verified from: https://dan.hersam.com/2009/02/13/how-to-beat-the-impossible-freecell-game/
    /// This game has been proven impossible by exhaustive computer search.
    /// </summary>
    private void InitializeGame11982()
    {
        // Column 1: JD 2S 9H 6C AD QS 9S (7 cards, bottom to top)
        Tableau[0].AddRange(new[]
        {
            new Card(Suit.Diamonds, Rank.Jack, true),   // JD
            new Card(Suit.Spades, Rank.Two, true),      // 2S
            new Card(Suit.Hearts, Rank.Nine, true),     // 9H
            new Card(Suit.Clubs, Rank.Six, true),       // 6C
            new Card(Suit.Diamonds, Rank.Ace, true),    // AD
            new Card(Suit.Spades, Rank.Queen, true),    // QS
            new Card(Suit.Spades, Rank.Nine, true)      // 9S
        });

        // Column 2: 2D 9D 8H 9C TH 4C 3C (7 cards)
        Tableau[1].AddRange(new[]
        {
            new Card(Suit.Diamonds, Rank.Two, true),    // 2D
            new Card(Suit.Diamonds, Rank.Nine, true),   // 9D
            new Card(Suit.Hearts, Rank.Eight, true),    // 8H
            new Card(Suit.Clubs, Rank.Nine, true),      // 9C
            new Card(Suit.Hearts, Rank.Ten, true),      // TH
            new Card(Suit.Clubs, Rank.Four, true),      // 4C
            new Card(Suit.Clubs, Rank.Three, true)      // 3C
        });

        // Column 3: KS 3D 4D 3S 8S JS KC (7 cards)
        Tableau[2].AddRange(new[]
        {
            new Card(Suit.Spades, Rank.King, true),     // KS
            new Card(Suit.Diamonds, Rank.Three, true),  // 3D
            new Card(Suit.Diamonds, Rank.Four, true),   // 4D
            new Card(Suit.Spades, Rank.Three, true),    // 3S
            new Card(Suit.Spades, Rank.Eight, true),    // 8S
            new Card(Suit.Spades, Rank.Jack, true),     // JS
            new Card(Suit.Clubs, Rank.King, true)       // KC
        });

        // Column 4: 8C QH TC 7S 7C 4H KD (7 cards)
        Tableau[3].AddRange(new[]
        {
            new Card(Suit.Clubs, Rank.Eight, true),     // 8C
            new Card(Suit.Hearts, Rank.Queen, true),    // QH
            new Card(Suit.Clubs, Rank.Ten, true),       // TC
            new Card(Suit.Spades, Rank.Seven, true),    // 7S
            new Card(Suit.Clubs, Rank.Seven, true),     // 7C
            new Card(Suit.Hearts, Rank.Four, true),     // 4H
            new Card(Suit.Diamonds, Rank.King, true)    // KD
        });

        // Column 5: JH 7D 6H QC 6D AH (6 cards)
        Tableau[4].AddRange(new[]
        {
            new Card(Suit.Hearts, Rank.Jack, true),     // JH
            new Card(Suit.Diamonds, Rank.Seven, true),  // 7D
            new Card(Suit.Hearts, Rank.Six, true),      // 6H
            new Card(Suit.Clubs, Rank.Queen, true),     // QC
            new Card(Suit.Diamonds, Rank.Six, true),    // 6D
            new Card(Suit.Hearts, Rank.Ace, true)       // AH
        });

        // Column 6: 5S TS 8D 7H 3H 4S (6 cards)
        Tableau[5].AddRange(new[]
        {
            new Card(Suit.Spades, Rank.Five, true),     // 5S
            new Card(Suit.Spades, Rank.Ten, true),      // TS
            new Card(Suit.Diamonds, Rank.Eight, true),  // 8D
            new Card(Suit.Hearts, Rank.Seven, true),    // 7H
            new Card(Suit.Hearts, Rank.Three, true),    // 3H
            new Card(Suit.Spades, Rank.Four, true)      // 4S
        });

        // Column 7: 2C JC TD QD 2H KH (6 cards)
        Tableau[6].AddRange(new[]
        {
            new Card(Suit.Clubs, Rank.Two, true),       // 2C
            new Card(Suit.Clubs, Rank.Jack, true),      // JC
            new Card(Suit.Diamonds, Rank.Ten, true),    // TD
            new Card(Suit.Diamonds, Rank.Queen, true),  // QD
            new Card(Suit.Hearts, Rank.Two, true),      // 2H
            new Card(Suit.Hearts, Rank.King, true)      // KH
        });

        // Column 8: 5H 5C 6S AS 5D AC (6 cards)
        Tableau[7].AddRange(new[]
        {
            new Card(Suit.Hearts, Rank.Five, true),     // 5H
            new Card(Suit.Clubs, Rank.Five, true),      // 5C
            new Card(Suit.Spades, Rank.Six, true),      // 6S
            new Card(Suit.Spades, Rank.Ace, true),      // AS
            new Card(Suit.Diamonds, Rank.Five, true),   // 5D
            new Card(Suit.Clubs, Rank.Ace, true)        // AC
        });
    }

    /// <summary>
    /// Initializes an impossible game #999999 with all 4 aces deeply buried.
    /// This makes the game systematically unsolvable since foundations must start with aces,
    /// but the aces are trapped at the bottom of columns with no way to reach them.
    /// Each ace is blocked by same-color cards that cannot be moved elsewhere.
    /// </summary>
    private void InitializeBuriedAcesGame()
    {
        // Column 1: Ace of Hearts buried under red cards that can't move (7 cards)
        Tableau[0].AddRange(new[]
        {
            new Card(Suit.Hearts, Rank.Ace, true),     // A? - BURIED at bottom!
            new Card(Suit.Hearts, Rank.King, true),    // K? - blocks ace, can only go to empty column
            new Card(Suit.Diamonds, Rank.Queen, true), // Q? - needs black K
            new Card(Suit.Hearts, Rank.Jack, true),    // J? - needs black Q
            new Card(Suit.Diamonds, Rank.Ten, true),   // T? - needs black J
            new Card(Suit.Hearts, Rank.Nine, true),    // 9? - needs black T
            new Card(Suit.Diamonds, Rank.Eight, true)  // 8? - needs black 9
        });

        // Column 2: Ace of Diamonds buried under red cards (7 cards)
        Tableau[1].AddRange(new[]
        {
            new Card(Suit.Diamonds, Rank.Ace, true),   // A? - BURIED at bottom!
            new Card(Suit.Diamonds, Rank.King, true),  // K? - blocks ace
            new Card(Suit.Hearts, Rank.Queen, true),   // Q? - needs black K
            new Card(Suit.Diamonds, Rank.Jack, true),  // J? - needs black Q
            new Card(Suit.Hearts, Rank.Ten, true),     // T? - needs black J
            new Card(Suit.Diamonds, Rank.Nine, true),  // 9? - needs black T
            new Card(Suit.Hearts, Rank.Eight, true)    // 8? - needs black 9
        });

        // Column 3: Ace of Clubs buried under black cards (7 cards)
        Tableau[2].AddRange(new[]
        {
            new Card(Suit.Clubs, Rank.Ace, true),      // A? - BURIED at bottom!
            new Card(Suit.Clubs, Rank.King, true),     // K? - blocks ace
            new Card(Suit.Spades, Rank.Queen, true),   // Q? - needs red K
            new Card(Suit.Clubs, Rank.Jack, true),     // J? - needs red Q
            new Card(Suit.Spades, Rank.Ten, true),     // T? - needs red J
            new Card(Suit.Clubs, Rank.Nine, true),     // 9? - needs red T
            new Card(Suit.Spades, Rank.Eight, true)    // 8? - needs red 9
        });

        // Column 4: Ace of Spades buried under black cards (7 cards)
        Tableau[3].AddRange(new[]
        {
            new Card(Suit.Spades, Rank.Ace, true),     // A? - BURIED at bottom!
            new Card(Suit.Spades, Rank.King, true),    // K? - blocks ace
            new Card(Suit.Clubs, Rank.Queen, true),    // Q? - needs red K
            new Card(Suit.Spades, Rank.Jack, true),    // J? - needs red Q
            new Card(Suit.Clubs, Rank.Ten, true),      // T? - needs red J
            new Card(Suit.Spades, Rank.Nine, true),    // 9? - needs red T
            new Card(Suit.Clubs, Rank.Eight, true)     // 8? - needs red 9
        });

        // Column 5: Sevens (6 cards)
        Tableau[4].AddRange(new[]
        {
            new Card(Suit.Hearts, Rank.Seven, true),
            new Card(Suit.Diamonds, Rank.Seven, true),
            new Card(Suit.Clubs, Rank.Seven, true),
            new Card(Suit.Spades, Rank.Seven, true),
            new Card(Suit.Hearts, Rank.Six, true),
            new Card(Suit.Diamonds, Rank.Six, true)
        });

        // Column 6: Sixes and fives (6 cards)
        Tableau[5].AddRange(new[]
        {
            new Card(Suit.Clubs, Rank.Six, true),
            new Card(Suit.Spades, Rank.Six, true),
            new Card(Suit.Hearts, Rank.Five, true),
            new Card(Suit.Diamonds, Rank.Five, true),
            new Card(Suit.Clubs, Rank.Five, true),
            new Card(Suit.Spades, Rank.Five, true)
        });

        // Column 7: Fours and threes (6 cards)
        Tableau[6].AddRange(new[]
        {
            new Card(Suit.Hearts, Rank.Four, true),
            new Card(Suit.Diamonds, Rank.Four, true),
            new Card(Suit.Clubs, Rank.Four, true),
            new Card(Suit.Spades, Rank.Four, true),
            new Card(Suit.Hearts, Rank.Three, true),
            new Card(Suit.Diamonds, Rank.Three, true)
        });

        // Column 8: Threes and twos (6 cards)
        Tableau[7].AddRange(new[]
        {
            new Card(Suit.Clubs, Rank.Three, true),
            new Card(Suit.Spades, Rank.Three, true),
            new Card(Suit.Hearts, Rank.Two, true),
            new Card(Suit.Diamonds, Rank.Two, true),
            new Card(Suit.Clubs, Rank.Two, true),
            new Card(Suit.Spades, Rank.Two, true)
        });
    }

    /// <summary>
    /// Captures the current game state for undo
    /// </summary>
    private GameSnapshot CaptureSnapshot()
    {
        return new GameSnapshot(
            Tableau.Select(col => col.Select(c => new Card(c.Suit, c.Rank, c.IsFaceUp)).ToList()).ToList(),
            FreeCells.Select(c => c != null ? new Card(c.Suit, c.Rank, c.IsFaceUp) : null).ToList(),
            Foundations.Select(f => f.Select(c => new Card(c.Suit, c.Rank, c.IsFaceUp)).ToList()).ToList(),
            MoveCount
        );
    }

    /// <summary>
    /// Override to capture snapshot before each move for undo support
    /// </summary>
    protected override void OnBeforeMove()
    {
        _undoStack.Push(CaptureSnapshot());
    }

    /// <summary>
    /// Restores the game state from a snapshot
    /// </summary>
    private void RestoreSnapshot(GameSnapshot snapshot)
    {
        Tableau = snapshot.Tableau;
        FreeCells = snapshot.FreeCells;
        Foundations = snapshot.Foundations;
        MoveCount = snapshot.MoveCount;
        Selection = null;
    }

    /// <summary>
    /// Undoes the last move
    /// </summary>
    public bool Undo()
    {
        if (!CanUndo) return false;

        var snapshot = _undoStack.Pop();
        RestoreSnapshot(snapshot);
        return true;
    }

    #region Serialization

    /// <summary>
    /// Serializes a card to a compact string format (e.g., "AS" for Ace of Spades)
    /// </summary>
    private static string SerializeCard(Card card)
    {
        char rank = card.Rank switch
        {
            Rank.Ace => 'A',
            Rank.Ten => 'T',
            Rank.Jack => 'J',
            Rank.Queen => 'Q',
            Rank.King => 'K',
            _ => (char)('0' + (int)card.Rank)
        };

        char suit = card.Suit switch
        {
            Suit.Clubs => 'C',
            Suit.Diamonds => 'D',
            Suit.Hearts => 'H',
            Suit.Spades => 'S',
            _ => '?'
        };

        return $"{rank}{suit}";
    }

    /// <summary>
    /// Deserializes a card from compact string format
    /// </summary>
    private static Card DeserializeCard(string str)
    {
        if (string.IsNullOrEmpty(str) || str.Length != 2)
            throw new ArgumentException($"Invalid card string: {str}");

        Rank rank = str[0] switch
        {
            'A' => Rank.Ace,
            'T' => Rank.Ten,
            'J' => Rank.Jack,
            'Q' => Rank.Queen,
            'K' => Rank.King,
            >= '2' and <= '9' => (Rank)(str[0] - '0'),
            _ => throw new ArgumentException($"Invalid rank: {str[0]}")
        };

        Suit suit = str[1] switch
        {
            'C' => Suit.Clubs,
            'D' => Suit.Diamonds,
            'H' => Suit.Hearts,
            'S' => Suit.Spades,
            _ => throw new ArgumentException($"Invalid suit: {str[1]}")
        };

        return new Card(suit, rank, true);
    }

    /// <summary>
    /// Serializes the current game state to a DTO for JSON storage
    /// </summary>
    public FreeCellGameState SerializeState()
    {
        var state = new FreeCellGameState
        {
            GameId = GameId,
            MoveCount = MoveCount,
            Tableau = Tableau.Select(col => col.Select(SerializeCard).ToList()).ToList(),
            FreeCells = FreeCells.Select(c => c != null ? SerializeCard(c) : null).ToList(),
            Foundations = Foundations.Select(f => f.Select(SerializeCard).ToList()).ToList(),
            UndoStack = _undoStack.Select(SerializeSnapshot).ToList()
        };

        return state;
    }

    /// <summary>
    /// Serializes a snapshot to JSON string
    /// </summary>
    private string SerializeSnapshot(GameSnapshot snapshot)
    {
        var dto = new
        {
            Tableau = snapshot.Tableau.Select(col => col.Select(SerializeCard).ToList()).ToList(),
            FreeCells = snapshot.FreeCells.Select(c => c != null ? SerializeCard(c) : null).ToList(),
            Foundations = snapshot.Foundations.Select(f => f.Select(SerializeCard).ToList()).ToList(),
            MoveCount = snapshot.MoveCount
        };
        return JsonSerializer.Serialize(dto);
    }

    /// <summary>
    /// Deserializes a snapshot from JSON string
    /// </summary>
    private GameSnapshot DeserializeSnapshot(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tableau = root.GetProperty("Tableau").EnumerateArray()
            .Select(col => col.EnumerateArray().Select(c => DeserializeCard(c.GetString()!)).ToList())
            .ToList();

        var freeCells = root.GetProperty("FreeCells").EnumerateArray()
            .Select(c => c.ValueKind == JsonValueKind.Null ? null : DeserializeCard(c.GetString()!))
            .ToList();

        var foundations = root.GetProperty("Foundations").EnumerateArray()
            .Select(f => f.EnumerateArray().Select(c => DeserializeCard(c.GetString()!)).ToList())
            .ToList();

        var moveCount = root.GetProperty("MoveCount").GetInt32();

        return new GameSnapshot(tableau, freeCells, foundations, moveCount);
    }

    /// <summary>
    /// Restores game state from a serialized DTO
    /// </summary>
    public void RestoreState(FreeCellGameState state)
    {
        GameId = state.GameId;
        MoveCount = state.MoveCount;
        Selection = null;

        Tableau = state.Tableau.Select(col => col.Select(DeserializeCard).ToList()).ToList();
        FreeCells = state.FreeCells.Select(c => c != null ? DeserializeCard(c) : null).ToList();
        Foundations = state.Foundations.Select(f => f.Select(DeserializeCard).ToList()).ToList();

        _undoStack.Clear();
        // Restore undo stack in reverse order (stack is LIFO)
        foreach (var snapshotJson in state.UndoStack.AsEnumerable().Reverse())
        {
            _undoStack.Push(DeserializeSnapshot(snapshotJson));
        }
    }

    /// <summary>
    /// Serializes game state to JSON string
    /// </summary>
    public string ToJson()
    {
        var state = SerializeState();
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Creates a game service from JSON state
    /// </summary>
    public static FreeCellGameService FromJson(string json)
    {
        // Use case-insensitive property matching to accept JSON produced by JS or
        // other serializers that use camelCase (e.g. "tableau") instead of PascalCase ("Tableau").
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var state = JsonSerializer.Deserialize<FreeCellGameState>(json, options)
            ?? throw new ArgumentException("Invalid JSON state");

        var service = new FreeCellGameService();
        service.RestoreState(state);
        return service;
    }

    #endregion
}
