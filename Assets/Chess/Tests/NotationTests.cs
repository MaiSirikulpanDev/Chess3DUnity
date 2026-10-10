using System.Linq;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    public class NotationTests
    {
        static string San(string fen, string uci)
        {
            var b = Board.FromFen(fen);
            return Notation.San(b, MoveGen.Legal(b).Single(m => m.ToString() == uci));
        }

        [TestCase(Board.StartFen, "e2e4", "e4")]
        [TestCase(Board.StartFen, "g1f3", "Nf3")]
        [TestCase("rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2", "e4d5", "exd5")]          // pawn capture
        [TestCase("rnbqkbnr/pppppppp/8/8/8/5N2/PPPPPPPP/RNBQKB1R b KQkq - 1 1", "g8f6", "Nf6")]
        [TestCase("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1", "e1g1", "O-O")]
        [TestCase("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1", "e1c1", "O-O-O")]
        [TestCase("r3k2r/8/8/8/8/8/8/4K3 b kq - 0 1", "e8g8", "O-O")]
        [TestCase("k7/8/8/3pP3/8/8/8/K7 w - d6 0 1", "e5d6", "exd6")]                                          // en passant
        [TestCase("7k/4P3/8/8/8/8/8/K7 w - - 0 1", "e7e8q", "e8=Q+")]                                          // promotion that gives check
        [TestCase("7k/4P3/8/8/8/8/8/K7 w - - 0 1", "e7e8n", "e8=N")]
        [TestCase("3rr2k/4P3/8/8/8/8/8/K7 w - - 0 1", "e7d8q", "exd8=Q")]                                     // capture and promote
        [TestCase("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", "a1a8", "Ra8+")]                                          // check
        [TestCase("rnbqkbnr/pppp1ppp/8/4p3/6P1/5P2/PPPPP2P/RNBQKBNR b KQkq - 0 2", "d8h4", "Qh4#")]           // Fool's mate
        [TestCase("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", "e1d1", "Kd1")]
        public void San_Examples(string fen, string uci, string expected) => Assert.AreEqual(expected, San(fen, uci));

        // ---- when two pieces of a kind can reach the square ----
        [Test] public void DifferentFiles_UseTheFile() => Assert.AreEqual("Nbd2", San("4k3/8/8/8/8/5N2/8/1N2K3 w - - 0 1", "b1d2"));

        [Test] public void SameFile_UseTheRank() => Assert.AreEqual("R1a3", San("4k3/8/8/R7/8/8/8/R3K3 w - - 0 1", "a1a3"));

        [Test] public void SameRank_UseTheFile() => Assert.AreEqual("Rad2", San("4k3/8/8/8/8/8/R6R/4K3 w - - 0 1", "a2d2"));

        [Test] public void NeitherAloneWorks_UseBoth() => Assert.AreEqual("Qa1c3", San("4k3/8/8/8/8/Q7/8/Q1Q1K3 w - - 0 1", "a1c3"));

        [Test] public void APinnedTwinDoesNotCount() => Assert.AreEqual("Nd2", San("5r1k/8/8/8/8/5N2/8/1N3K2 w - - 0 1", "b1d2"));
    }
}
