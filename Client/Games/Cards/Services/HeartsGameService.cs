using Client.Games.Cards.Models;

namespace Client.Games.Cards.Services;

/// <summary>
/// Represents a player in a Hearts game
/// </summary>
public class HeartsPlayer
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public List<Card> Hand { get; set; } = new();
    public List<Card> TricksTaken { get; set; } = new();
    public int RoundScore { get; set; }
    public int TotalScore { get; set; }
    public bool IsHuman { get; set; }
    public List<Card> CardsToPass { get; set; } = new();
    
    /// <summary>
    /// Gets the point cards this player has taken
    /// </summary>
    public int PointsTaken => TricksTaken.Count(c => c.Suit == Suit.Hearts) + 
                              (TricksTaken.Any(c => c.Suit == Suit.Spades && c.Rank == Rank.Queen) ? 13 : 0);
}

/// <summary>
/// Direction to pass cards in Hearts
/// </summary>
public enum PassDirection
{
    Left,
    Right,
    Across,
    NoPass
}

/// <summary>
/// Game phase in Hearts
/// </summary>
public enum HeartsPhase
{
    Passing,
    Playing,
    RoundEnd,
    GameEnd
}

/// <summary>
/// Service for managing Hearts game state
/// </summary>
public class HeartsGameService
{
    private readonly Random _random;

    // Game configuration
    public int GameEndScore { get; set; } = 100; // Game ends when someone reaches this score

    // Game state
    public List<HeartsPlayer> Players { get; private set; } = new();
    public HeartsPhase Phase { get; private set; }
    public PassDirection CurrentPassDirection { get; private set; }
    public int RoundNumber { get; private set; }
    
    // Trick state
    public List<(int playerIndex, Card card)> CurrentTrick { get; private set; } = new();
    public int LeadPlayerIndex { get; private set; }
    public int CurrentPlayerIndex { get; private set; }
    public Suit? LeadSuit => CurrentTrick.Count > 0 ? CurrentTrick[0].card.Suit : null;
    
    // Hearts state
    public bool HeartsBroken { get; private set; }
    public bool IsFirstTrick { get; private set; }

    public HeartsGameService(Random? random = null)
    {
        _random = random ?? new Random();
        InitializeGame();
    }

    /// <summary>
    /// Initializes a new game with 4 players
    /// </summary>
    public void InitializeGame()
    {
        // Create 4 players (player 0 is human)
        Players = new List<HeartsPlayer>
        {
            new HeartsPlayer { Index = 0, Name = "You", IsHuman = true },
            new HeartsPlayer { Index = 1, Name = "West", IsHuman = false },
            new HeartsPlayer { Index = 2, Name = "North", IsHuman = false },
            new HeartsPlayer { Index = 3, Name = "East", IsHuman = false }
        };

        RoundNumber = 0;
        CurrentPassDirection = PassDirection.Left;
        
        StartNewRound();
    }

    /// <summary>
    /// Starts a new round
    /// </summary>
    public void StartNewRound()
    {
        RoundNumber++;
        HeartsBroken = false;
        IsFirstTrick = true;
        CurrentTrick.Clear();

        // Clear hands and tricks
        foreach (var player in Players)
        {
            player.Hand.Clear();
            player.TricksTaken.Clear();
            player.CardsToPass.Clear();
            player.RoundScore = 0;
        }

        // Deal cards
        var deck = new Deck(_random);
        deck.Shuffle();

        int playerIndex = 0;
        while (deck.Count > 0)
        {
            var card = deck.Draw();
            if (card != null)
            {
                card.IsFaceUp = true;
                Players[playerIndex].Hand.Add(card);
                playerIndex = (playerIndex + 1) % 4;
            }
        }

        // Sort hands
        foreach (var player in Players)
        {
            SortHand(player.Hand);
        }

        // Determine pass direction based on round
        CurrentPassDirection = (RoundNumber % 4) switch
        {
            1 => PassDirection.Left,
            2 => PassDirection.Right,
            3 => PassDirection.Across,
            0 => PassDirection.NoPass,
            _ => PassDirection.Left
        };

        Phase = CurrentPassDirection == PassDirection.NoPass ? HeartsPhase.Playing : HeartsPhase.Passing;

        if (Phase == HeartsPhase.Playing)
        {
            FindStartingPlayer();
        }
    }

