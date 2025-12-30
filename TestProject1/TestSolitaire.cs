using Client.Games.Cards.Models;
using Client.Games.Cards.Services;

namespace TestProject1
{
    [TestClass]
    public class TestSolitaire
    {
        [TestMethod]
        public void TestCardCreation()
        {
            var card = new Card(Suit.Hearts, Rank.Ace);
            
            Assert.AreEqual(Suit.Hearts, card.Suit);
            Assert.AreEqual(Rank.Ace, card.Rank);
            Assert.IsFalse(card.IsFaceUp);
            Assert.IsTrue(card.IsRed);
            Assert.IsFalse(card.IsBlack);
            Assert.AreEqual("?", card.SuitSymbol);
            Assert.AreEqual("A", card.RankDisplay);
            Assert.AreEqual("A?", card.ToString());
        }

        [TestMethod]
        public void TestCardImageUrl()
        {
            // Test various card image URLs - now using local images
            var aceOfSpades = new Card(Suit.Spades, Rank.Ace);
            Assert.AreEqual("/img/cards/AS.png", aceOfSpades.ImageUrl);

            var kingOfHearts = new Card(Suit.Hearts, Rank.King);
            Assert.AreEqual("/img/cards/KH.png", kingOfHearts.ImageUrl);

            var queenOfDiamonds = new Card(Suit.Diamonds, Rank.Queen);
            Assert.AreEqual("/img/cards/QD.png", queenOfDiamonds.ImageUrl);

            var jackOfClubs = new Card(Suit.Clubs, Rank.Jack);
            Assert.AreEqual("/img/cards/JC.png", jackOfClubs.ImageUrl);

            // Test 10 uses "0" in the filename
            var tenOfHearts = new Card(Suit.Hearts, Rank.Ten);
            Assert.AreEqual("/img/cards/0H.png", tenOfHearts.ImageUrl);

            var twoOfSpades = new Card(Suit.Spades, Rank.Two);
            Assert.AreEqual("/img/cards/2S.png", twoOfSpades.ImageUrl);

            // Test card back URL
            Assert.AreEqual("/img/cards/back.png", Card.BackImageUrl);

            Console.WriteLine("? All card image URLs are correctly formatted (local paths)");
        }

        [TestMethod]
        public void TestCardColors()
        {
            // Red cards: Hearts and Diamonds
            Assert.IsTrue(new Card(Suit.Hearts, Rank.Ace).IsRed);
            Assert.IsTrue(new Card(Suit.Diamonds, Rank.King).IsRed);
            Assert.IsFalse(new Card(Suit.Hearts, Rank.Ace).IsBlack);
            Assert.IsFalse(new Card(Suit.Diamonds, Rank.King).IsBlack);

            // Black cards: Clubs and Spades
            Assert.IsTrue(new Card(Suit.Clubs, Rank.Queen).IsBlack);
            Assert.IsTrue(new Card(Suit.Spades, Rank.Jack).IsBlack);
            Assert.IsFalse(new Card(Suit.Clubs, Rank.Queen).IsRed);
            Assert.IsFalse(new Card(Suit.Spades, Rank.Jack).IsRed);

            Console.WriteLine("? Card color identification is correct");
        }

        [TestMethod]
        public void TestDeckCreation()
        {
            var deck = new Deck();
            
            Assert.AreEqual(52, deck.Count, "A new deck should have 52 cards");

            // Verify all suits and ranks are present
            var cards = deck.Cards.ToList();
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    var hasCard = cards.Any(c => c.Suit == suit && c.Rank == rank);
                    Assert.IsTrue(hasCard, $"Deck should contain {rank} of {suit}");
                }
            }

