using System.Linq;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    public class SpecialMoveTests
    {
        const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

        static string Legal(string fen, string from) => string.Join(" ",
            MoveGen.Legal(Board.FromFen(fen)).Where(m => m.From == Board.Sq(from)).Select(m => m.ToString().Substring(2)).OrderBy(s => s));

        static Move Find(Board b, string uci) => MoveGen.Legal(b).Single(m => m.ToString() == uci);

        static int Perft(Board b, int depth)
        {
            if (depth == 0) return 1;
            int n = 0;
            foreach (var m in MoveGen.Legal(b)) n += Perft(b.Apply(m), depth - 1);
            return n;
        }

        // ---- castling ----
        [Test] public void Castling_BothSides_WhenFree() =>
            Assert.AreEqual("c1 d1 d2 e2 f1 f2 g1", Legal("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1"));

        [Test] public void Castling_BlackBothSides() =>
            Assert.AreEqual("c8 d7 d8 e7 f7 f8 g8", Legal("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1", "e8"));

        [Test] public void Castling_NeedsTheRight() =>
            Assert.AreEqual("c1 d1 d2 e2 f1 f2", Legal("r3k2r/8/8/8/8/8/8/R3K2R w Qkq - 0 1", "e1"));

        [Test] public void Castling_BlockedByPiece() =>
            Assert.AreEqual("d1 d2 e2 f1 f2", Legal("r3k2r/8/8/8/8/8/8/RN2K1NR w KQkq - 0 1", "e1"));

        [Test] public void Castling_QueenSide_BlockedOnB1_StillBlocks() =>
            Assert.AreEqual("d1 d2 e2 f1 f2 g1", Legal("4k3/8/8/8/8/8/8/RN2K2R w KQ - 0 1", "e1"));

        [Test] public void Castling_NotThroughAttackedSquare() =>
            // black rook on f8 attacks f1: no kingside, queenside is fine
            Assert.AreEqual("c1 d1 d2 e2", Legal("5r1k/8/8/8/8/8/8/R3K2R w KQ - 0 1", "e1"));

        [Test] public void Castling_RookMayBeAttackedOnB1() =>
            Assert.IsTrue(Legal("1r5k/8/8/8/8/8/8/R3K3 w Q - 0 1", "e1").Contains("c1"));

        [Test] public void Castling_NotOutOfCheck() =>
            Assert.AreEqual("d1 d2 f1 f2", Legal("4r2k/8/8/8/8/8/8/R3K2R w KQ - 0 1", "e1"));

        [Test] public void Castling_MovesTheRook_AndDropsRights()
        {
            var a = Board.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            var k = a.Apply(Find(a, "e1g1"));
            Assert.AreEqual('K', k["g1"]); Assert.AreEqual('R', k["f1"]);
            Assert.AreEqual(Board.Empty, k["h1"]); Assert.AreEqual(Board.Empty, k["e1"]);
            Assert.AreEqual("kq", k.Castling);
            var q = a.Apply(Find(a, "e1c1"));
            Assert.AreEqual('K', q["c1"]); Assert.AreEqual('R', q["d1"]); Assert.AreEqual(Board.Empty, q["a1"]);
        }

        [Test] public void BlackCastling_MovesTheRook()
        {
            var a = Board.FromFen("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1");
            var k = a.Apply(Find(a, "e8g8"));
            Assert.AreEqual('k', k["g8"]); Assert.AreEqual('r', k["f8"]); Assert.AreEqual("KQ", k.Castling);
        }

        [Test] public void RookMove_DropsOnlyThatSide()
        {
            var a = Board.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Assert.AreEqual("Qkq", a.Apply(Find(a, "h1h2")).Castling);
        }

        [Test] public void CapturedRook_DropsOpponentRight()
        {
            var a = Board.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Assert.AreEqual("Kk", a.Apply(Find(a, "a1a8")).Castling);
        }

        // ---- en passant ----
        [Test] public void DoublePush_SetsEnPassantSquare_SinglePushClearsIt()
        {
            var a = Board.Start().Apply(Find(Board.Start(), "e2e4"));
            Assert.AreEqual(Board.Sq("e3"), a.EnPassant);
            Assert.AreEqual(-1, a.Apply(Find(a, "a7a6")).EnPassant);
        }

        [Test] public void EnPassant_IsOfferedAndRemovesTheCapturedPawn()
        {
            var b = Board.FromFen("8/8/8/3pP3/8/8/8/k6K w - d6 0 1");
            Assert.AreEqual("d6 e6", Legal("8/8/8/3pP3/8/8/8/k6K w - d6 0 1", "e5"));
            var after = b.Apply(Find(b, "e5d6"));
            Assert.AreEqual('P', after["d6"]); Assert.AreEqual(Board.Empty, after["d5"]);
        }

        [Test] public void EnPassant_BlackSide()
        {
            var b = Board.FromFen("k6K/8/8/8/3pP3/8/8/8 b - e3 0 1");
            var after = b.Apply(Find(b, "d4e3"));
            Assert.AreEqual('p', after["e3"]); Assert.AreEqual(Board.Empty, after["e4"]);
        }

        [Test] public void EnPassant_NotOfferedWithoutTheSquare() =>
            Assert.AreEqual("e6", Legal("8/8/8/3pP3/8/8/8/k6K w - - 0 1", "e5"));

        // ---- promotion ----
        [Test] public void Promotion_OffersFourPieces() =>
            Assert.AreEqual("a8b a8n a8q a8r", string.Join(" ",
                MoveGen.Legal(Board.FromFen("7k/P7/8/8/8/8/8/K7 w - - 0 1")).Where(m => m.From == Board.Sq("a7"))
                    .Select(m => m.ToString().Substring(2)).OrderBy(s => s)));

        [Test] public void Promotion_CaptureAlsoPromotes() =>
            Assert.AreEqual(8, MoveGen.Legal(Board.FromFen("1n5k/P7/8/8/8/8/8/K7 w - - 0 1")).Count(m => m.From == Board.Sq("a7")));

        [Test] public void Promotion_PlacesChosenPiece_InTheMoversColour()
        {
            var w = Board.FromFen("7k/P7/8/8/8/8/8/K7 w - - 0 1");
            Assert.AreEqual('N', w.Apply(Find(w, "a7a8n"))["a8"]);
            var bl = Board.FromFen("k7/8/8/8/8/8/p7/7K b - - 0 1");
            Assert.AreEqual('q', bl.Apply(Find(bl, "a2a1q"))["a1"]);
        }

        // ---- perft against published values ----
        [Test] public void Perft_Kiwipete() { var b = Board.FromFen(Kiwipete); Assert.AreEqual(48, Perft(b, 1)); Assert.AreEqual(2039, Perft(b, 2)); Assert.AreEqual(97862, Perft(b, 3)); }
        [Test] public void Perft_Position3() { var b = Board.FromFen("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1"); Assert.AreEqual(14, Perft(b, 1)); Assert.AreEqual(191, Perft(b, 2)); Assert.AreEqual(2812, Perft(b, 3)); Assert.AreEqual(43238, Perft(b, 4)); }
        [Test] public void Perft_Position4() { var b = Board.FromFen("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1"); Assert.AreEqual(6, Perft(b, 1)); Assert.AreEqual(264, Perft(b, 2)); Assert.AreEqual(9467, Perft(b, 3)); }
        [Test] public void Perft_Position5() { var b = Board.FromFen("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8"); Assert.AreEqual(44, Perft(b, 1)); Assert.AreEqual(1486, Perft(b, 2)); Assert.AreEqual(62379, Perft(b, 3)); }
    }
}
