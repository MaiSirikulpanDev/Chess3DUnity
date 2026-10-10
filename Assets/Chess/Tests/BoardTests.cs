using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    public class BoardTests
    {
        [Test]
        public void SquareIndex_A1IsZero_H8Is63()
        {
            Assert.AreEqual(0, Board.Sq("a1"));
            Assert.AreEqual(7, Board.Sq("h1"));
            Assert.AreEqual(8, Board.Sq("a2"));
            Assert.AreEqual(63, Board.Sq("h8"));
        }

        [TestCase("a1", 'R')] [TestCase("b1", 'N')] [TestCase("c1", 'B')] [TestCase("d1", 'Q')]
        [TestCase("e1", 'K')] [TestCase("f1", 'B')] [TestCase("g1", 'N')] [TestCase("h1", 'R')]
        [TestCase("a2", 'P')] [TestCase("h2", 'P')]
        [TestCase("a7", 'p')] [TestCase("h7", 'p')]
        [TestCase("a8", 'r')] [TestCase("d8", 'q')] [TestCase("e8", 'k')] [TestCase("h8", 'r')]
        [TestCase("e4", Board.Empty)] [TestCase("a3", Board.Empty)] [TestCase("h6", Board.Empty)]
        public void StartPosition_HasPieceOn(string square, char piece)
        {
            Assert.AreEqual(piece, Board.Start()[square]);
        }

        [Test]
        public void StartPosition_WhiteToMove_AllCastlingRights_NoEnPassant()
        {
            var b = Board.Start();
            Assert.IsTrue(b.WhiteToMove);
            Assert.AreEqual("KQkq", b.Castling);
            Assert.AreEqual(-1, b.EnPassant);
        }

        [Test]
        public void FromFen_ReadsSideCastlingAndEnPassant()
        {
            var b = Board.FromFen("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQK2R b Kq e3 0 2");
            Assert.IsFalse(b.WhiteToMove);
            Assert.AreEqual("Kq", b.Castling);
            Assert.AreEqual(Board.Sq("e3"), b.EnPassant);
            Assert.AreEqual('P', b["e4"]);
            Assert.AreEqual('p', b["e5"]);
            Assert.AreEqual(Board.Empty, b["e2"]);
            Assert.AreEqual(Board.Empty, b["g1"]);
        }

        [Test]
        public void FromFen_NoCastlingRights_IsEmptyString()
        {
            Assert.AreEqual("", Board.FromFen("8/8/8/8/8/8/8/K6k w - - 0 1").Castling);
        }

        [TestCase("")]
        [TestCase("8/8/8/8/8/8/8 w - - 0 1")]
        [TestCase("9/8/8/8/8/8/8/8 w - - 0 1")]
        [TestCase("8/8/8/8/8/8/8/7x w - - 0 1")]
        public void FromFen_RejectsMalformedInput(string fen)
        {
            Assert.Throws<System.ArgumentException>(() => Board.FromFen(fen));
        }
    }
}
