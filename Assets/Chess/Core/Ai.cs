using System;
using System.Linq;

namespace Chess.Core
{
    // Negamax with alpha-beta. Scores are from the side to move's point of view.
    public static class Ai
    {
        const int Mate = 100000;
        static readonly Random Rng = new Random();

        // `isDraw` flags moves that would end the game in a draw; those score 0 instead of their search value.
        // Any move scoring within `tolerance` of the best may be played, picked at random: 0 means exact ties only.
        public static Move? Best(Board b, int depth, Func<Board, bool> isDraw = null, int tolerance = 0, Random rng = null)
        {
            var scored = new System.Collections.Generic.List<(Move move, int score)>();
            int best = -2 * Mate;
            foreach (var m in Ordered(b))
            {
                var next = b.Apply(m);
                // The window stays open down to best - tolerance, so every score inside the tolerance is exact.
                int score = isDraw?.Invoke(next) == true ? 0 : -Search(next, depth - 1, -2 * Mate, -(best - tolerance - 1), 1);
                best = Math.Max(best, score);
                scored.Add((m, score));
            }
            var pool = scored.Where(s => s.score >= best - tolerance).ToList();
            return pool.Count == 0 ? null : pool[(rng ?? Rng).Next(pool.Count)].move;
        }

        static int Search(Board b, int depth, int alpha, int beta, int ply)
        {
            if (depth == 0) return Eval(b);
            var moves = Ordered(b);
            if (moves.Length == 0) return MoveGen.InCheck(b, b.WhiteToMove) ? -(Mate - ply) : 0;
            foreach (var m in moves)
            {
                alpha = Math.Max(alpha, -Search(b.Apply(m), depth - 1, -beta, -alpha, ply + 1));
                if (alpha >= beta) break;
            }
            return alpha;
        }

        // Captures first so alpha-beta cuts early.
        static Move[] Ordered(Board b) =>
            MoveGen.Legal(b).OrderByDescending(m => Value(b.Squares[m.To])).ToArray();

        static int Value(char piece) => char.ToLower(piece) switch
        {
            'p' => 100, 'n' => 320, 'b' => 330, 'r' => 500, 'q' => 900, _ => 0,
        };

        static int Eval(Board b)
        {
            int score = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                char p = b.Squares[sq];
                if (p == Board.Empty) continue;
                bool white = char.IsUpper(p);
                int f = sq % 8, r = sq / 8;
                int centre = 7 - Math.Max(Math.Abs(2 * f - 7), Math.Abs(2 * r - 7)); // 0 on the edge .. 6 in the middle
                int bonus = char.ToLower(p) switch
                {
                    'n' or 'b' => centre * 3,
                    'p' => centre + (white ? r : 7 - r) * 4, // pushes forward, prefers the middle
                    _ => 0,
                };
                score += (white ? 1 : -1) * (Value(p) + bonus);
            }
            return b.WhiteToMove ? score : -score;
        }
    }
}
