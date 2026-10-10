using System.Collections.Generic;
using System.Linq;

namespace Chess.Core
{
    public readonly struct Move
    {
        public readonly int From, To;

        public Move(int from, int to) { From = from; To = to; }

        public override string ToString() => Board.Name(From) + Board.Name(To);
    }

    public static class MoveGen
    {
        static readonly (int f, int r)[] Orthogonal = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        static readonly (int f, int r)[] Diagonal = { (1, 1), (1, -1), (-1, 1), (-1, -1) };
        static readonly (int f, int r)[] AllDirs = Orthogonal.Concat(Diagonal).ToArray();
        static readonly (int f, int r)[] KnightJumps = { (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2) };

        // Moves that obey how each piece moves, ignoring whether they leave the mover's king in check.
        public static List<Move> PseudoLegal(Board b)
        {
            var moves = new List<Move>();
            for (int from = 0; from < 64; from++)
            {
                char piece = b.Squares[from];
                if (!Board.Owns(b.WhiteToMove, piece)) continue;
                switch (char.ToLower(piece))
                {
                    case 'p': Pawn(b, from, moves); break;
                    case 'n': Walk(b, from, KnightJumps, 1, moves); break;
                    case 'k': Walk(b, from, AllDirs, 1, moves); break;
                    case 'b': Walk(b, from, Diagonal, 7, moves); break;
                    case 'r': Walk(b, from, Orthogonal, 7, moves); break;
                    case 'q': Walk(b, from, AllDirs, 7, moves); break;
                }
            }
            return moves;
        }

        static void Walk(Board b, int from, (int f, int r)[] dirs, int maxSteps, List<Move> moves)
        {
            foreach (var (df, dr) in dirs)
            {
                int f = from % 8, r = from / 8;
                for (int step = 0; step < maxSteps; step++)
                {
                    f += df; r += dr;
                    if (f < 0 || f > 7 || r < 0 || r > 7) break;
                    char target = b.Squares[r * 8 + f];
                    if (Board.Owns(b.WhiteToMove, target)) break;
                    moves.Add(new Move(from, r * 8 + f));
                    if (target != Board.Empty) break;
                }
            }
        }

        static void Pawn(Board b, int from, List<Move> moves)
        {
            int f = from % 8, r = from / 8;
            int dir = b.WhiteToMove ? 1 : -1, startRank = b.WhiteToMove ? 1 : 6;
            int ahead = from + 8 * dir;
            if (ahead < 0 || ahead > 63) return;

            if (b.Squares[ahead] == Board.Empty)
            {
                moves.Add(new Move(from, ahead));
                if (r == startRank && b.Squares[ahead + 8 * dir] == Board.Empty)
                    moves.Add(new Move(from, ahead + 8 * dir));
            }
            for (int df = -1; df <= 1; df += 2)
            {
                if (f + df < 0 || f + df > 7) continue;
                if (Board.Owns(!b.WhiteToMove, b.Squares[ahead + df]))
                    moves.Add(new Move(from, ahead + df));
            }
        }
    }
}
