using System;
using System.Linq;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    public class AiTests
    {
        static string Best(string fen, int depth = 3) => Ai.Best(Board.FromFen(fen), depth).ToString();

        [Test] public void FindsMateInOne_White() => Assert.AreEqual("a1a8", Best("6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1"));
        [Test] public void FindsMateInOne_Black() => Assert.AreEqual("a8a1", Best("r3k3/8/8/8/8/8/5PPP/6K1 b - - 0 1"));

        [Test] public void TakesAFreeQueen() => Assert.AreEqual("d1d5", Best("k7/8/8/3q4/8/8/8/K2R4 w - - 0 1"));

        [Test] public void PrefersTheQuickerMate()
        {
            // Mate in 1 exists; a slower mate must not be chosen even though both win.
            Assert.AreEqual("a1a8", Best("6k1/5ppp/8/8/8/8/1R6/R3K3 w - - 0 1"));
        }

        [Test] public void DoesNotLeaveTheQueenEnPrise()
        {
            var b = Board.FromFen("k7/8/8/8/3p4/4Q3/8/K7 w - - 0 1");
            var after = b.Apply(Ai.Best(b, 3).Value);
            Assert.IsTrue(MoveGen.Legal(after).All(m => after[Board.Name(m.To)] != 'Q'));
        }

        [Test] public void NoMoves_ReturnsNull()
        {
            Assert.IsNull(Ai.Best(Board.FromFen("rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3"), 3));
            Assert.IsNull(Ai.Best(Board.FromFen("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1"), 3));
        }

        // A draw is worth 0 to the AI: it dodges one when ahead and grabs one when behind.
        static Func<Board, bool> Flag(Board b, Move m) { var key = b.Apply(m).Key; return next => next.Key == key; }

        [Test] public void WhenAhead_AvoidsAMoveThatWouldDraw()
        {
            var b = Board.FromFen("k7/8/8/8/8/8/8/KQ6 w - - 0 1");
            var best = Ai.Best(b, 3).Value;
            var other = Ai.Best(b, 3, Flag(b, best)).Value;
            Assert.AreNotEqual(best.ToString(), other.ToString());
        }

        [Test] public void WhenBehind_TakesTheDraw()
        {
            var b = Board.FromFen("8/8/8/4k3/8/8/8/KR6 b - - 0 1");
            var best = Ai.Best(b, 3).Value;
            var drawing = MoveGen.Legal(b).First(m => m.ToString() != best.ToString());
            Assert.AreEqual(drawing.ToString(), Ai.Best(b, 3, Flag(b, drawing)).Value.ToString());
        }

        [Test] public void PlaysOnlyLegalMoves_ThroughAWholeGame()
        {
            var b = Board.Start();
            for (int ply = 0; ply < 40 && MoveGen.Status(b) == GameStatus.Ongoing; ply++)
            {
                var m = Ai.Best(b, 2).Value;
                Assert.IsTrue(MoveGen.Legal(b).Any(l => l.ToString() == m.ToString()), "illegal " + m + " at ply " + ply);
                b = b.Apply(m);
            }
        }

    }
}
