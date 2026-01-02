using Client.Games.Cards.Models;

namespace Client.Games.Cards.Services;

/// <summary>
/// Service for managing FreeCell game state
/// </summary>
public class FreeCellGameService
{
    private readonly Random _random;

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
    /// Initializes a new game
    /// </summary>
    public void InitializeGame()
    {
        MoveCount = 0;
        Selection = null;
        _undoStack.Clear();

        // Create and shuffle deck
        var deck = new Deck(_random);
        deck.Shuffle();

        // Initialize 4 free cells (all empty)
        FreeCells = new List<Card?> { null, null, null, null };

        // Initialize 4 foundations (empty)
        Foundations = new List<List<Card>>
        {
            new(), new(), new(), new()
        };

        // Initialize 8 tableau columns
        // First 4 columns get 7 cards, last 4 columns get 6 cards
        Tableau = new List<List<Card>>();
        for (int col = 0; col < 8; col++)
        {
            Tableau.Add(new List<Card>());
        }

        // Deal all 52 cards face up to tableau
        int column = 0;
        while (deck.Count > 0)
        {
            var card = deck.Draw();
            if (card != null)
            {
                card.IsFaceUp = true; // All cards face up in FreeCell
                Tableau[column].Add(card);
                column = (column + 1) % 8;
            }
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
}
