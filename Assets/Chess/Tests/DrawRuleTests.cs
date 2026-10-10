using System.Linq;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    public class DrawRuleTests
    {
        static Move Find(Board b, string uci) => MoveGen.Legal(b).Single(m => m.ToString() == uci);

        static void Play(Game g, params string[] moves)
        {
            foreach (var uci in moves) g.Play(Find(g.Board, uci));
        }

        // ---- halfmove clock ----
        [Test] public void FromFen_ReadsTheClock_AndDefaultsToZero()
        {
            Assert.AreEqual(37, Board.FromFen("8/8/8/8/8/8/8/K6k w - - 37 80").HalfmoveClock);
            Assert.AreEqual(0, Board.FromFen("8/8/8/8/8/8/8/K6k w - -").HalfmoveClock);
        }

        [Test] public void QuietPieceMove_AddsOne()
        {
            var b = Board.FromFen("8/8/8/8/8/8/8/K6k w - - 5 1");
            Assert.AreEqual(6, b.Apply(Find(b, "a1b1")).HalfmoveClock);
        }

        [Test] public void PawnMove_ResetsIt()
        {
            var b = Board.FromFen("8/8/8/8/8/8/P7/K6k w - - 40 1");
            Assert.AreEqual(0, b.Apply(Find(b, "a2a3")).HalfmoveClock);
        }

        [Test] public void Capture_ResetsIt()
        {
            var b = Board.FromFen("8/8/8/8/8/8/r7/R3K2k w - - 40 1");
            Assert.AreEqual(0, b.Apply(Find(b, "a1a2")).HalfmoveClock);
        }

        [Test] public void EnPassantCapture_ResetsIt()
        {
            var b = Board.FromFen("k6K/8/8/3pP3/8/8/8/8 w - d6 40 1");
            Assert.AreEqual(0, b.Apply(Find(b, "e5d6")).HalfmoveClock);
        }

        // ---- position key ----
        [Test] public void Key_IgnoresTheClock() =>
            Assert.AreEqual(Board.FromFen("8/8/8/8/8/8/8/K6k w - - 3 9").Key, Board.FromFen("8/8/8/8/8/8/8/K6k w - - 90 40").Key);

        [Test] public void Key_SeparatesSideToMoveAndCastlingRights()
        {
            var w = Board.FromFen("r3k3/8/8/8/8/8/8/R3K3 w Qq - 0 1");
            Assert.AreNotEqual(w.Key, Board.FromFen("r3k3/8/8/8/8/8/8/R3K3 b Qq - 0 1").Key);
            Assert.AreNotEqual(w.Key, Board.FromFen("r3k3/8/8/8/8/8/8/R3K3 w - - 0 1").Key);
        }

        [Test] public void Key_CountsEnPassantOnlyWhenACaptureIsPossible()
        {
            // After 1.e4 with no black pawn beside e4, the ep square is irrelevant...
            Assert.AreEqual(
                Board.FromFen("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1").Key,
                Board.FromFen("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1").Key);
            // ...but with a black pawn on d4 the capture is possible, so it is a different position.
            Assert.AreNotEqual(
                Board.FromFen("rnbqkbnr/ppp1pppp/8/8/3pP3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1").Key,
                Board.FromFen("rnbqkbnr/ppp1pppp/8/8/3pP3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1").Key);
        }

        // ---- game-level draws ----
        [Test] public void ThreefoldRepetition_IsADraw()
        {
            var g = new Game();
            var shuffle = new[] { "g1f3", "g8f6", "f3g1", "f6g8" };
            Play(g, shuffle);
            Assert.AreEqual(GameStatus.Ongoing, g.Status, "start position seen twice");
            Play(g, shuffle.Take(3).ToArray());
            Assert.AreEqual(GameStatus.Ongoing, g.Status);
            Play(g, "f6g8");
            Assert.AreEqual(GameStatus.DrawRepetition, g.Status, "start position seen three times");
        }

        [Test] public void Repetition_CountsAcrossDifferentCycles()
        {
            var g = new Game();
            Play(g, "g1f3", "g8f6", "f3g1", "f6g8");
            Assert.AreEqual(GameStatus.Ongoing, g.Status, "start position seen twice");
            Play(g, "b1c3", "b8c6", "c3b1", "c6b8");
            Assert.AreEqual(GameStatus.DrawRepetition, g.Status, "a second cycle with other knights makes the third visit");
        }

        [Test] public void FiftyMoveRule_DrawsAtHundredHalfmoves()
        {
            var g = new Game("8/8/8/8/8/8/8/K6k w - - 98 80");
            Play(g, "a1b1");
            Assert.AreEqual(GameStatus.Ongoing, g.Status);
            Play(g, "h1g1");
            Assert.AreEqual(GameStatus.DrawFiftyMove, g.Status);
        }

        [Test] public void PawnMove_AvoidsTheFiftyMoveDraw()
        {
            var g = new Game("8/8/8/8/8/8/P7/K6k w - - 99 80");
            Play(g, "a2a3");
            Assert.AreEqual(GameStatus.Ongoing, g.Status);
        }

        [Test] public void Checkmate_OutranksTheFiftyMoveRule() =>
            Assert.AreEqual(GameStatus.Checkmate, new Game("R5k1/5ppp/8/8/8/8/8/4K3 b - - 100 80").Status);

        [Test] public void Restart_ForgetsTheHistory()
        {
            var g = new Game();
            Play(g, "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1", "f6g8");
            Assert.AreEqual(GameStatus.DrawRepetition, g.Status);
            g.Restart();
            Assert.AreEqual(GameStatus.Ongoing, g.Status);
            Play(g, "g1f3", "g8f6", "f3g1", "f6g8");
            Assert.AreEqual(GameStatus.Ongoing, g.Status);
        }
    }
}
