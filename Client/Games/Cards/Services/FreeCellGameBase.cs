using Client.Games.Cards.Models;

namespace Client.Games.Cards.Services;

/// <summary>
/// Source of a card selection or move target
/// </summary>
public enum SourceType
{
    FreeCell = 0,
    Tableau = 1,
    Foundation = 2
}

/// <summary>
/// Represents a card selection with source location
/// </summary>
/// <param name="SourceType">The type of source (FreeCell, Tableau, or Foundation)</param>
/// <param name="SourceIndex">Index within the source (column index for Tableau, cell index for FreeCell, pile index for Foundation)</param>
/// <param name="CardIndex">Index of the card within the source (for Tableau stacks)</param>
public readonly record struct CardSelection(SourceType SourceType, int SourceIndex, int CardIndex);

/// <summary>
/// Lightweight base class for FreeCell game state and move logic.
/// Does NOT include undo support - use FreeCellGameService for full features.
/// Designed for efficient solver calculations.
/// </summary>
public class FreeCellGameBase
{
    // Game identification
    public int GameId { get; protected set; }

    // Game state
    public List<List<Card>> Tableau { get; protected set; } = new(); // 8 columns
    public List<Card?> FreeCells { get; protected set; } = new(); // 4 free cells
    public List<List<Card>> Foundations { get; protected set; } = new(); // 4 foundation piles

    // Selection state
    public CardSelection? Selection { get; set; }

    public int MoveCount { get; protected set; }
    public bool IsGameWon => Foundations.All(f => f.Count == 13);
    public bool AutoMoveToFoundationDisable = false; // Set to true to allow auto-move to foundation. Used by autosolver.

    /// <summary>
    /// Checks if the game is in a stalemate (no valid moves available).
    /// </summary>
    public bool IsStalemate
    {
        get
        {
            if (IsGameWon) return false;
            return !HasAnyValidMove();
        }
    }

    public FreeCellGameBase()
    {
        InitializeEmpty();
    }

    /// <summary>
    /// Initializes empty game structure (no cards dealt)
    /// </summary>
    protected void InitializeEmpty()
    {
        MoveCount = 0;
        Selection = null;
        FreeCells = new List<Card?> { null, null, null, null };
        Foundations = new List<List<Card>> { new(), new(), new(), new() };
        Tableau = new List<List<Card>>();
        for (int col = 0; col < 8; col++)
        {
            Tableau.Add(new List<Card>());
        }
    }

    /// <summary>
    /// Creates a deep copy of this game state
    /// </summary>
    public FreeCellGameBase Clone()
    {
        var clone = new FreeCellGameBase
        {
            GameId = GameId,
            MoveCount = MoveCount,
            Selection = Selection,
            Tableau = Tableau.Select(col => col.Select(c => new Card(c.Suit, c.Rank, c.IsFaceUp)).ToList()).ToList(),
            FreeCells = FreeCells.Select(c => c != null ? new Card(c.Suit, c.Rank, c.IsFaceUp) : null).ToList(),
            Foundations = Foundations.Select(f => f.Select(c => new Card(c.Suit, c.Rank, c.IsFaceUp)).ToList()).ToList()
        };
        return clone;
    }

