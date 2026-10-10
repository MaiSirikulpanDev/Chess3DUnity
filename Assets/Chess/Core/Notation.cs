using System;
using System.Linq;

namespace Chess.Core
{
    public static class Notation
    {
        // Standard algebraic notation for a legal move played on `before`: Nf3, exd5, O-O, e8=Q+, Qh4#.
        public static string San(Board before, Move m)
        {
            char kind = char.ToUpper(before.Squares[m.From]);
            string san;
            if (kind == 'K' && Math.Abs(m.To - m.From) == 2)
                san = m.To > m.From ? "O-O" : "O-O-O";
            else
            {
                bool capture = before.Squares[m.To] != Board.Empty || (kind == 'P' && m.From % 8 != m.To % 8);
                san = kind == 'P' ? (capture ? File(m.From).ToString() : "") : kind + Disambiguation(before, m, kind);
                san += (capture ? "x" : "") + Board.Name(m.To) + (m.Promo == '\0' ? "" : "=" + char.ToUpper(m.Promo));
            }

            var after = before.Apply(m);
            if (!MoveGen.InCheck(after, after.WhiteToMove)) return san;
            return san + (MoveGen.Legal(after).Count == 0 ? "#" : "+");
        }

        static char File(int sq) => (char)('a' + sq % 8);

        // The file, the rank, or both, when another piece of the same kind could also reach the square.
        static string Disambiguation(Board before, Move m, char kind)
        {
            var rivals = MoveGen.Legal(before)
                .Where(o => o.To == m.To && o.From != m.From && char.ToUpper(before.Squares[o.From]) == kind).ToList();
            if (rivals.Count == 0) return "";
            if (rivals.All(o => o.From % 8 != m.From % 8)) return File(m.From).ToString();
            if (rivals.All(o => o.From / 8 != m.From / 8)) return ((char)('1' + m.From / 8)).ToString();
            return Board.Name(m.From);
        }
    }
}
