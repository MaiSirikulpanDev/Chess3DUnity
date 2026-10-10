using System.Collections.Generic;
using System.Linq;

namespace Chess.Core
{
    public readonly struct Move
    {
        public readonly int From, To;
        public readonly char Promo; // '\0', or q/r/b/n when a pawn promotes

        public Move(int from, int to, char promo = '\0') { From = from; To = to; Promo = promo; }

        public override string ToString() => Board.Name(From) + Board.Name(To) + (Promo == '\0' ? "" : Promo.ToString());
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
            Castle(b, moves);
            return moves;
        }


        public static List<Move> Legal(Board b) =>
            PseudoLegal(b).Where(m => !InCheck(b.Apply(m), b.WhiteToMove)).ToList();

        public static bool InCheck(Board b, bool white) =>
            IsAttacked(b, System.Array.IndexOf(b.Squares, white ? 'K' : 'k'), !white);

        // Is `sq` attacked by the given side? Looks outward from the square for each attacker type.
        public static bool IsAttacked(Board b, int sq, bool byWhite)
        {
            char Piece(char white) => byWhite ? white : char.ToLower(white);
            int f = sq % 8, r = sq / 8;

            int pawnRank = r + (byWhite ? -1 : 1);
            for (int df = -1; df <= 1; df += 2)
                if (At(b, f + df, pawnRank) == Piece('P')) return true;

            foreach (var (df, dr) in KnightJumps)
                if (At(b, f + df, r + dr) == Piece('N')) return true;
            foreach (var (df, dr) in AllDirs)
                if (At(b, f + df, r + dr) == Piece('K')) return true;

            return Slides(b, f, r, Orthogonal, Piece('R'), Piece('Q')) || Slides(b, f, r, Diagonal, Piece('B'), Piece('Q'));
        }

        static char At(Board b, int f, int r) => f < 0 || f > 7 || r < 0 || r > 7 ? '#' : b.Squares[r * 8 + f];

        static bool Slides(Board b, int f, int r, (int f, int r)[] dirs, char a, char c)
        {
            foreach (var (df, dr) in dirs)
                for (int x = f + df, y = r + dr; ; x += df, y += dr)
                {
                    char p = At(b, x, y);
                    if (p == a || p == c) return true;
                    if (p != Board.Empty) break;
                }
            return false;
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

            void Add(int to)
            {
                if (to / 8 == (b.WhiteToMove ? 7 : 0)) foreach (char p in "qrbn") moves.Add(new Move(from, to, p));
                else moves.Add(new Move(from, to));
            }

            if (b.Squares[ahead] == Board.Empty)
            {
                Add(ahead);
                if (r == startRank && b.Squares[ahead + 8 * dir] == Board.Empty)
                    moves.Add(new Move(from, ahead + 8 * dir));
            }
            for (int df = -1; df <= 1; df += 2)
            {
                if (f + df < 0 || f + df > 7) continue;
                if (Board.Owns(!b.WhiteToMove, b.Squares[ahead + df]) || ahead + df == b.EnPassant)
                    Add(ahead + df);
            }
        }

        static void Castle(Board b, List<Move> moves)
        {
            bool w = b.WhiteToMove;
            int k = w ? 4 : 60;
            if (b.Squares[k] != (w ? 'K' : 'k') || IsAttacked(b, k, !w)) return;
            char rook = w ? 'R' : 'r';
            if (b.Castling.IndexOf(w ? 'K' : 'k') >= 0 && b.Squares[k + 3] == rook && CanPass(b, k, 1, 2, w))
                moves.Add(new Move(k, k + 2));
            if (b.Castling.IndexOf(w ? 'Q' : 'q') >= 0 && b.Squares[k - 4] == rook && b.Squares[k - 3] == Board.Empty && CanPass(b, k, -1, 2, w))
                moves.Add(new Move(k, k - 2));
        }

        // The squares the king crosses and lands on must be empty and unattacked.
        static bool CanPass(Board b, int k, int dir, int n, bool w)
        {
            for (int i = 1; i <= n; i++)
                if (b.Squares[k + dir * i] != Board.Empty || IsAttacked(b, k + dir * i, !w)) return false;
            return true;
        }
    }
}
