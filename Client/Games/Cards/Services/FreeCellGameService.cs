using Client.Games.Cards.Models;
using System.Text;
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
/// Service for managing FreeCell game state
/// </summary>
public class FreeCellGameService
{
    private readonly Random _random;

    // Game identification
    public int GameId { get; private set; }

    // Game state
    public List<List<Card>> Tableau { get; private set; } = new(); // 8 columns
    public List<Card?> FreeCells { get; private set; } = new(); // 4 free cells
    public List<List<Card>> Foundations { get; private set; } = new(); // 4 foundation piles

    // Selection state
    public (int sourceType, int sourceIndex, int cardIndex)? Selection { get; set; }
    // sourceType: 0=FreeCell, 1=Tableau, 2=Foundation

    // Undo support
    private readonly Stack<GameSnapshot> _undoStack = new();
    private record GameSnapshot(
        List<List<Card>> Tableau,
        List<Card?> FreeCells,
        List<List<Card>> Foundations,
        int MoveCount
    );

    public int MoveCount { get; private set; }
    public bool IsGameWon => Foundations.All(f => f.Count == 13);
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
    /// </summary>
    public void InitializeGame(int gameId)
    {
        GameId = gameId;
        MoveCount = 0;
        Selection = null;
        _undoStack.Clear();

        // Use the game ID as seed for deterministic shuffling
        var gameRandom = new Random(gameId);

        // Create deck in standard order (same as classic FreeCell)
        var cards = new List<Card>();
        // Classic FreeCell order: Clubs, Diamonds, Hearts, Spades (A-K each)
        foreach (Suit suit in new[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades })
        {
            foreach (Rank rank in Enum.GetValues<Rank>())
            {
                cards.Add(new Card(suit, rank, true)); // All face up
            }
        }

        // Shuffle using the same algorithm as classic FreeCell (Fisher-Yates)
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = gameRandom.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

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

        // Deal all 52 cards to tableau (round-robin)
        for (int i = 0; i < cards.Count; i++)
        {
            Tableau[i % 8].Add(cards[i]);
        }
    }

    /// <summary>
    /// Gets the number of empty free cells
    /// </summary>
    public int EmptyFreeCellCount => FreeCells.Count(c => c == null);

    /// <summary>
    /// Gets the number of empty tableau columns
    /// </summary>
    public int EmptyTableauCount => Tableau.Count(col => col.Count == 0);

    /// <summary>
    /// Calculates the maximum number of cards that can be moved as a stack
    /// Formula: (1 + empty free cells) * 2^(empty columns)
    /// </summary>
    public int MaxMovableCards
    {
        get
        {
            int emptyFreeCells = EmptyFreeCellCount;
            int emptyColumns = EmptyTableauCount;
            return (1 + emptyFreeCells) * (int)Math.Pow(2, emptyColumns);
        }
    }

    /// <summary>
    /// Selects a card or stack of cards
    /// </summary>
    public void Select(int sourceType, int sourceIndex, int cardIndex = -1)
    {
        Selection = (sourceType, sourceIndex, cardIndex);
    }

