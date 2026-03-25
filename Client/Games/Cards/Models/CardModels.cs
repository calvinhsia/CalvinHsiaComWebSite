namespace Client.Games.Cards.Models;

/// <summary>
/// Represents a playing card suit
/// </summary>
public enum Suit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

/// <summary>
/// Represents a playing card rank
/// </summary>
public enum Rank
{
    Ace = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13
}

/// <summary>
/// Represents a playing card
/// </summary>
public class Card
{
    // Base URL for card images - using local images for offline support and faster loading
    // Original source: deckofcardsapi.com (public domain)
    // To re-download images, run: Client\wwwroot\img\cards\download-cards.ps1
    private const string CardImageBaseUrl = "/img/cards/";
    private const string CardBackImage = "back.png";

    public Suit Suit { get; set; }
    public Rank Rank { get; set; }
    public bool IsFaceUp { get; set; }

    // Cached image URL - computed once per card instance
    private string? _imageUrl;

    public Card(Suit suit, Rank rank, bool isFaceUp = false)
    {
        Suit = suit;
        Rank = rank;
        IsFaceUp = isFaceUp;
    }

    /// <summary>
    /// Gets the local card image URL.
    /// Format: /img/cards/{rank}{suit}.png
    /// Examples: AS.png (Ace Spades), KH.png (King Hearts), 2D.png (2 Diamonds)
    /// </summary>
    public string ImageUrl
    {
        get
        {
            if (_imageUrl != null) return _imageUrl;

            var rankCode = Rank switch
            {
                Rank.Ace => "A",
                Rank.King => "K",
                Rank.Queen => "Q",
                Rank.Jack => "J",
                Rank.Ten => "0", // Uses 0 for 10 (matches original deckofcardsapi.com naming)
                _ => ((int)Rank).ToString()
            };

            var suitCode = Suit switch
            {
                Suit.Hearts => "H",
                Suit.Diamonds => "D",
                Suit.Clubs => "C",
                Suit.Spades => "S",
                _ => "S"
            };

            _imageUrl = $"{CardImageBaseUrl}{rankCode}{suitCode}.png";
            return _imageUrl;
        }
    }

    /// <summary>
    /// Gets the card back image URL (static, same for all cards)
    /// </summary>
    public static string BackImageUrl => $"{CardImageBaseUrl}{CardBackImage}";

    /// <summary>
    /// Returns true if this card is red (Hearts or Diamonds)
    /// </summary>
    public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;

    /// <summary>
    /// Returns true if this card is black (Clubs or Spades)
    /// </summary>
    public bool IsBlack => Suit == Suit.Clubs || Suit == Suit.Spades;

    /// <summary>
    /// Gets the Unicode symbol for the suit
    /// </summary>
    public string SuitSymbol => Suit switch
    {
        Suit.Hearts => "\u2665",    // ♥
        Suit.Diamonds => "\u2666",  // ♦
        Suit.Clubs => "\u2663",     // ♣
        Suit.Spades => "\u2660",    // ♠
        _ => "?"
    };

    /// <summary>
    /// Gets the display name for the rank
    /// </summary>
    public string RankDisplay => Rank switch
    {
        Rank.Ace => "A",
        Rank.King => "K",
        Rank.Queen => "Q",
        Rank.Jack => "J",
        _ => ((int)Rank).ToString()
    };

    public override string ToString() => $"{RankDisplay,2}{SuitSymbol}"; // 3 char width for alignment (e.g. "10H" vs " 9D")

    /// <summary>
    /// Returns a compact 2-3 character string for hash generation (e.g., "AS", "10H", "KC")
    /// </summary>
    public string ToShortString() => $"{RankDisplay}{Suit.ToString()[0]}";

    /// <summary>
    /// Returns a 2-character serialization string (e.g., "AS", "TH", "KC").
    /// Uses 'T' for Ten to guarantee fixed 2-char width.
    /// Compatible with FreeCellGameService.DeserializeCard format.
    /// </summary>
    public string ToSerializedString()
    {
        char rank = Rank switch
        {
            Rank.Ace => 'A',
            Rank.Ten => 'T',
            Rank.Jack => 'J',
            Rank.Queen => 'Q',
            Rank.King => 'K',
            _ => (char)('0' + (int)Rank)
        };
        char suit = Suit switch
        {
            Suit.Clubs => 'C',
            Suit.Diamonds => 'D',
            Suit.Hearts => 'H',
            Suit.Spades => 'S',
            _ => '?'
        };
        return $"{rank}{suit}";
    }
}

/// <summary>
/// Represents a deck of 52 playing cards
/// </summary>
public class Deck
{
    private readonly List<Card> _cards = new();
    private readonly Random _random;

    public Deck(Random? random = null)
    {
        _random = random ?? new Random();
        Reset();
    }

    /// <summary>
    /// Resets the deck to a full 52-card deck
    /// </summary>
    public void Reset()
    {
        _cards.Clear();
        foreach (Suit suit in Enum.GetValues<Suit>())
        {
            foreach (Rank rank in Enum.GetValues<Rank>())
            {
                _cards.Add(new Card(suit, rank));
            }
        }
    }

    /// <summary>
    /// Shuffles the deck using Fisher-Yates algorithm
    /// </summary>
    public void Shuffle()
    {
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    /// <summary>
    /// Draws a card from the top of the deck
    /// </summary>
    public Card? Draw()
    {
        if (_cards.Count == 0) return null;
        var card = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        return card;
    }

    /// <summary>
    /// Returns the number of cards remaining in the deck
    /// </summary>
    public int Count => _cards.Count;

    /// <summary>
    /// Returns all cards in the deck
    /// </summary>
    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();
}
