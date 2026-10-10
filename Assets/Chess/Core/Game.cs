using System.Collections.Generic;
using System.Linq;

namespace Chess.Core
{
    // One game: the human plays White, the AI plays Black.
    public class Game
    {
        readonly string startFen;
        readonly List<string> seen = new List<string>(); // Board.Key of every position reached, for repetition

        public Board Board { get; private set; }
        public bool HumanTurn => Board.WhiteToMove;

        public GameStatus Status
        {
            get
            {
                var status = MoveGen.Status(Board); // mate and stalemate outrank the draw rules
                if (status != GameStatus.Ongoing) return status;
                if (seen.Count(k => k == Board.Key) >= 3) return GameStatus.DrawRepetition;
                return Board.HalfmoveClock >= 100 ? GameStatus.DrawFiftyMove : GameStatus.Ongoing;
            }
        }

        public Game(string fen = Board.StartFen) { startFen = fen; Restart(); }

        public void Restart()
        {
            Board = Board.FromFen(startFen);
            seen.Clear();
            seen.Add(Board.Key);
        }

        public IEnumerable<int> TargetsFrom(int from) => !HumanTurn
            ? Enumerable.Empty<int>()
            : MoveGen.Legal(Board).Where(m => m.From == from).Select(m => m.To).Distinct();

        // Plays a move for either side. Callers pass moves taken from MoveGen.Legal.
        public void Play(Move m)
        {
            Board = Board.Apply(m);
            seen.Add(Board.Key);
        }

        // Promotion is always to a queen.
        public bool TryMove(int from, int to)
        {
            if (!HumanTurn) return false;
            foreach (var m in MoveGen.Legal(Board))
                if (m.From == from && m.To == to && (m.Promo == '\0' || m.Promo == 'q'))
                {
                    Play(m);
                    return true;
                }
            return false;
        }

        public bool AiMove(int depth = 4)
        {
            if (HumanTurn || Ai.Best(Board, depth) is not Move m) return false;
            Play(m);
            return true;
        }
    }
}
