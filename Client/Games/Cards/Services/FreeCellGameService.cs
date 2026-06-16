using Client.Games.Cards.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    public List<string> MoveHistory { get; set; } = new(); // Each entry is a human-readable move string (e.g., "5♥:Col3>Col6")
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

    // Move history tracking
    private readonly List<string> _moveHistory = new();
    public IReadOnlyList<string> MoveHistory => _moveHistory;

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
        _moveHistory.Clear();

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
        }
        else if (gameId == 999999)
        {
            // Special case: Game #999999 is systematically unsolvable (all aces buried)
            InitializeBuriedAcesGame();
        }
        else
        {
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

        // Initialize incremental Zobrist hash for state tracking
        UseNumericHash = true;
        InitIncrementalHash();
    }
    // Matches a card token: rank (10 or single char A-K,2-9) followed by suit (Unicode symbol or letter)
    private static readonly Regex DumpCardPattern =
        new(@"(10|[AKQJ2-9])([♥♦♣♠HDCS])", RegexOptions.Compiled);

    /// <summary>
    /// Parses a single card token (e.g., "A♥", "10C", "KS") into a Card.
    /// Accepts Unicode suit symbols (♥♦♣♠) and letters (C, D, H, S).
    /// </summary>
    private static Card ParseCardToken(string token)
    {
        var match = DumpCardPattern.Match(token);
        if (!match.Success)
            throw new ArgumentException($"Invalid card token: '{token}'");

        Rank rank = match.Groups[1].Value switch
        {
            "A" => Rank.Ace,
            "K" => Rank.King,
            "Q" => Rank.Queen,
            "J" => Rank.Jack,
            "10" => Rank.Ten,
            var d => (Rank)int.Parse(d)
        };

        Suit suit = match.Groups[2].Value[0] switch
        {
            '♥' or 'H' => Suit.Hearts,
            '♦' or 'D' => Suit.Diamonds,
            '♣' or 'C' => Suit.Clubs,
            '♠' or 'S' => Suit.Spades,
            _ => throw new ArgumentException($"Invalid suit: {match.Groups[2].Value}")
        };

        return new Card(suit, rank, true);
    }

    /// <summary>
    /// Deserializes a FreeCellGameBase from the text format produced by dumpAllToLog() or ToDumpString().
    /// Accepts both Unicode suit symbols (♥♦♣♠) and letter abbreviations (C, D, H, S).
    /// Foundation piles are reconstructed from the top card (A through that rank).
    /// Optionally parses "Game #XXXX" and "MoveHistory:" lines when present.
    /// </summary>
    public static FreeCellGameService FromDumpString(string dump)
    {
        var game = new FreeCellGameService();
        game.GameId = -1;
        var lines = dump.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        // Find the header line containing FreeCells: and Foundations:
        var headerIdx = lines.FindIndex(l => l.Contains("FreeCells:"));
        if (headerIdx < 0)
            throw new ArgumentException("Invalid dump format: missing 'FreeCells:' header");

        // Parse optional "Game #XXXX" and "Moves: N" from lines before the header
        for (int i = 0; i < headerIdx; i++)
        {
            var gameMatch = Regex.Match(lines[i], @"Game\s*#(\d+)");
            if (gameMatch.Success)
                game.GameId = int.Parse(gameMatch.Groups[1].Value);
            var movesMatch = Regex.Match(lines[i], @"Moves:\s*(\d+)");
            if (movesMatch.Success)
                game.MoveCount = int.Parse(movesMatch.Groups[1].Value);
        }

        var headerLine = lines[headerIdx];
        var fcMarkerEnd = headerLine.IndexOf("FreeCells:") + "FreeCells:".Length;
        var fnMarkerStart = headerLine.IndexOf("Foundations:");
        if (fnMarkerStart < 0)
            throw new ArgumentException("Invalid dump format: missing 'Foundations:' header");
        var fnMarkerEnd = fnMarkerStart + "Foundations:".Length;
        var bvStart = headerLine.IndexOf("BValue:");

        // Parse FreeCells (cards between "FreeCells:" and "Foundations:")
        // Each slot is " {card}" = 4 chars (1-space prefix + 3-char ToString or 3 spaces for empty).
        // Use match.Index / 4 to determine the correct slot index (same as Foundations and Tableau).
        var fcSection = headerLine[fcMarkerEnd..fnMarkerStart];
        var fcMatches = DumpCardPattern.Matches(fcSection);
        game.FreeCells = [null, null, null, null];
        foreach (Match match in fcMatches)
        {
            int slotIndex = match.Index / 4;
            if (slotIndex >= 0 && slotIndex < 4)
            {
                game.FreeCells[slotIndex] = ParseCardToken(match.Value);
            }
        }

        // Parse Foundations top cards and reconstruct full piles (A through top rank)
        // Each foundation slot is exactly 4 chars: " {3-char card}" or " {3 spaces}".
        // Use match.Index / 4 to determine the correct slot index.
        var fnSection = bvStart >= 0 ? headerLine[fnMarkerEnd..bvStart] : headerLine[fnMarkerEnd..];
        var fnMatches = DumpCardPattern.Matches(fnSection);
        game.Foundations = [[], [], [], []];
        foreach (Match match in fnMatches)
        {
            int slotIndex = match.Index / 4;
            if (slotIndex >= 0 && slotIndex < 4)
            {
                var topCard = ParseCardToken(match.Value);
                for (int r = 1; r <= (int)topCard.Rank; r++)
                {
                    game.Foundations[slotIndex].Add(new Card(topCard.Suit, (Rank)r, true));
                }
            }
        }

        // Parse Tableau rows (each column is 4 chars wide: 3-char card + 1 space)
        // Stop at "MoveHistory:" line if present
        game.Tableau = Enumerable.Range(0, 8).Select(_ => new List<Card>()).ToList();
        for (int i = headerIdx + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.TrimStart().StartsWith("MoveHistory:")) break;

            foreach (Match match in DumpCardPattern.Matches(line))
            {
                int col = match.Index / 4;
                if (col >= 0 && col < 8)
                {
                    game.Tableau[col].Add(ParseCardToken(match.Value));
                }
            }
        }
        game.VerifyGame();

        // Parse optional MoveHistory section (header line + one move per subsequent line)
        var historyIdx = lines.FindIndex(l => l.TrimStart().StartsWith("MoveHistory:"));
        var moveHistoryLines = new List<string>();
        if (historyIdx >= 0)
        {
            for (int i = historyIdx + 1; i < lines.Count; i++)
            {
                var moveLine = lines[i].Trim();
                if (string.IsNullOrEmpty(moveLine)) continue;
                moveHistoryLines.Add(moveLine);
            }
        }

        // If we have a valid game ID and move history, re-deal and replay moves
        // so the undo stack is properly built (each TryMove calls OnBeforeMove).
        if (game.GameId > 0 && moveHistoryLines.Count > 0)
        {
            var importedDump = game.dumpAllToLog(""); // snapshot of expected final state
            game.InitializeGame(game.GameId);
            bool replayOk = true;
            foreach (var moveLine in moveHistoryLines)
            {
                try
                {
                    var move = ParseMoveHistoryEntry(moveLine);
                    if (!move.ApplyMove(game))
                    {
                        replayOk = false;
                        break;
                    }
                }
                catch
                {
                    replayOk = false;
                    break;
                }
            }
            if (!replayOk)
            {
                // Replay failed — fall back to the directly-imported state without undo support
                game = FromDumpStringDirect(dump);
            }
        }
        else
        {
            // No game ID or no move history — just populate _moveHistory without undo support
            game._moveHistory.AddRange(moveHistoryLines);
        }

        // Ensure hash is current for the no-replay and direct-import paths.
        // (The replay path already maintains the hash via TryMove.)
        if (!game.IncrementalHashReady)
        {
            game.UseNumericHash = true;
            game.InitIncrementalHash();
        }

        return game;
    }

    /// <summary>
    /// Direct import without move replay — used as fallback when replay fails.
    /// Produces a game with move history but no undo support.
    /// </summary>
    private static FreeCellGameService FromDumpStringDirect(string dump)
    {
        var game = new FreeCellGameService();
        game.GameId = -1;
        var lines = dump.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        var headerIdx = lines.FindIndex(l => l.Contains("FreeCells:"));
        if (headerIdx < 0)
            throw new ArgumentException("Invalid dump format: missing 'FreeCells:' header");

        for (int i = 0; i < headerIdx; i++)
        {
            var gameMatch = Regex.Match(lines[i], @"Game\s*#(\d+)");
            if (gameMatch.Success)
                game.GameId = int.Parse(gameMatch.Groups[1].Value);
            var movesMatch = Regex.Match(lines[i], @"Moves:\s*(\d+)");
            if (movesMatch.Success)
                game.MoveCount = int.Parse(movesMatch.Groups[1].Value);
        }

        var headerLine = lines[headerIdx];
        var fcMarkerEnd = headerLine.IndexOf("FreeCells:") + "FreeCells:".Length;
        var fnMarkerStart = headerLine.IndexOf("Foundations:");
        if (fnMarkerStart < 0)
            throw new ArgumentException("Invalid dump format: missing 'Foundations:' header");
        var fnMarkerEnd = fnMarkerStart + "Foundations:".Length;
        var bvStart = headerLine.IndexOf("BValue:");

        var fcSection = headerLine[fcMarkerEnd..fnMarkerStart];
        var fcMatches = DumpCardPattern.Matches(fcSection);
        game.FreeCells = [null, null, null, null];
        foreach (Match match in fcMatches)
        {
            int slotIndex = match.Index / 4;
            if (slotIndex >= 0 && slotIndex < 4)
                game.FreeCells[slotIndex] = ParseCardToken(match.Value);
        }

        var fnSection = bvStart >= 0 ? headerLine[fnMarkerEnd..bvStart] : headerLine[fnMarkerEnd..];
        var fnMatches = DumpCardPattern.Matches(fnSection);
        game.Foundations = [[], [], [], []];
        foreach (Match match in fnMatches)
        {
            int slotIndex = match.Index / 4;
            if (slotIndex >= 0 && slotIndex < 4)
            {
                var topCard = ParseCardToken(match.Value);
                for (int r = 1; r <= (int)topCard.Rank; r++)
                    game.Foundations[slotIndex].Add(new Card(topCard.Suit, (Rank)r, true));
            }
        }

        game.Tableau = Enumerable.Range(0, 8).Select(_ => new List<Card>()).ToList();
        for (int i = headerIdx + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.TrimStart().StartsWith("MoveHistory:")) break;
            foreach (Match match in DumpCardPattern.Matches(line))
            {
                int col = match.Index / 4;
                if (col >= 0 && col < 8)
                    game.Tableau[col].Add(ParseCardToken(match.Value));
            }
        }
        game.VerifyGame();

        var historyIdx = lines.FindIndex(l => l.TrimStart().StartsWith("MoveHistory:"));
        if (historyIdx >= 0)
        {
            for (int i = historyIdx + 1; i < lines.Count; i++)
            {
                var moveLine = lines[i].Trim();
                if (string.IsNullOrEmpty(moveLine)) continue;
                game._moveHistory.Add(moveLine);
            }
        }

        // Initialize incremental hash from imported state
        game.UseNumericHash = true;
        game.InitIncrementalHash();

        return game;
    }

    // Regex for parsing move history entries: card(rank + suit symbol or letter):Location idx > Location idx optional xCount
    // Accepts both Unicode suit symbols (♥♦♣♠) and ASCII letters (H, D, C, S)
    private static readonly Regex MoveHistoryPattern =
        new(@"((?:[AKQJ]|10|[2-9])[♥♦♣♠HDCS]):(Col|Free|Fnd)(\d)>(Col|Free|Fnd)(\d)(?:x(\d+))?", RegexOptions.Compiled);

    /// <summary>
    /// Exports the current game state as a human-readable dump string including game ID and optional move history.
    /// The output can be round-tripped through FromDumpString.
    /// </summary>
    public string ToDumpString(bool includeMoveHistory = true)
    {
        var dump = dumpAllToLog($"Game #{GameId} Moves: {MoveCount}");
        if (includeMoveHistory && _moveHistory.Count > 0)
        {
            dump += "MoveHistory:\r\n";
            foreach (var move in _moveHistory)
            {
                dump += $"  {move}\r\n";
            }
        }
        return dump;
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
    /// Initializes a provably unsolvable game #999999 with all 4 aces buried.
    /// All 8 columns are occupied — there are no empty columns to use as workspace.
    ///
    /// Columns 0–3: Ace at bottom, King at position 1, then a descending
    /// alternating-color sequence of Q–8 above (7 cards each).
    /// Columns 4–7: descending alternating-color sequences of 7–2 (6 cards each).
    ///
    /// Why it is unsolvable:
    /// To expose any Ace, the 6 cards above it (8, 9, 10, J, Q, K) must be cleared.
    /// With no empty columns, Kings can only go to a free cell.
    /// Queens can only stack on opposite-color Kings — all of which are buried at
    /// position [1] in their own columns and are inaccessible.
    /// After the first 4 cards (8, 9, 10, J) fill the 4 free cells, the Queen
    /// above each Ace has no free cell and no valid tableau destination.
    /// Therefore no Ace can ever be exposed and the game cannot be won.
    /// </summary>
    private void InitializeBuriedAcesGame()
    {
        // Column 1: A♥ buried at bottom, K♠ blocks it, then Q♥–8♥ alternating ♥/♠
        Tableau[0].AddRange(new[]
        {
            new Card(Suit.Hearts,   Rank.Ace,   true),   // A♥ (buried)
            new Card(Suit.Spades,   Rank.King,  true),   // K♠
            new Card(Suit.Hearts,   Rank.Queen, true),   // Q♥
            new Card(Suit.Spades,   Rank.Jack,  true),   // J♠
            new Card(Suit.Hearts,   Rank.Ten,   true),   // 10♥
            new Card(Suit.Spades,   Rank.Nine,  true),   // 9♠
            new Card(Suit.Hearts,   Rank.Eight, true)    // 8♥ (top)
        });

        // Column 2: A♦ buried at bottom, K♣ blocks it, then Q♦–8♦ alternating ♦/♣
        Tableau[1].AddRange(new[]
        {
            new Card(Suit.Diamonds, Rank.Ace,   true),   // A♦ (buried)
            new Card(Suit.Clubs,    Rank.King,  true),   // K♣
            new Card(Suit.Diamonds, Rank.Queen, true),   // Q♦
            new Card(Suit.Clubs,    Rank.Jack,  true),   // J♣
            new Card(Suit.Diamonds, Rank.Ten,   true),   // 10♦
            new Card(Suit.Clubs,    Rank.Nine,  true),   // 9♣
            new Card(Suit.Diamonds, Rank.Eight, true)    // 8♦ (top)
        });

        // Column 3: A♣ buried at bottom, K♥ blocks it, then Q♣–8♣ alternating ♣/♥
        Tableau[2].AddRange(new[]
        {
            new Card(Suit.Clubs,    Rank.Ace,   true),   // A♣ (buried)
            new Card(Suit.Hearts,   Rank.King,  true),   // K♥
            new Card(Suit.Clubs,    Rank.Queen, true),   // Q♣
            new Card(Suit.Hearts,   Rank.Jack,  true),   // J♥
            new Card(Suit.Clubs,    Rank.Ten,   true),   // 10♣
            new Card(Suit.Hearts,   Rank.Nine,  true),   // 9♥
            new Card(Suit.Clubs,    Rank.Eight, true)    // 8♣ (top)
        });

        // Column 4: A♠ buried at bottom, K♦ blocks it, then Q♠–8♠ alternating ♠/♦
        Tableau[3].AddRange(new[]
        {
            new Card(Suit.Spades,   Rank.Ace,   true),   // A♠ (buried)
            new Card(Suit.Diamonds, Rank.King,  true),   // K♦
            new Card(Suit.Spades,   Rank.Queen, true),   // Q♠
            new Card(Suit.Diamonds, Rank.Jack,  true),   // J♦
            new Card(Suit.Spades,   Rank.Ten,   true),   // 10♠
            new Card(Suit.Diamonds, Rank.Nine,  true),   // 9♦
            new Card(Suit.Spades,   Rank.Eight, true)    // 8♠ (top)
        });

        // Column 5: descending alternating ♠/♥ (7♠ at bottom, 2♥ on top)
        Tableau[4].AddRange(new[]
        {
            new Card(Suit.Spades,   Rank.Seven, true),   // 7♠
            new Card(Suit.Hearts,   Rank.Six,   true),   // 6♥
            new Card(Suit.Spades,   Rank.Five,  true),   // 5♠
            new Card(Suit.Hearts,   Rank.Four,  true),   // 4♥
            new Card(Suit.Spades,   Rank.Three, true),   // 3♠
            new Card(Suit.Hearts,   Rank.Two,   true)    // 2♥ (top)
        });

        // Column 6: descending alternating ♣/♦ (7♣ at bottom, 2♦ on top)
        Tableau[5].AddRange(new[]
        {
            new Card(Suit.Clubs,    Rank.Seven, true),   // 7♣
            new Card(Suit.Diamonds, Rank.Six,   true),   // 6♦
            new Card(Suit.Clubs,    Rank.Five,  true),   // 5♣
            new Card(Suit.Diamonds, Rank.Four,  true),   // 4♦
            new Card(Suit.Clubs,    Rank.Three, true),   // 3♣
            new Card(Suit.Diamonds, Rank.Two,   true)    // 2♦ (top)
        });

        // Column 7: descending alternating ♥/♠ (7♥ at bottom, 2♠ on top)
        Tableau[6].AddRange(new[]
        {
            new Card(Suit.Hearts,   Rank.Seven, true),   // 7♥
            new Card(Suit.Spades,   Rank.Six,   true),   // 6♠
            new Card(Suit.Hearts,   Rank.Five,  true),   // 5♥
            new Card(Suit.Spades,   Rank.Four,  true),   // 4♠
            new Card(Suit.Hearts,   Rank.Three, true),   // 3♥
            new Card(Suit.Spades,   Rank.Two,   true)    // 2♠ (top)
        });

        // Column 8: descending alternating ♦/♣ (7♦ at bottom, 2♣ on top)
        Tableau[7].AddRange(new[]
        {
            new Card(Suit.Diamonds, Rank.Seven, true),   // 7♦
            new Card(Suit.Clubs,    Rank.Six,   true),   // 6♣
            new Card(Suit.Diamonds, Rank.Five,  true),   // 5♦
            new Card(Suit.Clubs,    Rank.Four,  true),   // 4♣
            new Card(Suit.Diamonds, Rank.Three, true),   // 3♦
            new Card(Suit.Clubs,    Rank.Two,   true)    // 2♣ (top)
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
    /// Override to record each move in the move history for export.
    /// Format: {RankDisplay}{SuitSymbol}:{LocationLabel}{idx}>{LocationLabel}{idx} with optional x{count} for multi-card moves.
    /// Locations: Col=Tableau, Free=FreeCell, Fnd=Foundation.
    /// Example: 5♥:Col3>Col6, A♠:Col2>Fnd0, K♣:Free0>Col5, 5♥:Col3>Col6x3
    /// Parser also accepts ASCII suit letters: 5H:Col3>Col6
    /// </summary>
    protected override void OnMoveCompleted(FreeCellArea sourceType, int sourceIndex,
        FreeCellArea targetType, int targetIndex, List<Card> cardsToMove)
    {
        var card = cardsToMove[0];
        var cardStr = $"{card.RankDisplay}{card.SuitSymbol}";
        var src = $"{LocationLabel(sourceType)}{sourceIndex}";
        var tgt = $"{LocationLabel(targetType)}{targetIndex}";
        var count = cardsToMove.Count > 1 ? $"x{cardsToMove.Count}" : "";
        _moveHistory.Add($"{cardStr}:{src}>{tgt}{count}");
    }

    private static string LocationLabel(FreeCellArea type) => type switch
    {
        FreeCellArea.Tableau => "Col",
        FreeCellArea.FreeCell => "Free",
        FreeCellArea.Foundation => "Fnd",
        _ => "?"
    };

    /// <summary>
    /// Parses a location label string from move history back to a SourceType.
    /// </summary>
    private static FreeCellArea ParseLocationType(string label) => label switch
    {
        "Col" => FreeCellArea.Tableau,
        "Free" => FreeCellArea.FreeCell,
        "Fnd" => FreeCellArea.Foundation,
        _ => throw new ArgumentException($"Invalid location label: '{label}'")
    };

    /// <summary>
    /// Parses a single move history entry string into a FreeCellMove object.
    /// Format: {RankSuit}:{Location}{idx}>{Location}{idx}[x{count}]
    /// Examples: "5♥:Col3>Col6", "A♠:Col2>Fnd0", "5H:Col3>Col6x3"
    /// </summary>
    public static FreeCellMove ParseMoveHistoryEntry(string moveStr)
    {
        var match = MoveHistoryPattern.Match(moveStr);
        if (!match.Success)
            throw new ArgumentException($"Invalid move history entry: '{moveStr}'");

        var card = ParseCardToken(match.Groups[1].Value);
        var srcType = ParseLocationType(match.Groups[2].Value);
        var srcIndex = int.Parse(match.Groups[3].Value);
        var tgtType = ParseLocationType(match.Groups[4].Value);
        var tgtIndex = int.Parse(match.Groups[5].Value);
        var cardCount = match.Groups[6].Success ? int.Parse(match.Groups[6].Value) : 1;

        return new FreeCellMove(card)
        {
            sourceType = srcType,
            targetType = tgtType,
            sourceIndex = srcIndex,
            targetIndex = tgtIndex,
            cardCount = cardCount
        };
    }

    /// <summary>
    /// Parses a list of move history strings into FreeCellMove objects.
    /// </summary>
    public static List<FreeCellMove> ParseMoveHistory(IEnumerable<string> moveStrings)
    {
        return moveStrings.Select(ParseMoveHistoryEntry).ToList();
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
        // Recompute hash from the restored board state
        InitIncrementalHash();
    }

    /// <summary>
    /// Undoes the last move
    /// </summary>
    public bool Undo()
    {
        if (!CanUndo) return false;

        var snapshot = _undoStack.Pop();
        RestoreSnapshot(snapshot);
        if (_moveHistory.Count > 0)
            _moveHistory.RemoveAt(_moveHistory.Count - 1);
        return true;
    }

    #region Serialization

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
            Tableau = Tableau.Select(col => col.Select(c => c.ToSerializedString()).ToList()).ToList(),
            FreeCells = FreeCells.Select(c => c?.ToSerializedString()).ToList(),
            Foundations = Foundations.Select(f => f.Select(c => c.ToSerializedString()).ToList()).ToList(),
            UndoStack = _undoStack.Select(SerializeSnapshot).ToList(),
            MoveHistory = _moveHistory.ToList()
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
            Tableau = snapshot.Tableau.Select(col => col.Select(c => c.ToSerializedString()).ToList()).ToList(),
            FreeCells = snapshot.FreeCells.Select(c => c?.ToSerializedString()).ToList(),
            Foundations = snapshot.Foundations.Select(f => f.Select(c => c.ToSerializedString()).ToList()).ToList(),
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

        _moveHistory.Clear();
        if (state.MoveHistory != null)
            _moveHistory.AddRange(state.MoveHistory);

        // Initialize incremental hash from restored state
        UseNumericHash = true;
        InitIncrementalHash();
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
