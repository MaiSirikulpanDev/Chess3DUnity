using System.Linq;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    public class MoveGenTests
    {
        // Target squares reachable from one square, sorted, space separated.
        static string Targets(string fen, string from) => string.Join(" ",
            MoveGen.PseudoLegal(Board.FromFen(fen))
                .Where(m => m.From == Board.Sq(from))
                .Select(m => Board.Name(m.To))
                .OrderBy(s => s));

        [Test]
        public void Move_ToString_IsFromThenTo()
        {
            Assert.AreEqual("e2e4", new Move(Board.Sq("e2"), Board.Sq("e4")).ToString());
        }

        [Test]
        public void StartPosition_Has20Moves()
        {
            Assert.AreEqual(20, MoveGen.PseudoLegal(Board.Start()).Count);
        }

        [Test]
        public void OnlySideToMoveGetsMoves()
        {
            var moves = MoveGen.PseudoLegal(Board.FromFen("8/8/8/3n4/8/8/8/R7 b - - 0 1"));
            Assert.IsTrue(moves.All(m => m.From == Board.Sq("d5")));
            Assert.AreEqual(8, moves.Count);
        }

        [Test]
        public void Knight_InCorner_HasTwoMoves()
        {
            Assert.AreEqual("b3 c2", Targets("8/8/8/8/8/8/8/N7 w - - 0 1", "a1"));
        }

        [Test]
        public void Knight_JumpsOverPieces_ButNotOntoOwn()
        {
            // Knight on b1 in the start position: d2 is own pawn, a3 and c3 are free.
            Assert.AreEqual("a3 c3", Targets(Board.StartFen, "b1"));
        }

        [Test]
        public void Rook_OnEmptyBoard_Has14Moves()
        {
            Assert.AreEqual(14, Targets("8/8/8/8/3R4/8/8/8 w - - 0 1", "d4").Split(' ').Length);
        }

        [Test]
        public void Rook_StopsBeforeOwnPiece_AndCapturesEnemyThenStops()
        {
            // Rook a1, own pawn a3, enemy pawn c1 with another enemy behind it on e1.
            Assert.AreEqual("a2 b1 c1", Targets("8/8/8/8/8/P7/8/R1p1p3 w - - 0 1", "a1"));
        }

        [Test]
        public void Bishop_MovesDiagonally_AndDoesNotWrapAroundTheEdge()
        {
            Assert.AreEqual("a3 b2 d2 e3 f4 g5 h6", Targets("8/8/8/8/8/8/8/2B5 w - - 0 1", "c1"));
        }

        [Test]
        public void Queen_OnEmptyBoard_Has27Moves()
        {
            Assert.AreEqual(27, Targets("8/8/8/8/3Q4/8/8/8 w - - 0 1", "d4").Split(' ').Length);
        }

        [Test]
        public void King_MovesOneSquareInAnyDirection()
        {
            Assert.AreEqual("c3 c4 c5 d3 d5 e3 e4 e5", Targets("8/8/8/8/3K4/8/8/8 w - - 0 1", "d4"));
        }

        [Test]
        public void King_OnEdge_DoesNotWrap()
        {
            Assert.AreEqual("g1 g2 h2", Targets("8/8/8/8/8/8/8/7K w - - 0 1", "h1"));
        }

        [Test]
        public void WhitePawn_OnStartRank_CanMoveOneOrTwo()
        {
            Assert.AreEqual("e3 e4", Targets(Board.StartFen, "e2"));
        }

        [Test]
        public void WhitePawn_OffStartRank_MovesOneOnly()
        {
            Assert.AreEqual("e4", Targets("8/8/8/8/8/4P3/8/8 w - - 0 1", "e3"));
        }

        [Test]
        public void Pawn_BlockedDirectlyAhead_CannotMove()
        {
            Assert.AreEqual("", Targets("8/8/8/8/8/4p3/4P3/8 w - - 0 1", "e2"));
        }

        [Test]
        public void Pawn_DoublePush_BlockedOnSecondSquare_MovesOneOnly()
        {
            Assert.AreEqual("e3", Targets("8/8/8/8/4p3/8/4P3/8 w - - 0 1", "e2"));
        }

        [Test]
        public void Pawn_CapturesDiagonally_OnlyEnemies_NotStraightAhead()
        {
            // White pawn e4; enemy on d5, own piece on f5, enemy directly ahead on e5.
            Assert.AreEqual("d5", Targets("8/8/8/3ppN2/4P3/8/8/8 w - - 0 1", "e4"));
        }

        [Test]
        public void Pawn_OnAFile_DoesNotCaptureAcrossTheEdge()
        {
            // Black pawn on h5 must not be capturable from a4 (wrap-around).
            Assert.AreEqual("a5", Targets("8/8/8/7p/P7/8/8/8 w - - 0 1", "a4"));
        }

        [Test]
        public void BlackPawn_MovesDownTheBoard()
        {
            Assert.AreEqual("e5 e6", Targets("8/4p3/8/8/8/8/8/8 b - - 0 1", "e7"));
            Assert.AreEqual("d4", Targets("8/8/8/4p3/3PP3/8/8/8 b - - 0 1", "e5"));
        }
    }
}
