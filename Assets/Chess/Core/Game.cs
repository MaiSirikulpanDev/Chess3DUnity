using System;
using System.Collections.Generic;
using System.Linq;

namespace Chess.Core
{
    // One game: the human plays one colour (White unless told otherwise), the AI the other.
    public class Game
    {
        readonly string startFen;
        // Every move played, with the position it was played from and its notation: undo, repetition, move list.
        readonly List<(Board before, Move move, string san)> history = new List<(Board before, Move move, string san)>();

        public Board Board { get; private set; }
        public bool HumanIsWhite { get; }
        public bool HumanTurn => Board.WhiteToMove == HumanIsWhite;
        public bool CanUndo => history.Count > 0;
        public Move? LastMove => history.Count > 0 ? history[history.Count - 1].move : (Move?)null;

        public GameStatus Status
        {
            get
            {
                var status = MoveGen.Status(Board); // mate and stalemate outrank the draw rules
                if (status != GameStatus.Ongoing) return status;
                if (Board.InsufficientMaterial) return GameStatus.DrawInsufficientMaterial;
                if (history.Count(h => h.before.Key == Board.Key) >= 2) return GameStatus.DrawRepetition;
                return Board.HalfmoveClock >= 100 ? GameStatus.DrawFiftyMove : GameStatus.Ongoing;
            }
        }

        public Game(string fen = Board.StartFen, bool humanIsWhite = true) { startFen = fen; HumanIsWhite = humanIsWhite; Restart(); }

        public void Restart()
        {
            Board = Board.FromFen(startFen);
            history.Clear();
        }

        // Goes back to the start of the human's previous turn: takes back the AI's reply if it has moved,
        // then the human's own move.
        public bool Undo()
        {
            if (!CanUndo) return false;
            do
            {
                Board = history[history.Count - 1].before;
                history.RemoveAt(history.Count - 1);
            } while (!HumanTurn && CanUndo);
            return true;
        }

        // "1. e4 e5" per line, the last `maxLines` lines only. Assumes White made the first move.
        public string MoveListText(int maxLines = 12)
        {
            var lines = Enumerable.Range(0, (history.Count + 1) / 2)
                .Select(i => (i + 1) + ". " + string.Join(" ", history.Skip(2 * i).Take(2).Select(h => h.san)));
            return string.Join("\n", lines.Skip(Math.Max(0, (history.Count + 1) / 2 - maxLines)));
        }

        public IEnumerable<int> TargetsFrom(int from) => !HumanTurn
            ? Enumerable.Empty<int>()
            : MoveGen.Legal(Board).Where(m => m.From == from).Select(m => m.To).Distinct();

        // A move that ends the game in a draw (never a mate: that is a win).
        bool WouldDraw(Board next) =>
            (next.HalfmoveClock >= 100 || next.InsufficientMaterial || history.Count(h => h.before.Key == next.Key) >= 2) && MoveGen.Status(next) == GameStatus.Ongoing;

        // Plays a move for either side. Callers pass moves taken from MoveGen.Legal.
        public void Play(Move m)
        {
            history.Add((Board, m, Notation.San(Board, m)));
            Board = Board.Apply(m);
        }

        public bool IsPromotion(int from, int to) =>
            HumanTurn && MoveGen.Legal(Board).Any(m => m.From == from && m.To == to && m.Promo != '\0');

        // `promo` (q, r, b or n) only matters when the move is a promotion.
        public bool TryMove(int from, int to, char promo = 'q')
        {
            if (!HumanTurn) return false;
            foreach (var m in MoveGen.Legal(Board))
                if (m.From == from && m.To == to && (m.Promo == '\0' || m.Promo == promo))
                {
                    Play(m);
                    return true;
                }
            return false;
        }

        public bool AiMove(int depth = 4, int tolerance = 0)
        {
            if (HumanTurn || Ai.Best(Board, depth, WouldDraw, tolerance) is not Move m) return false;
            Play(m);
            return true;
        }
    }
}
