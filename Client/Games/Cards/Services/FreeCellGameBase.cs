using Client.Games.Cards.Models;
using System.Text.RegularExpressions;

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
    public List<List<Card>> Tableau { get; protected set; } = []; // 8 columns
    public List<Card?> FreeCells { get; protected set; } = []; // 4 free cells
    public List<List<Card>> Foundations { get; protected set; } = []; // 4 foundation piles

    // Selection state
    public CardSelection? Selection { get; set; }

    /// <summary>
    /// Gets or sets the number of moves made in the current game or session. Same as tree depth for solver.
    /// </summary>
    public int MoveCount { get; set; }
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
        FreeCells = [null, null, null, null];
        Foundations = [[], [], [], []];
        Tableau = [];
        for (int col = 0; col < 8; col++)
        {
            Tableau.Add([]);
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

    // Precomputed 2-char card codes indexed by (suit * 13 + rank). Index 0 unused.
    private static readonly string[] CardCodes = InitCardCodes();

    private static string[] InitCardCodes()
    {
        var codes = new string[53];
        const string suits = "HDCS";
        const string ranks = "A23456789TJQK";
        for (int s = 0; s < 4; s++)
            for (int r = 0; r < 13; r++)
                codes[s * 13 + r + 1] = $"{ranks[r]}{suits[s]}";
        return codes;
    }

    private static int CardToIndex(Card c) => (int)c.Suit * 13 + (int)c.Rank;

    /// <summary>
    /// Generates a canonical hash of the game state for cycle detection.
    /// Two identical board positions will have the same hash regardless of how they were reached.
    /// All components are SORTED because:
    /// - FreeCells: Order doesn't matter (swapping cards between free cells is equivalent)
    /// - Foundations: Order doesn't matter (any Ace can start any pile)
    /// - Tableau: Column order doesn't matter (swapping columns is equivalent)
    /// Optimized to minimize allocations: uses precomputed card codes, stack-allocated
    /// sorting for fixed-size collections, and direct StringBuilder writes.
    /// </summary>
    public string GetStateHash()
    {
        var sb = new System.Text.StringBuilder(256);

        // FreeCells - sort by integer key (order doesn't matter)
        Span<int> fcKeys = stackalloc int[4];
        for (int i = 0; i < 4; i++)
        {
            var c = FreeCells[i];
            fcKeys[i] = c != null ? CardToIndex(c) : 0;
        }
        fcKeys.Sort();
        sb.Append("F:");
        for (int i = 0; i < 4; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(fcKeys[i] == 0 ? "_" : CardCodes[fcKeys[i]]);
        }

        // Foundations - sort by encoded key (order doesn't matter)
        Span<int> fKeys = stackalloc int[4];
        for (int i = 0; i < 4; i++)
        {
            var f = Foundations[i];
            fKeys[i] = f.Count > 0 ? (int)f[0].Suit * 14 + f.Count : 0;
        }
        fKeys.Sort();
        sb.Append("|P:");
        for (int i = 0; i < 4; i++)
        {
            if (i > 0) sb.Append(',');
            if (fKeys[i] == 0)
                sb.Append('_');
            else
            {
                sb.Append("HDCS"[fKeys[i] / 14]);
                sb.Append(fKeys[i] % 14);
            }
        }

        // Tableau - build per-column string via char[], sort, append
        sb.Append("|T:");
        var columnStrings = new string[8];
        for (int col = 0; col < 8; col++)
        {
            var column = Tableau[col];
            if (column.Count == 0) { columnStrings[col] = ""; continue; }
            var chars = new char[column.Count * 2];
            for (int j = 0; j < column.Count; j++)
            {
                var code = CardCodes[CardToIndex(column[j])];
                chars[j * 2] = code[0];
                chars[j * 2 + 1] = code[1];
            }
            columnStrings[col] = new string(chars);
        }
        Array.Sort(columnStrings, StringComparer.Ordinal);
        for (int i = 0; i < 8; i++)
        {
            if (i > 0) sb.Append('|');
            sb.Append(columnStrings[i]);
        }

        return sb.ToString();
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
            if (card != null && CanMoveToAnyFoundation(card) >= 0)
            {
                return new CardSelection(SourceType.FreeCell, i, 0);
            }
        }

        for (int col = 0; col < 8; col++)
        {
            if (Tableau[col].Count > 0)
            {
                var card = Tableau[col][^1];
                if (CanMoveToAnyFoundation(card) >= 0)
                {
                    return new CardSelection(SourceType.Tableau, col, Tableau[col].Count - 1);
                }
            }
        }

        return null;
    }
    /// <summary>
    /// Dump the freecells, tableau and foundation from the gameservice similar to the visual layout for easy verification
    /// </summary>
    public string dumpAllToLog(string desc = "", string indentation = "")
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"{indentation}{desc}\r\n{indentation} FreeCells:");
        for (int i = 0; i < FreeCells.Count; i++)
        {
            var card = FreeCells[i]?.ToString() ?? "   ";
            sb.Append($" {card}");
        }
        sb.Append(" Foundations:");
        for (int i = 0; i < Foundations.Count; i++)
        {
            var cards = Foundations[i];
            var cardStr = cards.Count > 0 ? cards[^1].ToString() : "   ";
            sb.Append($" {cardStr}");
        }
        sb.Append($" BValue: {GetBValue()} Dpth {MoveCount}");
        sb.AppendLine();
        sb.Append(indentation);
        var cnt = Tableau.Max(c => c.Count);
        for (int row = 0; row < cnt; row++)
        {
            for (int col = 0; col < Tableau.Count; col++)
            {
                var card = row < Tableau[col].Count ? Tableau[col][row].ToString() : "   ";
                sb.Append($" {card}");
            }
            sb.AppendLine();
            if (row < cnt -1)
            {
                sb.Append(indentation);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Checks if a card can be moved to any foundation pile.
    /// Returns the foundation index (0-3) if successful, or -1 if no foundation can accept the card.
    /// </summary>
    public int CanMoveToAnyFoundation(Card card)
    {
        for (int i = 0; i < Foundations.Count; i++)
        {
            if (CanPlaceOnFoundation(card, Foundations[i]))
            {
                return i;
            }
        }
        return -1;
    }
    public int FindAnyFreeCell()
    {
        var result = -1;
        for (int i = 0; i < 4; i++)
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

    /// <summary>
    /// Board evaluator. Sum of bottom sequence lengths across all tableau columns, plus foundation lengths, plus bonus for empty columns.
    /// </summary>
    /// <returns></returns>
    public int GetBValue()
    {
        var totalBValue = 0;
        for (int col = 0; col < Tableau.Count; col++)
        {
            var column = Tableau[col];
            if (column.Count == 0) // empty column
            {
                totalBValue += 3; // bonus for empty column
            }
            else
            {
                totalBValue += GetBottomSequenceLength(col) - 1; // a sequence of 2 cards adds 1 point, 3 cards adds 2 points, etc.
            }
        }
        // also add all the foundation lengths
        var foundationLengths = Foundations.Select(f => f.Count).Sum();
        totalBValue += foundationLengths * 2; // if a card was in a seq in tableau, but moved to foundation,  the seq len decreased and the foundation length increased. That's a net wash-out. But it's worth more in foundation, so double it.
        return totalBValue;
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

            if (CanMoveToAnyFoundation(card) >= 0) return true;

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
            if (CanMoveToAnyFoundation(topCard) >= 0) return true;

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
        if (i < 0 || i >= 4 || dstCol < 0 || dstCol >= Tableau.Count) return false;
        var card = FreeCells[i];
        if (card == null) return false;
        return CanPlaceOnTableau(card, Tableau[dstCol]);
    }

    /// <summary>
    /// Verifies the board is a valid FreeCell game state.
    /// Checks: exactly 52 cards, no duplicates, all suits and ranks present,
    /// correct structure sizes, and valid foundation ordering.
    /// Throws <see cref="InvalidOperationException"/> with a descriptive message if invalid.
    /// </summary>
    public void VerifyGame()
    {
        if (Tableau.Count != 8)
            throw new InvalidOperationException($"Tableau must have 8 columns, found {Tableau.Count}");
        if (FreeCells.Count != 4)
            throw new InvalidOperationException($"FreeCells must have 4 slots, found {FreeCells.Count}");
        if (Foundations.Count != 4)
            throw new InvalidOperationException($"Foundations must have 4 piles, found {Foundations.Count}");

        // Validate foundation piles: each must be A,2,3,...,N of a single suit
        for (int i = 0; i < 4; i++)
        {
            var pile = Foundations[i];
            if (pile.Count == 0) continue;

            var suit = pile[0].Suit;
            if (pile[0].Rank != Rank.Ace)
                throw new InvalidOperationException($"Foundation {i}: first card must be Ace, found {pile[0]}");

            for (int j = 0; j < pile.Count; j++)
            {
                if (pile[j].Suit != suit)
                    throw new InvalidOperationException($"Foundation {i}: card at position {j} is {pile[j]}, expected suit {suit}");
                if ((int)pile[j].Rank != j + 1)
                    throw new InvalidOperationException($"Foundation {i}: card at position {j} is {pile[j]}, expected rank {(Rank)(j + 1)}");
            }
        }

        // Collect all cards with their locations for error reporting
        var allCards = new List<(Card card, string location)>();

        for (int i = 0; i < FreeCells.Count; i++)
        {
            if (FreeCells[i] != null)
                allCards.Add((FreeCells[i]!, $"FreeCell[{i}]"));
        }

        for (int i = 0; i < Foundations.Count; i++)
        {
            for (int j = 0; j < Foundations[i].Count; j++)
                allCards.Add((Foundations[i][j], $"Foundation[{i}][{j}]"));
        }

        for (int col = 0; col < Tableau.Count; col++)
        {
            for (int row = 0; row < Tableau[col].Count; row++)
                allCards.Add((Tableau[col][row], $"Tableau[{col}][{row}]"));
        }

        if (allCards.Count != 52)
            throw new InvalidOperationException($"Board must have exactly 52 cards, found {allCards.Count}");

        // Check for duplicates
        var seen = new Dictionary<(Suit, Rank), string>();
        foreach (var (card, location) in allCards)
        {
            var key = (card.Suit, card.Rank);
            if (seen.TryGetValue(key, out var firstLocation))
                throw new InvalidOperationException($"Duplicate card {card}: found at {firstLocation} and {location}");
            seen[key] = location;
        }

        // Verify all 52 unique cards are present
        foreach (Suit suit in Enum.GetValues<Suit>())
        {
            for (int r = 1; r <= 13; r++)
            {
                var rank = (Rank)r;
                if (!seen.ContainsKey((suit, rank)))
                    throw new InvalidOperationException($"Missing card: {rank} of {suit}");
            }
        }
    }
}