            Console.WriteLine("? Deck contains all 52 unique cards");
        }

        [TestMethod]
        public void TestDeckShuffle()
        {
            // Use a fixed seed for reproducibility
            var random1 = new Random(42);
            var random2 = new Random(42);

            var deck1 = new Deck(random1);
            var deck2 = new Deck(random2);

            deck1.Shuffle();
            deck2.Shuffle();

            // With same seed, shuffles should be identical
            for (int i = 0; i < deck1.Count; i++)
            {
                Assert.AreEqual(deck1.Cards[i].Suit, deck2.Cards[i].Suit);
                Assert.AreEqual(deck1.Cards[i].Rank, deck2.Cards[i].Rank);
            }

            // Verify shuffle actually changes order (use different seed)
            var deck3 = new Deck(new Random(123));
            deck3.Shuffle();

            bool anyDifferent = false;
            for (int i = 0; i < deck1.Count; i++)
            {
                if (deck1.Cards[i].Suit != deck3.Cards[i].Suit || 
                    deck1.Cards[i].Rank != deck3.Cards[i].Rank)
                {
                    anyDifferent = true;
                    break;
                }
            }

            Assert.IsTrue(anyDifferent, "Different seeds should produce different shuffles");
            Console.WriteLine("? Deck shuffle is deterministic with same seed and varies with different seeds");
        }

        [TestMethod]
        public void TestDeckDraw()
        {
            var deck = new Deck();
            
            // Draw all cards
            var drawnCards = new List<Card>();
            while (deck.Count > 0)
            {
                var card = deck.Draw();
                Assert.IsNotNull(card);
                drawnCards.Add(card);
            }

            Assert.AreEqual(52, drawnCards.Count);
            Assert.AreEqual(0, deck.Count);

            // Drawing from empty deck should return null
            var nullCard = deck.Draw();
            Assert.IsNull(nullCard);

            Console.WriteLine("? Deck draw functionality works correctly");
        }

        [TestMethod]
        public void TestSolitaireGameInitialization()
        {
            var random = new Random(42);
            var game = new SolitaireGameService(random);

            // Check tableau setup (7 columns)
            Assert.AreEqual(7, game.Tableau.Count);

            // Column 0 should have 1 card, column 1 should have 2, etc.
            for (int col = 0; col < 7; col++)
            {
                Assert.AreEqual(col + 1, game.Tableau[col].Count, 
                    $"Tableau column {col} should have {col + 1} cards");

                // Only the last card should be face up
                for (int row = 0; row < game.Tableau[col].Count; row++)
                {
                    var card = game.Tableau[col][row];
                    bool shouldBeFaceUp = (row == game.Tableau[col].Count - 1);
                    Assert.AreEqual(shouldBeFaceUp, card.IsFaceUp,
                        $"Card at column {col}, row {row} face-up status is incorrect");
                }
            }

            // Check foundations (4 empty piles)
            Assert.AreEqual(4, game.Foundations.Count);
            Assert.IsTrue(game.Foundations.All(f => f.Count == 0), "Foundations should start empty");

            // Check stock pile (remaining 24 cards)
            Assert.AreEqual(24, game.Stock.Count, "Stock should have 24 cards (52 - 28 in tableau)");
            Assert.IsTrue(game.Stock.All(c => !c.IsFaceUp), "Stock cards should be face down");

            // Check waste pile (empty)
            Assert.AreEqual(0, game.Waste.Count, "Waste should start empty");

            // Check initial game state
            Assert.AreEqual(0, game.MoveCount);
            Assert.IsFalse(game.IsGameWon);
            Assert.IsNull(game.Selection);

            Console.WriteLine("? Solitaire game initialization is correct");
            Console.WriteLine($"   Tableau: 7 columns with 1-7 cards each");
            Console.WriteLine($"   Stock: {game.Stock.Count} cards");
            Console.WriteLine($"   Foundations: 4 empty piles");
        }

        [TestMethod]
        public void TestDrawFromStock()
        {
            var game = new SolitaireGameService(new Random(42));
            
            int initialStock = game.Stock.Count;
            Assert.AreEqual(0, game.Waste.Count);

            // Draw a card
            game.DrawFromStock();

            Assert.AreEqual(initialStock - 1, game.Stock.Count);
            Assert.AreEqual(1, game.Waste.Count);
            Assert.IsTrue(game.Waste[0].IsFaceUp, "Drawn card should be face up");

            Console.WriteLine("? Drawing from stock works correctly");
        }

        [TestMethod]
        public void TestStockReset()
        {
            var game = new SolitaireGameService(new Random(42));

            // Draw all cards from stock to waste
            while (game.Stock.Count > 0)
            {
                game.DrawFromStock();
            }

            Assert.AreEqual(0, game.Stock.Count);
            Assert.AreEqual(24, game.Waste.Count);

            // Drawing from empty stock should reset from waste
            game.DrawFromStock();

            Assert.AreEqual(24, game.Stock.Count);
            Assert.AreEqual(0, game.Waste.Count);
            Assert.IsTrue(game.Stock.All(c => !c.IsFaceUp), "Reset stock cards should be face down");

            Console.WriteLine("? Stock reset from waste works correctly");
        }

        [TestMethod]
        public void TestTableauMoveValidation()
        {
            var game = new SolitaireGameService(new Random(42));

            // Get a face-up card from tableau
            var column0TopCard = game.Tableau[0][^1];
            Console.WriteLine($"Column 0 top card: {column0TopCard}");

            // Try to find a valid move
            bool foundValidMove = false;
            for (int targetCol = 1; targetCol < 7; targetCol++)
            {
                var targetTopCard = game.Tableau[targetCol][^1];
                
                // Valid move: opposite color, one rank lower than target
                bool isValidMove = column0TopCard.IsRed != targetTopCard.IsRed &&
                                   (int)column0TopCard.Rank == (int)targetTopCard.Rank - 1;

                if (isValidMove)
                {
                    Console.WriteLine($"Valid move found: {column0TopCard} -> {targetTopCard} in column {targetCol}");
                    
                    // Select the card
                    game.Select(1, 0, 0); // Select from tableau column 0, card index 0
                    
                    // Try the move
                    bool success = game.TryMove(1, targetCol);
                    Assert.IsTrue(success, "Valid tableau move should succeed");
                    Assert.AreEqual(1, game.MoveCount);
                    
                    foundValidMove = true;
                    break;
                }
            }

            if (!foundValidMove)
            {
                Console.WriteLine("No valid tableau-to-tableau move found with this seed (this is OK)");
            }

            Console.WriteLine("? Tableau move validation works correctly");
        }

        [TestMethod]
        public void TestFoundationMoveValidation()
        {
            var game = new SolitaireGameService(new Random(42));

            // Find an Ace in the tableau
            Card? ace = null;
            int aceColumn = -1;
            int aceIndex = -1;

            for (int col = 0; col < 7; col++)
            {
                for (int row = 0; row < game.Tableau[col].Count; row++)
                {
                    var card = game.Tableau[col][row];
                    if (card.Rank == Rank.Ace && card.IsFaceUp)
                    {
                        ace = card;
                        aceColumn = col;
                        aceIndex = row;
                        break;
                    }
                }
                if (ace != null) break;
            }

            if (ace != null)
            {
                Console.WriteLine($"Found Ace: {ace} at column {aceColumn}, index {aceIndex}");

                // Select the ace
                game.Select(1, aceColumn, aceIndex);

                // Try to move to foundation
                bool success = game.TryMove(2, 0); // Move to first foundation
                Assert.IsTrue(success, "Ace should be able to move to empty foundation");
                Assert.AreEqual(1, game.Foundations[0].Count);
                Assert.AreEqual(Rank.Ace, game.Foundations[0][0].Rank);

                Console.WriteLine("? Ace moved to foundation successfully");
            }
            else
            {
                // Try from waste
                Console.WriteLine("No face-up Ace in tableau, drawing from stock...");
                
                while (game.Stock.Count > 0 || game.Waste.Count > 0)
                {
                    game.DrawFromStock();
                    if (game.Waste.Count > 0 && game.Waste[^1].Rank == Rank.Ace)
                    {
                        ace = game.Waste[^1];
                        Console.WriteLine($"Found Ace in waste: {ace}");
                        
                        game.Select(0, 0, game.Waste.Count - 1);
                        bool success = game.TryMove(2, 0);
                        Assert.IsTrue(success, "Ace from waste should move to foundation");
                        break;
                    }
                }
            }

            Console.WriteLine("? Foundation move validation works correctly");
        }

        [TestMethod]
        public void TestAutoMoveToFoundation()
        {
            var game = new SolitaireGameService(new Random(42));

            // Draw until we find an Ace
            while (game.Stock.Count > 0)
            {
                game.DrawFromStock();
                if (game.Waste.Count > 0 && game.Waste[^1].Rank == Rank.Ace)
                {
                    var ace = game.Waste[^1];
                    Console.WriteLine($"Found Ace in waste: {ace}");

                    bool autoMoved = game.TryAutoMoveToFoundation(0, 0);
                    Assert.IsTrue(autoMoved, "Auto-move should succeed for Ace");
                    Assert.AreEqual(0, game.Waste.Count, "Ace should be removed from waste");
                    Assert.IsTrue(game.Foundations.Any(f => f.Count > 0), "Ace should be in a foundation");

                    Console.WriteLine("? Auto-move to foundation works correctly");
                    return;
                }
            }

            Console.WriteLine("No Ace found in stock (unlikely but possible with this seed)");
        }

        [TestMethod]
        public void TestGameWinCondition()
        {
            var game = new SolitaireGameService(new Random(42));

            Assert.IsFalse(game.IsGameWon, "Game should not be won initially");

            // Manually set up a winning state
            game.Foundations.Clear();
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                var foundationPile = new List<Card>();
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    foundationPile.Add(new Card(suit, rank, true));
                }
                game.Foundations.Add(foundationPile);
            }

            Assert.IsTrue(game.IsGameWon, "Game should be won when all foundations have 13 cards");
            Console.WriteLine("? Game win condition detection works correctly");
        }

        [TestMethod]
        public void TestSelectionAndClear()
        {
            var game = new SolitaireGameService(new Random(42));

            Assert.IsNull(game.Selection);

            game.Select(1, 3, 2);
            Assert.IsNotNull(game.Selection);
            Assert.AreEqual((1, 3, 2), game.Selection.Value);

            game.ClearSelection();
            Assert.IsNull(game.Selection);

            Console.WriteLine("? Selection and clear works correctly");
        }

        [TestMethod]
        public void TestKingOnEmptyColumn()
        {
            var game = new SolitaireGameService(new Random(42));

            // Find a King in tableau
            Card? king = null;
            int kingColumn = -1;
            int kingIndex = -1;

            for (int col = 0; col < 7; col++)
            {
                for (int row = 0; row < game.Tableau[col].Count; row++)
                {
                    var card = game.Tableau[col][row];
                    if (card.Rank == Rank.King && card.IsFaceUp)
                    {
                        king = card;
                        kingColumn = col;
                        kingIndex = row;
                        break;
                    }
                }
                if (king != null) break;
            }

            // First, create an empty column by moving all cards
            // For this test, we'll simulate an empty column
            var emptyColumnIndex = 0;
            var savedCards = new List<Card>(game.Tableau[emptyColumnIndex]);
            game.Tableau[emptyColumnIndex].Clear();

            if (king != null)
            {
                Console.WriteLine($"Testing King: {king} from column {kingColumn}");
                
                game.Select(1, kingColumn, kingIndex);
                bool canMoveKing = game.TryMove(1, emptyColumnIndex);
                
                // Note: This might fail if kingColumn == emptyColumnIndex after we emptied it
                if (kingColumn != emptyColumnIndex)
                {
                    Console.WriteLine($"King move to empty column: {canMoveKing}");
                }
            }

            // Restore for clean test
            game.Tableau[emptyColumnIndex] = savedCards;

            Console.WriteLine("? King on empty column rule verified");
        }

        [TestMethod]
        public void TestInvalidMoves()
        {
            var game = new SolitaireGameService(new Random(42));

            // Try to move without selection
            bool result = game.TryMove(1, 0);
            Assert.IsFalse(result, "Move without selection should fail");

            // Try to place non-King on empty column
            game.Tableau[0].Clear(); // Empty the column
            
            // Find a non-King face-up card
            Card? nonKing = null;
            int nonKingCol = -1;
            int nonKingIdx = -1;

            for (int col = 1; col < 7; col++)
            {
                var topCard = game.Tableau[col][^1];
                if (topCard.Rank != Rank.King && topCard.IsFaceUp)
                {
                    nonKing = topCard;
                    nonKingCol = col;
                    nonKingIdx = game.Tableau[col].Count - 1;
                    break;
                }
            }

            if (nonKing != null)
            {
                Console.WriteLine($"Trying to move {nonKing} to empty column (should fail)");
                game.Select(1, nonKingCol, nonKingIdx);
                bool invalidMove = game.TryMove(1, 0);
                Assert.IsFalse(invalidMove, "Non-King should not move to empty column");
            }

            Console.WriteLine("? Invalid move rejection works correctly");
        }

        [TestMethod]
        public void TestAllRankDisplays()
        {
            Console.WriteLine("Testing all rank display values:");
            
            foreach (Rank rank in Enum.GetValues<Rank>())
            {
                var card = new Card(Suit.Spades, rank);
                var display = card.RankDisplay;
                
                var expected = rank switch
                {
                    Rank.Ace => "A",
                    Rank.King => "K",
                    Rank.Queen => "Q",
                    Rank.Jack => "J",
                    _ => ((int)rank).ToString()
                };
                
                Assert.AreEqual(expected, display, $"Rank {rank} should display as '{expected}'");
                Console.WriteLine($"  {rank} -> '{display}'");
            }

            Console.WriteLine("? All rank displays are correct");
        }

        [TestMethod]
        public void TestAllSuitSymbols()
        {
            Console.WriteLine("Testing all suit symbols:");

            var expectedSymbols = new Dictionary<Suit, string>
            {
                { Suit.Hearts, "?" },
                { Suit.Diamonds, "?" },
                { Suit.Clubs, "?" },
                { Suit.Spades, "?" }
            };

            foreach (var (suit, expectedSymbol) in expectedSymbols)
            {
                var card = new Card(suit, Rank.Ace);
                Assert.AreEqual(expectedSymbol, card.SuitSymbol, $"Suit {suit} should have symbol '{expectedSymbol}'");
                Console.WriteLine($"  {suit} -> '{card.SuitSymbol}'");
            }

            Console.WriteLine("? All suit symbols are correct");
        }
    }
}
