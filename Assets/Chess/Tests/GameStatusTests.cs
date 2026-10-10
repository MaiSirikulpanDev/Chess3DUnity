using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    public class GameStatusTests
    {
        [TestCase("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", GameStatus.Ongoing)]
        [TestCase("rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3", GameStatus.Checkmate)]   // Fool's mate
        [TestCase("r1bqkb1r/pppp1Qpp/2n2n2/4p3/2B1P3/8/PPPP1PPP/RNB1K1NR b KQkq - 0 4", GameStatus.Checkmate) ] // Scholar's mate
        [TestCase("6k1/5ppp/8/8/8/8/8/R3K3 b - - 0 1", GameStatus.Ongoing)]                                   // not mate: back rank not yet hit
        [TestCase("R5k1/5ppp/8/8/8/8/8/4K3 b - - 0 1", GameStatus.Checkmate)]                                 // back-rank mate
        [TestCase("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1", GameStatus.Stalemate)]                                    // king boxed in, not in check
        [TestCase("k7/2Q5/1K6/8/8/8/8/8 b - - 0 1", GameStatus.Stalemate)]
        [TestCase("4k3/8/8/8/8/8/8/R3K3 b - - 0 1", GameStatus.Ongoing)]
        [TestCase("4r3/8/8/8/8/8/8/4K3 w - - 0 1", GameStatus.Ongoing)]                                       // in check but can step aside
        public void Status(string fen, GameStatus expected) =>
            Assert.AreEqual(expected, MoveGen.Status(Board.FromFen(fen)));

        [Test]
        public void InCheckWithAnEscape_IsOngoing_NotMate()
        {
            var b = Board.FromFen("4r2k/8/8/8/8/8/8/4K3 w - - 0 1");
            Assert.IsTrue(MoveGen.InCheck(b, true));
            Assert.AreEqual(GameStatus.Ongoing, MoveGen.Status(b));
        }
    }
}
