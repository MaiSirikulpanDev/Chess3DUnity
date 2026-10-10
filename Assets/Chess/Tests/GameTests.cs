using System.Linq;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    public class GameTests
    {
        static int Sq(string s) => Board.Sq(s);
        static string Targets(Game g, string from) => string.Join(" ", g.TargetsFrom(Sq(from)).Select(Board.Name).OrderBy(s => s));

        [Test] public void NewGame_HumanPlaysWhite_AndIsOngoing()
        {
            var g = new Game();
            Assert.IsTrue(g.HumanTurn);
            Assert.AreEqual(GameStatus.Ongoing, g.Status);
        }

        [Test] public void TargetsFrom_OwnPiece_ListsLegalDestinations() => Assert.AreEqual("e3 e4", Targets(new Game(), "e2"));

        [Test] public void TargetsFrom_EnemyOrEmptySquare_IsEmpty()
        {
            var g = new Game();
            Assert.AreEqual("", Targets(g, "e7"));
            Assert.AreEqual("", Targets(g, "e4"));
        }

        [Test] public void TryMove_LegalMove_UpdatesBoardAndPassesTurn()
        {
            var g = new Game();
            Assert.IsTrue(g.TryMove(Sq("e2"), Sq("e4")));
            Assert.AreEqual('P', g.Board["e4"]);
            Assert.IsFalse(g.HumanTurn);
        }

        [Test] public void TryMove_IllegalMove_ChangesNothing()
        {
            var g = new Game();
            Assert.IsFalse(g.TryMove(Sq("e2"), Sq("e5")));
            Assert.IsFalse(g.TryMove(Sq("e7"), Sq("e5")));
            Assert.AreEqual('P', g.Board["e2"]);
            Assert.IsTrue(g.HumanTurn);
        }

        [Test] public void TryMove_DuringAiTurn_IsRejected()
        {
            var g = new Game();
            g.TryMove(Sq("e2"), Sq("e4"));
            Assert.IsFalse(g.TryMove(Sq("d2"), Sq("d4")));
        }

        [Test] public void TryMove_PawnReachingLastRank_PromotesToQueen()
        {
            var g = new Game("7k/P7/8/8/8/8/8/K7 w - - 0 1");
            Assert.IsTrue(g.TryMove(Sq("a7"), Sq("a8")));
            Assert.AreEqual('Q', g.Board["a8"]);
        }

        [Test] public void AiMove_OnlyWhenItIsTheAisTurn()
        {
            var g = new Game();
            Assert.IsFalse(g.AiMove(2));
            g.TryMove(Sq("e2"), Sq("e4"));
            Assert.IsTrue(g.AiMove(2));
            Assert.IsTrue(g.HumanTurn);
        }

        [Test] public void AiMove_WhenCheckmated_ReturnsFalse()
        {
            var g = new Game("R5k1/5ppp/8/8/8/8/8/4K3 b - - 0 1");
            Assert.AreEqual(GameStatus.Checkmate, g.Status);
            Assert.IsFalse(g.AiMove(2));
        }

        [Test] public void AiAhead_DoesNotWalkIntoTheFiftyMoveDraw()
        {
            // Clock 99: the AI would naturally play a quiet queen move (a2a8) and draw; it must reset the clock instead.
            var g = new Game("7k/6p1/8/8/8/8/q7/6RK b - - 99 80");
            Assert.IsTrue(g.AiMove(3));
            Assert.AreEqual(GameStatus.Ongoing, g.Status);
        }

        [Test] public void AiAhead_DoesNotCaptureIntoAnInsufficientMaterialDraw()
        {
            // Bxh2 would win the last pawn but leave K+B v K, a dead draw; the AI is ahead and must decline.
            var g = new Game("7k/2b5/8/8/8/8/7P/K7 b - - 0 1");
            Assert.IsTrue(g.AiMove(3));
            Assert.AreEqual(GameStatus.Ongoing, g.Status);
        }

        [Test] public void AiTakesTheMate_EvenOnTheHundredthHalfmove()
        {
            var g = new Game("r3k3/8/8/8/8/8/5PPP/6K1 b - - 99 80");
            Assert.IsTrue(g.AiMove(3));
            Assert.AreEqual(GameStatus.Checkmate, g.Status);
        }

        [Test] public void Restart_ReturnsToTheStartingPosition()
        {
            var g = new Game();
            g.TryMove(Sq("e2"), Sq("e4"));
            g.Restart();
            Assert.AreEqual('P', g.Board["e2"]);
            Assert.IsTrue(g.HumanTurn);
        }
    }
}
