using System.Collections.Generic;
using System.Linq;

namespace Chess.Core
{
    // One game: the human plays White, the AI plays Black.
    public class Game
    {
        readonly string startFen;
        readonly List<string> seen = new List<string>(); // Board.Key of every position reached, for repetition
        readonly List<Board> history = new List<Board>(); // every earlier position, for undo

        public Board Board { get; private set; }
        public bool HumanTurn => Board.WhiteToMove;
        public bool CanUndo => history.Count > 0;

        public GameStatus Status
        {
            get
            {
                var status = MoveGen.Status(Board); // mate and stalemate outrank the draw rules
                if (status != GameStatus.Ongoing) return status;
                if (Board.InsufficientMaterial) return GameStatus.DrawInsufficientMaterial;
                if (seen.Count(k => k == Board.Key) >= 3) return GameStatus.DrawRepetition;
                return Board.HalfmoveClock >= 100 ? GameStatus.DrawFiftyMove : GameStatus.Ongoing;
            }
        }

        public Game(string fen = Board.StartFen) { startFen = fen; Restart(); }

        public void Restart()
        {
            Board = Board.FromFen(startFen);
            seen.Clear();
            history.Clear();
            seen.Add(Board.Key);
        }

        // Goes back to the start of the human's previous turn: takes back the AI's reply if it has moved,
        // then the human's own move.
        public bool Undo()
        {
            if (!CanUndo) return false;
            do
            {
                Board = history[history.Count - 1];
                history.RemoveAt(history.Count - 1);
                seen.RemoveAt(seen.Count - 1);
            } while (!HumanTurn && CanUndo);
            return true;
        }

        public IEnumerable<int> TargetsFrom(int from) => !HumanTurn
            ? Enumerable.Empty<int>()
            : MoveGen.Legal(Board).Where(m => m.From == from).Select(m => m.To).Distinct();

        // A move that ends the game in a draw (never a mate: that is a win).
        bool WouldDraw(Board next) =>
            (next.HalfmoveClock >= 100 || next.InsufficientMaterial || seen.Count(k => k == next.Key) >= 2) && MoveGen.Status(next) == GameStatus.Ongoing;

        // Plays a move for either side. Callers pass moves taken from MoveGen.Legal.
        public void Play(Move m)
        {
            history.Add(Board);
            Board = Board.Apply(m);
            seen.Add(Board.Key);
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

        public bool AiMove(int depth = 4)
        {
            if (HumanTurn || Ai.Best(Board, depth, WouldDraw) is not Move m) return false;
            Play(m);
            return true;
        }
    }
}