    /// <summary>
    /// Clears the current selection
    /// </summary>
    public void ClearSelection()
    {
        Selection = null;
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

    /// <summary>
    /// Attempts to move selected cards to a target
    /// </summary>
    public bool TryMove(int targetType, int targetIndex)
    {
        if (Selection == null) return false;

        var (sourceType, sourceIndex, cardIndex) = Selection.Value;
        List<Card> cardsToMove = new();
        
        // Get cards to move
        switch (sourceType)
        {
            case 0: // FreeCell
                if (sourceIndex < 0 || sourceIndex >= 4) return false;
                var freeCard = FreeCells[sourceIndex];
                if (freeCard == null) return false;
                cardsToMove.Add(freeCard);
                break;
            case 1: // Tableau
                if (sourceIndex < 0 || sourceIndex >= Tableau.Count) return false;
                var column = Tableau[sourceIndex];
                if (cardIndex < 0 || cardIndex >= column.Count) return false;
                cardsToMove = column.Skip(cardIndex).ToList();
                
                // Validate the stack is properly ordered (descending, alternating colors)
                if (!IsValidTableauStack(cardsToMove)) return false;
                
                // Check if we can move this many cards
                int maxMovable = CalculateMaxMovableCards(targetType, targetIndex);
                if (cardsToMove.Count > maxMovable) return false;
                break;
            case 2: // Foundation
                if (sourceIndex < 0 || sourceIndex >= Foundations.Count) return false;
                if (Foundations[sourceIndex].Count == 0) return false;
                cardsToMove.Add(Foundations[sourceIndex][^1]);
                break;
            default:
                return false;
        }

        if (cardsToMove.Count == 0) return false;

        // Try to place on target
        bool success = false;
        switch (targetType)
        {
            case 0: // FreeCell
                if (targetIndex >= 0 && targetIndex < 4 && cardsToMove.Count == 1)
                {
                    if (FreeCells[targetIndex] == null)
                    {
                        // Save state before move
                        _undoStack.Push(CaptureSnapshot());
                        
                        FreeCells[targetIndex] = cardsToMove[0];
                        success = true;
                    }
                }
                break;
            case 1: // Tableau
                if (targetIndex >= 0 && targetIndex < Tableau.Count)
                {
                    if (CanPlaceOnTableau(cardsToMove[0], Tableau[targetIndex]))
                    {
                        // Save state before move
                        _undoStack.Push(CaptureSnapshot());
                        
                        Tableau[targetIndex].AddRange(cardsToMove);
                        success = true;
                    }
                }
                break;
            case 2: // Foundation
                if (targetIndex >= 0 && targetIndex < Foundations.Count && cardsToMove.Count == 1)
                {
                    if (CanPlaceOnFoundation(cardsToMove[0], Foundations[targetIndex]))
                    {
                        // Save state before move
                        _undoStack.Push(CaptureSnapshot());
                        
                        Foundations[targetIndex].Add(cardsToMove[0]);
                        success = true;
                    }
                }
                break;
        }

        if (success)
        {
            // Remove from source
            switch (sourceType)
            {
                case 0: // FreeCell
                    FreeCells[sourceIndex] = null;
                    break;
                case 1: // Tableau
                    Tableau[sourceIndex].RemoveRange(cardIndex, cardsToMove.Count);
                    break;
                case 2: // Foundation
                    Foundations[sourceIndex].RemoveAt(Foundations[sourceIndex].Count - 1);
                    break;
            }

            MoveCount++;
            Selection = null;
        }

        return success;
    }

    /// <summary>
    /// Attempts to auto-move a card to foundation
    /// </summary>
    public bool TryAutoMoveToFoundation(int sourceType, int sourceIndex, int cardIndex = -1)
    {
        Card? card = null;

        switch (sourceType)
        {
            case 0: // FreeCell
                if (sourceIndex >= 0 && sourceIndex < 4)
                    card = FreeCells[sourceIndex];
                break;
            case 1: // Tableau
                if (sourceIndex >= 0 && sourceIndex < Tableau.Count && Tableau[sourceIndex].Count > 0)
                {
                    card = Tableau[sourceIndex][^1];
                    cardIndex = Tableau[sourceIndex].Count - 1;
                }
                break;
        }

        if (card == null) return false;

        // Find appropriate foundation
        for (int i = 0; i < Foundations.Count; i++)
        {
            if (CanPlaceOnFoundation(card, Foundations[i]))
            {
                Selection = (sourceType, sourceIndex, cardIndex);
                return TryMove(2, i);
            }
        }

        return false;
    }

    /// <summary>
    /// Auto-moves all possible cards to foundations
    /// </summary>
    public int AutoMoveToFoundations()
    {
        int movesMade = 0;
        bool madeMove;

        do
        {
            madeMove = false;

            // Try free cells
            for (int i = 0; i < 4; i++)
            {
                if (FreeCells[i] != null && TryAutoMoveToFoundation(0, i))
                {
                    madeMove = true;
                    movesMade++;
                }
            }

            // Try tableau columns
            for (int i = 0; i < 8; i++)
            {
                if (Tableau[i].Count > 0 && TryAutoMoveToFoundation(1, i))
                {
                    madeMove = true;
                    movesMade++;
                }
            }
        } while (madeMove);

        return movesMade;
    }

    /// <summary>
    /// Checks if the game is in a trivially winnable state.
    /// A game is trivially winnable when all cards in tableau are in descending sequences
    /// and can be moved to foundations without any complex moves.
    /// </summary>
    public bool IsTriviallyWinnable()
    {
        // If already won, return false (nothing to do)
        if (IsGameWon) return false;

        // Check if all tableau columns are in valid descending sequences from top to bottom
        foreach (var column in Tableau)
        {
            if (column.Count <= 1) continue;

            // Check if the entire column is a valid descending sequence
            for (int i = 0; i < column.Count - 1; i++)
            {
                var current = column[i];
                var next = column[i + 1];

                // Cards must be in descending order (not necessarily alternating colors for winnability check)
                if ((int)current.Rank <= (int)next.Rank)
                {
                    return false; // Out of order - not trivially winnable
                }
            }
        }

        // Check free cells - all cards in free cells must be playable to foundations eventually
        // For simplicity, we consider it trivially winnable if tableau is all in order
        // The free cell cards will be moved when their turn comes

        return true;
    }

    /// <summary>
    /// Gets the next card that can be moved to a foundation.
    /// Returns the source info (sourceType, sourceIndex, cardIndex) or null if none found.
    /// </summary>
    public (int sourceType, int sourceIndex, int cardIndex)? GetNextFoundationMove()
    {
        // Check free cells first
        for (int i = 0; i < 4; i++)
        {
            var card = FreeCells[i];
            if (card != null && CanMoveToAnyFoundation(card))
            {
                return (0, i, 0);
            }
        }

        // Check tableau columns (top card only)
        for (int col = 0; col < 8; col++)
        {
            if (Tableau[col].Count > 0)
            {
                var card = Tableau[col][^1];
                if (CanMoveToAnyFoundation(card))
                {
                    return (1, col, Tableau[col].Count - 1);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a card can be moved to any foundation pile
    /// </summary>
    private bool CanMoveToAnyFoundation(Card card)
    {
        for (int i = 0; i < Foundations.Count; i++)
        {
            if (CanPlaceOnFoundation(card, Foundations[i]))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Performs one step of auto-solve by moving a card to foundation.
    /// Returns info about the move made, or null if no move possible.
    /// </summary>
    public (int sourceType, int sourceIndex, Card card)? AutoSolveStep()
    {
        var nextMove = GetNextFoundationMove();
        if (nextMove == null) return null;

        var (sourceType, sourceIndex, cardIndex) = nextMove.Value;
        
        Card? card = sourceType switch
        {
            0 => FreeCells[sourceIndex],
            1 => Tableau[sourceIndex].Count > 0 ? Tableau[sourceIndex][^1] : null,
            _ => null
        };

        if (card == null) return null;

        // Make a copy before move
        var movedCard = new Card(card.Suit, card.Rank, true);

        Selection = nextMove;
        if (TryAutoMoveToFoundation(sourceType, sourceIndex, cardIndex))
        {
            return (sourceType, sourceIndex, movedCard);
        }

        return null;
    }

    /// <summary>
    /// Checks if selecting from a specific card index in a tableau column forms a valid sequence
    /// </summary>
    public bool IsValidTableauSequence(int columnIndex, int cardIndex)
    {
        if (columnIndex < 0 || columnIndex >= Tableau.Count) return false;
        var column = Tableau[columnIndex];
        if (cardIndex < 0 || cardIndex >= column.Count) return false;

        var cards = column.Skip(cardIndex).ToList();
        return IsValidTableauStack(cards);
    }

    /// <summary>
    /// Validates that a stack of cards is properly ordered for tableau movement
    /// </summary>
    private bool IsValidTableauStack(List<Card> cards)
    {
        if (cards.Count <= 1) return true;

        for (int i = 0; i < cards.Count - 1; i++)
        {
            var current = cards[i];
            var next = cards[i + 1];

            // Must be descending rank and alternating colors
            if ((int)current.Rank != (int)next.Rank + 1) return false;
            if (current.IsRed == next.IsRed) return false;
        }

        return true;
    }

    /// <summary>
    /// Calculates max movable cards considering the target
    /// </summary>
    private int CalculateMaxMovableCards(int targetType, int targetIndex)
    {
        int emptyFreeCells = EmptyFreeCellCount;
        int emptyColumns = EmptyTableauCount;

        // If moving to an empty column, we have one fewer empty column to use
        if (targetType == 1 && targetIndex >= 0 && targetIndex < Tableau.Count && Tableau[targetIndex].Count == 0)
        {
            emptyColumns = Math.Max(0, emptyColumns - 1);
        }

        return (1 + emptyFreeCells) * (int)Math.Pow(2, emptyColumns);
    }

    /// <summary>
    /// Checks if a card can be placed on a tableau column
    /// </summary>
    private bool CanPlaceOnTableau(Card card, List<Card> column)
    {
        if (column.Count == 0)
        {
            // Any card can go on an empty column in FreeCell
            return true;
        }

        var topCard = column[^1];
        // Must be opposite color and one rank lower
        return card.IsRed != topCard.IsRed && (int)card.Rank == (int)topCard.Rank - 1;
    }

    /// <summary>
    /// Checks if a card can be placed on a foundation pile
    /// </summary>
    private bool CanPlaceOnFoundation(Card card, List<Card> foundation)
    {
        if (foundation.Count == 0)
        {
            // Only Aces can start a foundation
            return card.Rank == Rank.Ace;
        }

        var topCard = foundation[^1];
        // Must be same suit and one rank higher
        return card.Suit == topCard.Suit && (int)card.Rank == (int)topCard.Rank + 1;
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
        var state = JsonSerializer.Deserialize<FreeCellGameState>(json)
            ?? throw new ArgumentException("Invalid JSON state");
        
        var service = new FreeCellGameService();
        service.RestoreState(state);
        return service;
    }

    #endregion
}
