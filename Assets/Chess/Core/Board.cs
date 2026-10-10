using System;

namespace Chess.Core
{
    // Pieces are FEN letters: uppercase white, lowercase black. Squares are 0..63, a1 = 0, h8 = 63.
    public class Board
    {
        public const char Empty = '.';
        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public readonly char[] Squares = new char[64];
        public bool WhiteToMove = true;
        public string Castling = "";
        public int EnPassant = -1;

        public char this[string square] => Squares[Sq(square)];

        public static int Sq(string square) => (square[1] - '1') * 8 + (square[0] - 'a');

        public static string Name(int sq) => "" + (char)('a' + sq % 8) + (char)('1' + sq / 8);

        public static bool Owns(bool white, char piece) => white ? char.IsUpper(piece) : char.IsLower(piece);

        public static Board Start() => FromFen(StartFen);

        // Returns a new board; the original is left untouched.
        public Board Apply(Move m)
        {
            var b = new Board { WhiteToMove = !WhiteToMove, Castling = Castling };
            Array.Copy(Squares, b.Squares, 64);
            b.Squares[m.To] = Squares[m.From];
            b.Squares[m.From] = Empty;
            return b;
        }

        public static Board FromFen(string fen)
        {
            var parts = (fen ?? "").Split(' ');
            var ranks = parts[0].Split('/');
            if (parts.Length < 4 || ranks.Length != 8) throw new ArgumentException("Bad FEN: " + fen);

            var b = new Board();
            for (int r = 0; r < 8; r++)
            {
                int file = 0;
                foreach (char c in ranks[7 - r])
                {
                    bool digit = char.IsDigit(c);
                    int run = digit ? c - '0' : 1;
                    if (file + run > 8 || (!digit && "PNBRQKpnbrqk".IndexOf(c) < 0))
                        throw new ArgumentException("Bad FEN: " + fen);
                    for (int i = 0; i < run; i++)
                        b.Squares[r * 8 + file++] = digit ? Empty : c;
                }
                if (file != 8) throw new ArgumentException("Bad FEN: " + fen);
            }
            b.WhiteToMove = parts[1] == "w";
            b.Castling = parts[2] == "-" ? "" : parts[2];
            b.EnPassant = parts[3] == "-" ? -1 : Sq(parts[3]);
            return b;
        }
    }
}