    /// <summary>
    /// Sorts a hand by suit then rank
    /// </summary>
    private void SortHand(List<Card> hand)
    {
        hand.Sort((a, b) =>
        {
            // Sort by suit: Spades, Hearts, Diamonds, Clubs
            var suitOrder = new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs };
            int suitCompare = Array.IndexOf(suitOrder, a.Suit).CompareTo(Array.IndexOf(suitOrder, b.Suit));
            if (suitCompare != 0) return suitCompare;
            
            // Then by rank descending
            return ((int)b.Rank).CompareTo((int)a.Rank);
        });
    }

    /// <summary>
    /// Finds the player with 2 of clubs to start
    /// </summary>
    private void FindStartingPlayer()
    {
        for (int i = 0; i < 4; i++)
        {
            if (Players[i].Hand.Any(c => c.Suit == Suit.Clubs && c.Rank == Rank.Two))
            {
                LeadPlayerIndex = i;
                CurrentPlayerIndex = i;
                return;
            }
        }
    }

    /// <summary>
    /// Selects cards to pass (for human player)
    /// </summary>
    public bool SelectCardToPass(int playerIndex, Card card)
    {
        if (Phase != HeartsPhase.Passing) return false;
        var player = Players[playerIndex];
        
        if (player.CardsToPass.Contains(card))
        {
            player.CardsToPass.Remove(card);
            return true;
        }
        
        if (player.CardsToPass.Count >= 3) return false;
        if (!player.Hand.Contains(card)) return false;
        
        player.CardsToPass.Add(card);
        return true;
    }

    /// <summary>
    /// AI selects cards to pass
    /// </summary>
    public void AISelectCardsToPass(int playerIndex)
    {
        var player = Players[playerIndex];
        if (!player.IsHuman && Phase == HeartsPhase.Passing)
        {
            player.CardsToPass.Clear();
            
            // Simple AI: pass highest cards, prioritizing Queen of Spades and high hearts
            var sortedHand = player.Hand
                .OrderByDescending(c => c.Suit == Suit.Spades && c.Rank == Rank.Queen ? 100 : 0)
                .ThenByDescending(c => c.Suit == Suit.Hearts ? 50 + (int)c.Rank : 0)
                .ThenByDescending(c => (int)c.Rank)
                .ToList();

            player.CardsToPass = sortedHand.Take(3).ToList();
        }
    }

    /// <summary>
    /// Executes the card passing
    /// </summary>
    public bool ExecutePass()
    {
        if (Phase != HeartsPhase.Passing) return false;
        if (Players.Any(p => p.CardsToPass.Count != 3)) return false;

        // Determine pass targets
        var passedCards = new List<Card>[4];
        for (int i = 0; i < 4; i++)
        {
            passedCards[i] = new List<Card>(Players[i].CardsToPass);
            
            // Remove cards from hand
            foreach (var card in passedCards[i])
            {
                Players[i].Hand.Remove(card);
            }
            Players[i].CardsToPass.Clear();
        }

        // Pass cards
        for (int i = 0; i < 4; i++)
        {
            int targetIndex = CurrentPassDirection switch
            {
                PassDirection.Left => (i + 1) % 4,
                PassDirection.Right => (i + 3) % 4,
                PassDirection.Across => (i + 2) % 4,
                _ => i
            };

            Players[targetIndex].Hand.AddRange(passedCards[i]);
        }

        // Sort hands again
        foreach (var player in Players)
        {
            SortHand(player.Hand);
        }

        Phase = HeartsPhase.Playing;
        FindStartingPlayer();
        return true;
    }

    /// <summary>
    /// Checks if a card can be legally played
    /// </summary>
    public bool CanPlayCard(int playerIndex, Card card)
    {
        if (Phase != HeartsPhase.Playing) return false;
        if (playerIndex != CurrentPlayerIndex) return false;
        
        var player = Players[playerIndex];
        if (!player.Hand.Contains(card)) return false;

        // First trick must lead 2 of clubs
        if (IsFirstTrick && CurrentTrick.Count == 0)
        {
            return card.Suit == Suit.Clubs && card.Rank == Rank.Two;
        }

        // Must follow suit if possible
        if (CurrentTrick.Count > 0)
        {
            var leadSuit = CurrentTrick[0].card.Suit;
            bool hasLeadSuit = player.Hand.Any(c => c.Suit == leadSuit);
            
            if (hasLeadSuit && card.Suit != leadSuit)
            {
                return false;
            }

            // Can't play hearts or Queen of Spades on first trick (if following suit)
            if (IsFirstTrick && !hasLeadSuit)
            {
                bool isPointCard = card.Suit == Suit.Hearts || 
                                   (card.Suit == Suit.Spades && card.Rank == Rank.Queen);
                
                // Can only play point cards if that's all we have
                if (isPointCard)
                {
                    bool onlyHasPointCards = player.Hand.All(c => 
                        c.Suit == Suit.Hearts || (c.Suit == Suit.Spades && c.Rank == Rank.Queen));
                    return onlyHasPointCards;
                }
            }
        }
        else
        {
            // Leading - can't lead hearts until broken (unless only hearts in hand)
            if (card.Suit == Suit.Hearts && !HeartsBroken)
            {
                return player.Hand.All(c => c.Suit == Suit.Hearts);
            }
        }

        return true;
    }

    /// <summary>
    /// Plays a card
    /// </summary>
    public bool PlayCard(int playerIndex, Card card)
    {
        if (!CanPlayCard(playerIndex, card)) return false;

        var player = Players[playerIndex];
        player.Hand.Remove(card);
        CurrentTrick.Add((playerIndex, card));

        // Check if hearts broken
        if (card.Suit == Suit.Hearts)
        {
            HeartsBroken = true;
        }

        // Move to next player or end trick
        if (CurrentTrick.Count == 4)
        {
            EndTrick();
        }
        else
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % 4;
        }

        return true;
    }

    /// <summary>
    /// AI plays a card
    /// </summary>
    public Card? AIPlayCard(int playerIndex)
    {
        var player = Players[playerIndex];
        if (player.IsHuman || playerIndex != CurrentPlayerIndex) return null;

        // Get legal cards
        var legalCards = player.Hand.Where(c => CanPlayCard(playerIndex, c)).ToList();
        if (legalCards.Count == 0) return null;

        Card selectedCard;

        if (CurrentTrick.Count == 0)
        {
            // Leading: play lowest non-heart if possible
            selectedCard = legalCards
                .Where(c => c.Suit != Suit.Hearts)
                .OrderBy(c => (int)c.Rank)
                .FirstOrDefault() ?? legalCards.First();
        }
        else
        {
            var leadSuit = CurrentTrick[0].card.Suit;
            var followingSuit = legalCards.Where(c => c.Suit == leadSuit).ToList();

            if (followingSuit.Count > 0)
            {
                // Try to play under if possible, otherwise dump high
                var currentHigh = CurrentTrick.Where(t => t.card.Suit == leadSuit).Max(t => (int)t.card.Rank);
                var safeCards = followingSuit.Where(c => (int)c.Rank < currentHigh).ToList();
                
                selectedCard = safeCards.Count > 0 
                    ? safeCards.OrderByDescending(c => (int)c.Rank).First()
                    : followingSuit.OrderByDescending(c => (int)c.Rank).First();
            }
            else
            {
                // Can't follow suit - dump Queen of Spades or high hearts
                var queenOfSpades = legalCards.FirstOrDefault(c => c.Suit == Suit.Spades && c.Rank == Rank.Queen);
                if (queenOfSpades != null)
                {
                    selectedCard = queenOfSpades;
                }
                else
                {
                    selectedCard = legalCards
                        .OrderByDescending(c => c.Suit == Suit.Hearts ? 100 + (int)c.Rank : (int)c.Rank)
                        .First();
                }
            }
        }

        PlayCard(playerIndex, selectedCard);
        return selectedCard;
    }

    /// <summary>
    /// Ends the current trick
    /// </summary>
    private void EndTrick()
    {
        // Find winner (highest card of lead suit)
        var leadSuit = CurrentTrick[0].card.Suit;
        var winnerPlay = CurrentTrick
            .Where(t => t.card.Suit == leadSuit)
            .OrderByDescending(t => (int)t.card.Rank)
            .First();

        int winnerIndex = winnerPlay.playerIndex;
        
        // Give cards to winner
        foreach (var (_, card) in CurrentTrick)
        {
            Players[winnerIndex].TricksTaken.Add(card);
        }

        CurrentTrick.Clear();
        IsFirstTrick = false;
        LeadPlayerIndex = winnerIndex;
        CurrentPlayerIndex = winnerIndex;

        // Check if round is over
        if (Players.All(p => p.Hand.Count == 0))
        {
            EndRound();
        }
    }

    /// <summary>
    /// Ends the current round and calculates scores
    /// </summary>
    private void EndRound()
    {
        // Calculate points
        foreach (var player in Players)
        {
            player.RoundScore = player.PointsTaken;
        }

        // Check for shooting the moon
        var moonShooter = Players.FirstOrDefault(p => p.RoundScore == 26);
        if (moonShooter != null)
        {
            // Moon shooter gets 0, everyone else gets 26
            foreach (var player in Players)
            {
                player.RoundScore = player == moonShooter ? 0 : 26;
            }
        }

        // Add to total scores
        foreach (var player in Players)
        {
            player.TotalScore += player.RoundScore;
        }

        // Check for game over
        if (Players.Any(p => p.TotalScore >= GameEndScore))
        {
            Phase = HeartsPhase.GameEnd;
        }
        else
        {
            Phase = HeartsPhase.RoundEnd;
        }
    }

    /// <summary>
    /// Gets the winner (lowest score)
    /// </summary>
    public HeartsPlayer? GetWinner()
    {
        if (Phase != HeartsPhase.GameEnd) return null;
        return Players.OrderBy(p => p.TotalScore).First();
    }

    /// <summary>
    /// Gets whose turn it is
    /// </summary>
    public HeartsPlayer CurrentPlayer => Players[CurrentPlayerIndex];

    /// <summary>
    /// Gets the trick winner (if trick is complete)
    /// </summary>
    public int? TrickWinnerIndex
    {
        get
        {
            if (CurrentTrick.Count != 4) return null;
            
            var leadSuit = CurrentTrick[0].card.Suit;
            return CurrentTrick
                .Where(t => t.card.Suit == leadSuit)
                .OrderByDescending(t => (int)t.card.Rank)
                .First().playerIndex;
        }
    }
}
