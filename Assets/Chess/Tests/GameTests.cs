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

        [TestCase('q', 'Q')] [TestCase('r', 'R')] [TestCase('b', 'B')] [TestCase('n', 'N')]
        public void TryMove_Promotion_UsesTheChosenPiece(char choice, char expected)
        {
            var g = new Game("7k/P7/8/8/8/8/8/K7 w - - 0 1");
            Assert.IsTrue(g.TryMove(Sq("a7"), Sq("a8"), choice));
            Assert.AreEqual(expected, g.Board["a8"]);
        }

        [Test] public void TryMove_Promotion_RejectsAPieceThatIsNotAnOption()
        {
            var g = new Game("7k/P7/8/8/8/8/8/K7 w - - 0 1");
            Assert.IsFalse(g.TryMove(Sq("a7"), Sq("a8"), 'k'));
            Assert.AreEqual('P', g.Board["a7"]);
        }

        [Test] public void IsPromotion_OnlyForAPawnReachingTheLastRank()
        {
            var g = new Game("1n5k/P7/8/8/8/8/4P3/K7 w - - 0 1");
            Assert.IsTrue(g.IsPromotion(Sq("a7"), Sq("a8")));
            Assert.IsTrue(g.IsPromotion(Sq("a7"), Sq("b8")), "capturing promotion");
            Assert.IsFalse(g.IsPromotion(Sq("e2"), Sq("e4")));
            Assert.IsFalse(g.IsPromotion(Sq("a1"), Sq("a2")));
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

        static string Fen(Board b) => new string(b.Squares) + b.WhiteToMove + b.Castling + b.EnPassant;

        [Test] public void Undo_AfterTheAiReplied_TakesBackBothMoves()
        {
            var g = new Game();
            g.TryMove(Sq("e2"), Sq("e4"));
            g.AiMove(2);
            Assert.IsTrue(g.CanUndo);
            Assert.IsTrue(g.Undo());
            Assert.AreEqual(Fen(new Game().Board), Fen(g.Board));
            Assert.IsTrue(g.HumanTurn);
            Assert.IsFalse(g.CanUndo);
        }

        [Test] public void Undo_BeforeTheAiReplied_TakesBackOnlyTheHumanMove()
        {
            var g = new Game();
            g.TryMove(Sq("e2"), Sq("e4"));
            Assert.IsTrue(g.Undo());
            Assert.AreEqual('P', g.Board["e2"]);
            Assert.IsTrue(g.HumanTurn);
        }

        [Test] public void Undo_TwiceGoesBackTwoRounds()
        {
            var g = new Game();
            g.TryMove(Sq("e2"), Sq("e4")); g.AiMove(2);
            var afterFirstRound = Fen(g.Board);
            g.TryMove(Sq("d2"), Sq("d4")); g.AiMove(2);
            g.Undo();
            Assert.AreEqual(afterFirstRound, Fen(g.Board));
            g.Undo();
            Assert.AreEqual(Fen(new Game().Board), Fen(g.Board));
        }

        [Test] public void Undo_WithNothingToTakeBack_ReturnsFalse()
        {
            var g = new Game();
            Assert.IsFalse(g.CanUndo);
            Assert.IsFalse(g.Undo());
        }

        [Test] public void Undo_BringsBackAPromotedPawn()
        {
            var g = new Game("7k/P7/8/8/8/8/8/K7 w - - 0 1");
            g.TryMove(Sq("a7"), Sq("a8"), 'q');
            g.Undo();
            Assert.AreEqual('P', g.Board["a7"]);
            Assert.AreEqual(Board.Empty, g.Board["a8"]);
        }

        [Test] public void Undo_ForgetsTheTakenBackPositions_ForRepetition()
        {
            var g = new Game();
            foreach (var uci in new[] { "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1", "f6g8" })
                g.Play(MoveGen.Legal(g.Board).Single(m => m.ToString() == uci));
            Assert.AreEqual(GameStatus.DrawRepetition, g.Status);
            g.Undo();
            Assert.AreEqual(GameStatus.Ongoing, g.Status);
            // Replaying the same move must not count the positions that were taken back.
            g.Play(MoveGen.Legal(g.Board).Single(m => m.ToString() == "f3g1"));
            Assert.AreEqual(GameStatus.Ongoing, g.Status);
        }

        static void PlayAll(Game g, params string[] ucis)
        {
            foreach (var uci in ucis) g.Play(MoveGen.Legal(g.Board).Single(m => m.ToString() == uci));
        }

        [Test] public void LastMove_IsNullAtStart_ThenTheMoveJustPlayed_ForEitherSide()
        {
            var g = new Game();
            Assert.IsNull(g.LastMove);
            PlayAll(g, "e2e4");
            Assert.AreEqual("e2e4", g.LastMove.ToString());
            PlayAll(g, "e7e5");
            Assert.AreEqual("e7e5", g.LastMove.ToString());
        }

        [Test] public void Undo_RestoresThePreviousLastMove()
        {
            var g = new Game();
            PlayAll(g, "e2e4", "e7e5", "g1f3", "b8c6");
            g.Undo(); // back to the position before Nf3
            Assert.AreEqual("e7e5", g.LastMove.ToString());
            g.Undo();
            Assert.IsNull(g.LastMove);
        }

        [Test] public void MoveList_NumbersTheMoves_AndKeepsOnlyTheLastLines()
        {
            var g = new Game();
            PlayAll(g, "e2e4", "e7e5", "g1f3");
            Assert.AreEqual("1. e4 e5\n2. Nf3", g.MoveListText());
            PlayAll(g, "b8c6", "f1b5", "a7a6");
            Assert.AreEqual("2. Nf3 Nc6\n3. Bb5 a6", g.MoveListText(2));
        }

        [Test] public void MoveList_FollowsUndoAndRestart()
        {
            var g = new Game();
            PlayAll(g, "e2e4", "e7e5", "g1f3", "b8c6");
            g.Undo();
            Assert.AreEqual("1. e4 e5", g.MoveListText());
            g.Restart();
            Assert.AreEqual("", g.MoveListText());
        }

        [Test] public void Restart_ClearsTheUndoHistory()
        {
            var g = new Game();
            g.TryMove(Sq("e2"), Sq("e4"));
            g.Restart();
            Assert.IsFalse(g.CanUndo);
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
