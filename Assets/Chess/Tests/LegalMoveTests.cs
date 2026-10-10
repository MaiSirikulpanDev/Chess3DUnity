using System.Linq;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    public class LegalMoveTests
    {
        static string Legal(string fen, string from) => string.Join(" ",
            MoveGen.Legal(Board.FromFen(fen)).Where(m => m.From == Board.Sq(from)).Select(m => Board.Name(m.To)).OrderBy(s => s));

        static int Perft(Board b, int depth)
        {
            if (depth == 0) return 1;
            int n = 0;
            foreach (var m in MoveGen.Legal(b)) n += Perft(b.Apply(m), depth - 1);
            return n;
        }

        [Test]
        public void Apply_MovesPiece_FlipsSide_LeavesOriginalUntouched()
        {
            var b = Board.Start();
            var after = b.Apply(new Move(Board.Sq("e2"), Board.Sq("e4")));
            Assert.AreEqual('P', after["e4"]);
            Assert.AreEqual(Board.Empty, after["e2"]);
            Assert.IsFalse(after.WhiteToMove);
            Assert.AreEqual('P', b["e2"]);
            Assert.IsTrue(b.WhiteToMove);
        }

        [Test]
        public void Apply_Capture_ReplacesTarget()
        {
            var after = Board.FromFen("8/8/8/3p4/4P3/8/8/8 w - - 0 1").Apply(new Move(Board.Sq("e4"), Board.Sq("d5")));
            Assert.AreEqual('P', after["d5"]);
        }

        [TestCase("8/8/8/8/4k3/8/4R3/4K3 b - - 0 1", true)]   // rook gives check up the file
        [TestCase("8/8/8/8/8/2n5/8/4K3 w - - 0 1", false)]    // knight on c3 does not attack e1
        [TestCase("8/8/8/8/8/3n4/8/4K3 w - - 0 1", true)]     // knight d3 attacks e1
        [TestCase("8/8/8/8/8/8/3p4/4K3 w - - 0 1", true)]     // black pawn d2 attacks e1
        [TestCase("8/8/8/8/8/8/4p3/4K3 w - - 0 1", false)]    // pawn directly ahead does not attack
        [TestCase("8/8/8/8/8/8/8/b3K3 w - - 0 1", false)]     // bishop a1 on same rank: not a diagonal
        [TestCase("8/8/8/8/7b/8/8/4K3 w - - 0 1", true)]      // bishop h4 sees e1 along the diagonal
        [TestCase("8/8/8/8/8/8/8/r3K3 w - - 0 1", true)]      // rook on rank
        [TestCase("8/8/8/8/8/8/8/rP2K3 w - - 0 1", false)]    // blocked by pawn
        public void InCheck_Detects(string fen, bool expected)
        {
            var b = Board.FromFen(fen);
            Assert.AreEqual(expected, MoveGen.InCheck(b, b.WhiteToMove));
        }

        [Test]
        public void KingCannotStepIntoAttack()
        {
            // Black rook on a2 covers the 2nd rank; king e1 may not go to d2/e2/f2.
            Assert.AreEqual("d1 f1", Legal("8/8/8/8/8/8/r7/4K3 w - - 0 1", "e1"));
        }

        [Test]
        public void PinnedPiece_CannotLeaveTheLine()
        {
            // White rook e2 pinned to king e1 by black rook e8: may only move along the e-file.
            Assert.AreEqual("e3 e4 e5 e6 e7 e8", Legal("4r3/8/8/8/8/8/4R3/4K3 w - - 0 1", "e2"));
        }

        [Test]
        public void InCheck_OnlyResponsesThatResolveItAreLegal()
        {
            // Rook e8 checks king e1; white bishop c3 can only interpose on e5, white knight cannot help.
            var moves = MoveGen.Legal(Board.FromFen("4r3/8/8/8/8/2B5/8/4K3 w - - 0 1")).Select(m => m.ToString()).OrderBy(s => s).ToList();
            CollectionAssert.AreEqual(new[] { "c3e5", "e1d1", "e1d2", "e1f1", "e1f2" }, moves);
        }

        [Test]
        public void DoubleCheck_OnlyKingMoves()
        {
            // Knight d3 and rook e8 both check king e1.
            var moves = MoveGen.Legal(Board.FromFen("4r3/8/8/8/8/3n4/6B1/4K3 w - - 0 1"));
            Assert.IsTrue(moves.All(m => m.From == Board.Sq("e1")));
        }

        [Test]
        public void Checkmate_HasNoLegalMoves_FoolsMate()
        {
            var b = Board.FromFen("rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3");
            Assert.IsTrue(MoveGen.InCheck(b, true));
            Assert.AreEqual(0, MoveGen.Legal(b).Count);
        }

        [Test] public void Perft_Start_Depth1() => Assert.AreEqual(20, Perft(Board.Start(), 1));
        [Test] public void Perft_Start_Depth2() => Assert.AreEqual(400, Perft(Board.Start(), 2));
        [Test] public void Perft_Start_Depth3() => Assert.AreEqual(8902, Perft(Board.Start(), 3));
        [Test] public void Perft_Start_Depth4() => Assert.AreEqual(197281, Perft(Board.Start(), 4));
    }
}
