using Client.Games.Cards.Models;

namespace Client.Games.Cards.Services;

/// <summary>
/// Service for managing Solitaire (Klondike) game state
/// </summary>
public class SolitaireGameService
{
    private readonly Random _random;

    // Game state
    public List<Card> Stock { get; private set; } = new(); // Draw pile
    public List<Card> Waste { get; private set; } = new(); // Drawn cards
    public List<List<Card>> Tableau { get; private set; } = new(); // 7 columns
    public List<List<Card>> Foundations { get; private set; } = new(); // 4 foundation piles (one per suit)

    // Selection state
    public (int sourceType, int sourceIndex, int cardIndex)? Selection { get; set; }
    // sourceType: 0=Waste, 1=Tableau, 2=Foundation

    public int MoveCount { get; private set; }
    public bool IsGameWon => Foundations.All(f => f.Count == 13);

    public SolitaireGameService(Random? random = null)
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

        // Create and shuffle deck
        var deck = new Deck(_random);
        deck.Shuffle();

        // Initialize tableau (7 columns)
        Tableau = new List<List<Card>>();
        for (int col = 0; col < 7; col++)
        {
            var column = new List<Card>();
            for (int row = 0; row <= col; row++)
            {
                var card = deck.Draw();
                if (card != null)
                {
                    // Only the top card of each column is face up
                    card.IsFaceUp = (row == col);
                    column.Add(card);
                }
            }
            Tableau.Add(column);
        }

        // Initialize foundations (4 empty piles)
        Foundations = new List<List<Card>>
        {
            new(), new(), new(), new()
        };

        // Remaining cards go to stock
        Stock = new List<Card>();
        while (deck.Count > 0)
        {
            var card = deck.Draw();
            if (card != null)
            {
                card.IsFaceUp = false;
                Stock.Add(card);
            }
        }

        // Waste starts empty
        Waste = new List<Card>();
    }

    /// <summary>
    /// Draws cards from stock to waste
    /// </summary>
    public void DrawFromStock()
    {
        if (Stock.Count == 0)
        {
            // Reset stock from waste
            while (Waste.Count > 0)
            {
                var card = Waste[^1];
                Waste.RemoveAt(Waste.Count - 1);
                card.IsFaceUp = false;
                Stock.Add(card);
            }
        }
        else
        {
            // Draw one card (standard Klondike draws 1 or 3)
            var card = Stock[^1];
            Stock.RemoveAt(Stock.Count - 1);
            card.IsFaceUp = true;
            Waste.Add(card);
        }
    }

    /// <summary>
    /// Attempts to move selected cards to a target
    /// </summary>
    public bool TryMove(int targetType, int targetIndex)
    {
        if (Selection == null) return false;

        var (sourceType, sourceIndex, cardIndex) = Selection.Value;
        List<Card> sourceCards;
        List<Card> cardsToMove;

        // Get source cards
        switch (sourceType)
        {
            case 0: // Waste
                if (Waste.Count == 0) return false;
                sourceCards = Waste;
                cardsToMove = new List<Card> { Waste[^1] };
                break;
            case 1: // Tableau
                if (sourceIndex < 0 || sourceIndex >= Tableau.Count) return false;
                sourceCards = Tableau[sourceIndex];
                if (cardIndex < 0 || cardIndex >= sourceCards.Count) return false;
                cardsToMove = sourceCards.Skip(cardIndex).ToList();
                break;
            case 2: // Foundation
                if (sourceIndex < 0 || sourceIndex >= Foundations.Count) return false;
                sourceCards = Foundations[sourceIndex];
                if (sourceCards.Count == 0) return false;
                cardsToMove = new List<Card> { sourceCards[^1] };
                break;
            default:
                return false;
        }

        if (cardsToMove.Count == 0) return false;

        // Try to place on target
        bool success = false;
        switch (targetType)
        {
            case 1: // Tableau
                if (targetIndex >= 0 && targetIndex < Tableau.Count)
                {
                    success = CanPlaceOnTableau(cardsToMove[0], Tableau[targetIndex]);
                    if (success)
                    {
                        Tableau[targetIndex].AddRange(cardsToMove);
                    }
                }
                break;
            case 2: // Foundation
                if (targetIndex >= 0 && targetIndex < Foundations.Count && cardsToMove.Count == 1)
                {
                    success = CanPlaceOnFoundation(cardsToMove[0], Foundations[targetIndex]);
                    if (success)
                    {
                        Foundations[targetIndex].Add(cardsToMove[0]);
                    }
                }
                break;
        }

        if (success)
        {
            // Remove from source
            switch (sourceType)
            {
                case 0:
                    Waste.RemoveAt(Waste.Count - 1);
                    break;
                case 1:
                    Tableau[sourceIndex].RemoveRange(cardIndex, cardsToMove.Count);
                    // Flip the new top card if it exists
                    if (Tableau[sourceIndex].Count > 0)
                    {
                        Tableau[sourceIndex][^1].IsFaceUp = true;
                    }
                    break;
                case 2:
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
            case 0: // Waste
                if (Waste.Count > 0) card = Waste[^1];
                break;
            case 1: // Tableau
                if (sourceIndex >= 0 && sourceIndex < Tableau.Count && Tableau[sourceIndex].Count > 0)
                {
                    card = Tableau[sourceIndex][^1];
                    cardIndex = Tableau[sourceIndex].Count - 1;
                }
                break;
        }

        if (card == null || !card.IsFaceUp) return false;

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
    /// Checks if a card can be placed on a tableau column
    /// </summary>
    private bool CanPlaceOnTableau(Card card, List<Card> column)
    {
        if (column.Count == 0)
        {
            // Only Kings can go on empty columns
            return card.Rank == Rank.King;
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

    /// <summary>
    /// Clears the current selection
    /// </summary>
    public void ClearSelection()
    {
        Selection = null;
    }

    /// <summary>
    /// Selects a card or stack of cards
    /// </summary>
    public void Select(int sourceType, int sourceIndex, int cardIndex = -1)
    {
        Selection = (sourceType, sourceIndex, cardIndex);
    }
}