    /// <summary>
    /// Copies state from another game instance
    /// </summary>
    public void CopyFrom(FreeCellGameBase other)
    {
        GameId = other.GameId;
        MoveCount = other.MoveCount;
        Selection = other.Selection;
        Tableau = other.Tableau.Select(col => col.Select(c => new Card(c.Suit, c.Rank, c.IsFaceUp)).ToList()).ToList();
        FreeCells = other.FreeCells.Select(c => c != null ? new Card(c.Suit, c.Rank, c.IsFaceUp) : null).ToList();
        Foundations = other.Foundations.Select(f => f.Select(c => new Card(c.Suit, c.Rank, c.IsFaceUp)).ToList()).ToList();
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
    public void Select(SourceType sourceType, int sourceIndex, int cardIndex = -1)
    {
        Selection = new CardSelection(sourceType, sourceIndex, cardIndex);
    }

    /// <summary>
    /// Clears the current selection
    /// </summary>
    public void ClearSelection()
    {
        Selection = null;
    }

    /// <summary>
    /// Called before a successful move. Override to add undo support.
    /// </summary>
    protected virtual void OnBeforeMove() { }

    /// <summary>
    /// Attempts to move selected cards to a target
    /// </summary>
    public bool TryMove(SourceType targetType, int targetIndex)
    {
        if (Selection == null) return false;

        var (sourceType, sourceIndex, cardIndex) = (Selection.Value.SourceType, Selection.Value.SourceIndex, Selection.Value.CardIndex);
        List<Card> cardsToMove = new();

        // Get cards to move
        switch (sourceType)
        {
            case SourceType.FreeCell:
                if (sourceIndex < 0 || sourceIndex >= 4) return false;
                var freeCard = FreeCells[sourceIndex];
                if (freeCard == null) return false;
                cardsToMove.Add(freeCard);
                break;
            case SourceType.Tableau:
                if (sourceIndex < 0 || sourceIndex >= Tableau.Count) return false;
                var column = Tableau[sourceIndex];
                if (cardIndex < 0 || cardIndex >= column.Count) return false;
                if (!IsValidTableauStack(column, cardIndex)) return false;
                int cardCount = column.Count - cardIndex;
                int maxMovable = CalculateMaxMovableCards(targetType, targetIndex);
                if (cardCount > maxMovable) return false;
                cardsToMove = column.GetRange(cardIndex, cardCount);
                break;
            case SourceType.Foundation:
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
            case SourceType.FreeCell:
                if (targetIndex >= 0 && targetIndex < 4 && cardsToMove.Count == 1)
                {
                    if (FreeCells[targetIndex] == null)
                    {
                        OnBeforeMove();
                        FreeCells[targetIndex] = cardsToMove[0];
                        success = true;
                    }
                }
                break;
            case SourceType.Tableau:
                if (targetIndex >= 0 && targetIndex < Tableau.Count)
                {
                    if (CanPlaceOnTableau(cardsToMove[0], Tableau[targetIndex]))
                    {
                        OnBeforeMove();
                        Tableau[targetIndex].AddRange(cardsToMove);
                        success = true;
                    }
                }
                break;
            case SourceType.Foundation:
                if (targetIndex >= 0 && targetIndex < Foundations.Count && cardsToMove.Count == 1)
                {
                    if (CanPlaceOnFoundation(cardsToMove[0], Foundations[targetIndex]))
                    {
                        OnBeforeMove();
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
                case SourceType.FreeCell:
                    FreeCells[sourceIndex] = null;
                    break;
                case SourceType.Tableau:
                    Tableau[sourceIndex].RemoveRange(cardIndex, cardsToMove.Count);
                    break;
                case SourceType.Foundation:
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
    public bool TryAutoMoveToFoundation(SourceType sourceType, int sourceIndex, int cardIndex = -1)
    {
        Card? card = null;

        switch (sourceType)
        {
            case SourceType.FreeCell:
                if (sourceIndex >= 0 && sourceIndex < 4)
                    card = FreeCells[sourceIndex];
                break;
            case SourceType.Tableau:
                if (sourceIndex >= 0 && sourceIndex < Tableau.Count && Tableau[sourceIndex].Count > 0)
                {
                    card = Tableau[sourceIndex][^1];
                    cardIndex = Tableau[sourceIndex].Count - 1;
                }
                break;
        }

        if (card == null) return false;

        for (int i = 0; i < Foundations.Count; i++)
        {
            if (CanPlaceOnFoundation(card, Foundations[i]))
            {
                Selection = new CardSelection(sourceType, sourceIndex, cardIndex);
                return TryMove(SourceType.Foundation, i);
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

            for (int i = 0; i < 4; i++)
            {
                if (FreeCells[i] != null && TryAutoMoveToFoundation(SourceType.FreeCell, i))
                {
                    madeMove = true;
                    movesMade++;
                }
            }

            for (int i = 0; i < 8; i++)
            {
                if (Tableau[i].Count > 0 && TryAutoMoveToFoundation(SourceType.Tableau, i))
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
    /// </summary>
    public (bool isWinnable, string reason) IsTriviallyWinnableWithReason()
    {
        if (IsGameWon) return (false, "Game already won");

        for (int colIdx = 0; colIdx < Tableau.Count; colIdx++)
        {
            var column = Tableau[colIdx];
            if (column.Count <= 1) continue;

            for (int i = 0; i < column.Count - 1; i++)
            {
                var current = column[i];
                var next = column[i + 1];

                if ((int)current.Rank < (int)next.Rank)
                {
                    return (false, $"Column {colIdx + 1}: {current} (rank {(int)current.Rank}) is not >= {next} (rank {(int)next.Rank}) at position {i}");
                }
            }
        }

        return (true, "All columns in descending order");
    }

    /// <summary>
    /// Checks if the game is in a trivially winnable state.
    /// </summary>
    public bool IsTriviallyWinnable()
    {
        return IsTriviallyWinnableWithReason().isWinnable;
    }

    /// <summary>
    /// Gets the next card that can be moved to a foundation.
    /// </summary>
    public CardSelection? GetNextFoundationMove()
    {
        for (int i = 0; i < 4; i++)
        {
            var card = FreeCells[i];
            if (card != null && CanMoveToAnyFoundation(card))
            {
                return new CardSelection(SourceType.FreeCell, i, 0);
            }
        }

        for (int col = 0; col < 8; col++)
        {
            if (Tableau[col].Count > 0)
            {
                var card = Tableau[col][^1];
                if (CanMoveToAnyFoundation(card))
                {
                    return new CardSelection(SourceType.Tableau, col, Tableau[col].Count - 1);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a card can be moved to any foundation pile
    /// </summary>
    public bool CanMoveToAnyFoundation(Card card)
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
    public int FindAnyFreeCell()
    {
        var result = -1;
        for (int i = 0;i < 4; i ++)
        {
            if (this.FreeCells[i] == null)
            {
                result = i;
                break;
            }
        }
        return result;
    }

    public bool CanMoveTableauToTableau(int sourceCol, int targetCol, int length)
    {
        if (sourceCol < 0 || sourceCol >= Tableau.Count || targetCol < 0 || targetCol >= Tableau.Count) return false;
        if (Tableau[sourceCol].Count == 0) return false;
        var cardToMove = Tableau[sourceCol][^length];
        return CanPlaceOnTableau(cardToMove, Tableau[targetCol]);
    }

    /// <summary>
    /// Performs one step of auto-solve by moving a card to foundation.
    /// </summary>
    public (SourceType sourceType, int sourceIndex, Card card)? AutoMoveStep()
    {
        if (AutoMoveToFoundationDisable) return null; // don't automove when running autosolver
        var nextMove = GetNextFoundationMove();
        if (nextMove == null) return null;

        var (sourceType, sourceIndex, cardIndex) = (nextMove.Value.SourceType, nextMove.Value.SourceIndex, nextMove.Value.CardIndex);

        Card? card = sourceType switch
        {
            SourceType.FreeCell => FreeCells[sourceIndex],
            SourceType.Tableau => Tableau[sourceIndex].Count > 0 ? Tableau[sourceIndex][^1] : null,
            _ => null
        };

        if (card == null) return null;

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

        return IsValidTableauStack(column, cardIndex);
    }

    /// <summary>
    /// Validates that a stack of cards is properly ordered for tableau movement.
    /// </summary>
    protected bool IsValidTableauStack(List<Card> column, int startIndex)
    {
        int count = column.Count - startIndex;
        if (count <= 1) return true;

        for (int i = startIndex; i < column.Count - 1; i++)
        {
            var current = column[i];
            var next = column[i + 1];

            if ((int)current.Rank != (int)next.Rank + 1) return false;
            if (current.IsRed == next.IsRed) return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the starting index of the largest valid tableau sequence at the bottom of a column.
    /// </summary>
    public int GetBottomSequenceStartIndex(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= Tableau.Count) return -1;
        var column = Tableau[columnIndex];
        if (column.Count == 0) return -1;
        if (column.Count == 1) return 0;

        int sequenceStart = column.Count - 1;

        for (int i = column.Count - 2; i >= 0; i--)
        {
            var current = column[i];
            var next = column[i + 1];

            if ((int)current.Rank == (int)next.Rank + 1 && current.IsRed != next.IsRed)
            {
                sequenceStart = i;
            }
            else
            {
                break;
            }
        }

        return sequenceStart;
    }

    /// <summary>
    /// Gets the length of the largest valid tableau sequence at the bottom of a column.
    /// </summary>
    public int GetBottomSequenceLength(int columnIndex)
    {
        int startIndex = GetBottomSequenceStartIndex(columnIndex);
        if (startIndex < 0) return 0;
        return Tableau[columnIndex].Count - startIndex;
    }

    public List<int> GetBottomSequenceLengths()
    {
        List<int> lengths = new();
        for (int col = 0; col < Tableau.Count; col++)
        {
            lengths.Add(GetBottomSequenceLength(col));
        }
        return lengths;
    }

    /// <summary>
    /// Calculates max movable cards considering the target
    /// </summary>
    public int CalculateMaxMovableCards(SourceType targetType, int targetIndex)
    {
        int emptyFreeCells = EmptyFreeCellCount;
        int emptyColumns = EmptyTableauCount;

        if (targetType == SourceType.Tableau && targetIndex >= 0 && targetIndex < Tableau.Count && Tableau[targetIndex].Count == 0)
        {
            emptyColumns = Math.Max(0, emptyColumns - 1);
        }

        return (1 + emptyFreeCells) * (int)Math.Pow(2, emptyColumns);
    }

    /// <summary>
    /// Checks if a card can be placed on a tableau column
    /// </summary>
    public bool CanPlaceOnTableau(Card card, List<Card> column)
    {
        if (column.Count == 0)
        {
            return true;
        }

        var topCard = column[^1];
        return card.IsRed != topCard.IsRed && (int)card.Rank == (int)topCard.Rank - 1;
    }

    /// <summary>
    /// Checks if a card can be placed on a foundation pile
    /// </summary>
    protected bool CanPlaceOnFoundation(Card card, List<Card> foundation)
    {
        if (foundation.Count == 0)
        {
            return card.Rank == Rank.Ace;
        }

        var topCard = foundation[^1];
        return card.Suit == topCard.Suit && (int)card.Rank == (int)topCard.Rank + 1;
    }

    /// <summary>
    /// Checks if there is at least one valid move available.
    /// </summary>
    protected bool HasAnyValidMove()
    {
        for (int i = 0; i < 4; i++)
        {
            var card = FreeCells[i];
            if (card == null) continue;

            if (CanMoveToAnyFoundation(card)) return true;

            for (int col = 0; col < 8; col++)
            {
                if (CanPlaceOnTableau(card, Tableau[col])) return true;
            }
        }

        for (int col = 0; col < 8; col++)
        {
            var column = Tableau[col];
            if (column.Count == 0) continue;

            var topCard = column[^1];
            if (CanMoveToAnyFoundation(topCard)) return true;

            for (int cardIdx = column.Count - 1; cardIdx >= 0; cardIdx--)
            {
                if (!IsValidTableauStack(column, cardIdx)) continue;

                var leadCard = column[cardIdx];
                int stackSize = column.Count - cardIdx;

                if (stackSize == 1 && EmptyFreeCellCount > 0) return true;

                for (int targetCol = 0; targetCol < 8; targetCol++)
                {
                    if (targetCol == col) continue;

                    if (CanPlaceOnTableau(leadCard, Tableau[targetCol]))
                    {
                        int maxMovable = CalculateMaxMovableCards(SourceType.Tableau, targetCol);
                        if (stackSize <= maxMovable) return true;
                    }
                }
            }
        }

        return false;
    }

    public bool CanMoveFreeCellToTableau(int i, int dstCol)
    {
        if (i <0 || i >= 4 || dstCol < 0 || dstCol >= Tableau.Count) return false;
        var card = FreeCells[i];
        if (card == null) return false;
        return CanPlaceOnTableau(card, Tableau[dstCol]);
    }
}
