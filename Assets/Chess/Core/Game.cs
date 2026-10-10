using System.Collections.Generic;
using System.Linq;

namespace Chess.Core
{
    // One game: the human plays White, the AI plays Black.
    public class Game
    {
        readonly string startFen;

        public Board Board { get; private set; }
        public GameStatus Status => MoveGen.Status(Board);
        public bool HumanTurn => Board.WhiteToMove;

        public Game(string fen = Board.StartFen) { startFen = fen; Restart(); }

        public void Restart() => Board = Board.FromFen(startFen);

        public IEnumerable<int> TargetsFrom(int from) => !HumanTurn
            ? Enumerable.Empty<int>()
            : MoveGen.Legal(Board).Where(m => m.From == from).Select(m => m.To).Distinct();

        // Promotion is always to a queen.
        public bool TryMove(int from, int to)
        {
            if (!HumanTurn) return false;
            foreach (var m in MoveGen.Legal(Board))
                if (m.From == from && m.To == to && (m.Promo == '\0' || m.Promo == 'q'))
                {
                    Board = Board.Apply(m);
                    return true;
                }
            return false;
        }

        public bool AiMove(int depth = 4)
        {
            if (HumanTurn || Ai.Best(Board, depth) is not Move m) return false;
            Board = Board.Apply(m);
            return true;
        }
    }
}
