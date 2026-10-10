using System.Collections.Generic;
using System.Linq;
using Chess.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Chess.View
{
    // Thin 3D front end for Chess.Core.Game: draws the pieces, turns clicks into squares, shows the status.
    public class ChessView : MonoBehaviour
    {
        // Prefabs in the order King, Queen, Bishop, Knight, Rook, Pawn.
        public GameObject[] whitePrefabs, blackPrefabs;
        public Material highlight;
        const float squareSize = 1.5f; // 12 units of playing area on the board model / 8
        const int aiDepth = 4;

        const float FieldY = 0.068f; // surface of the playing squares on the board model
        const string Order = "KQBNRP";

        readonly Game game = new Game();
        readonly GameObject[] pieces = new GameObject[64];
        readonly char[] shown = Enumerable.Repeat(Board.Empty, 64).ToArray();
        readonly List<GameObject> marks = new List<GameObject>();
        int selected = -1;
        float aiTimer = -1;
        Text statusText;

        public string StatusText => statusText.text;

        void Start()
        {
            BuildUi();
            NewGame();
        }

        void Update()
        {
            for (int sq = 0; sq < 64; sq++)
                if (pieces[sq] != null)
                    pieces[sq].transform.position = Vector3.MoveTowards(pieces[sq].transform.position, SquarePos(sq), 12f * Time.deltaTime);

            if (aiTimer >= 0 && (aiTimer -= Time.deltaTime) < 0)
            {
                game.AiMove(aiDepth);
                Refresh();
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (new Plane(Vector3.up, new Vector3(0, FieldY, 0)).Raycast(ray, out float d))
                {
                    var p = ray.GetPoint(d);
                    int f = Mathf.FloorToInt(p.x / squareSize + 4), r = Mathf.FloorToInt(p.z / squareSize + 4);
                    if (f >= 0 && f < 8 && r >= 0 && r < 8) Click(r * 8 + f);
                }
            }
        }

        public void NewGame()
        {
            game.Restart();
            selected = -1;
            aiTimer = -1;
            for (int sq = 0; sq < 64; sq++) { Destroy(pieces[sq]); pieces[sq] = null; shown[sq] = Board.Empty; }
            Refresh();
            SnapAll();
        }

        public void Click(int sq)
        {
            if (game.Status != GameStatus.Ongoing || !game.HumanTurn) return;
            if (selected >= 0 && game.TryMove(selected, sq))
            {
                selected = -1;
                Refresh();
                if (game.Status == GameStatus.Ongoing) aiTimer = 0.4f;
                return;
            }
            selected = game.TargetsFrom(sq).Any() ? sq : -1;
            Refresh();
        }

        // Jump every piece to its square at once, skipping the slide animation.
        void SnapAll()
        {
            for (int sq = 0; sq < 64; sq++)
                if (pieces[sq] != null) pieces[sq].transform.position = SquarePos(sq);
        }

        Vector3 SquarePos(int sq) => new Vector3((sq % 8 - 3.5f) * squareSize, 0, (sq / 8 - 3.5f) * squareSize);

        void Refresh()
        {
            SyncPieces();
            ShowMarks();
            var b = game.Board;
            bool check = MoveGen.InCheck(b, b.WhiteToMove);
            statusText.text = game.Status switch
            {
                GameStatus.Checkmate => b.WhiteToMove ? "Checkmate - Black (AI) wins" : "Checkmate - You win!",
                GameStatus.Stalemate => "Stalemate - draw",
                GameStatus.DrawRepetition => "Draw - threefold repetition",
                GameStatus.DrawFiftyMove => "Draw - 50-move rule",
                _ => b.WhiteToMove ? (check ? "Check! Your move" : "Your move (White)") : "AI is thinking...",
            };
        }

        // Match each changed square to a piece object that just left a square, so moves, captures,
        // castling, en passant and promotion all fall out of one diff against the board.
        void SyncPieces()
        {
            var board = game.Board.Squares;
            var vacated = new List<(char piece, GameObject go)>();
            for (int sq = 0; sq < 64; sq++)
                if (shown[sq] != board[sq] && pieces[sq] != null) { vacated.Add((shown[sq], pieces[sq])); pieces[sq] = null; }

            for (int sq = 0; sq < 64; sq++)
            {
                if (shown[sq] == board[sq] || board[sq] == Board.Empty) continue;
                int i = vacated.FindIndex(v => v.piece == board[sq]);
                if (i >= 0) { pieces[sq] = vacated[i].go; vacated.RemoveAt(i); }
                else pieces[sq] = Spawn(board[sq], sq);
            }
            foreach (var v in vacated) Destroy(v.go);
            System.Array.Copy(board, shown, 64);
        }

        GameObject Spawn(char piece, int sq)
        {
            var prefabs = char.IsUpper(piece) ? whitePrefabs : blackPrefabs;
            var holder = new GameObject(piece + " " + Board.Name(sq));
            holder.transform.SetParent(transform, false);
            holder.transform.position = SquarePos(sq);
            var model = Instantiate(prefabs[Order.IndexOf(char.ToUpper(piece))], holder.transform);
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            model.transform.position += Vector3.up * (FieldY - bounds.min.y); // stand on the squares
            return holder;
        }

        void ShowMarks()
        {
            foreach (var m in marks) Destroy(m);
            marks.Clear();
            if (selected < 0) return;
            Mark(selected, Color.yellow, 1f);
            foreach (int to in game.TargetsFrom(selected))
                if (game.Board.Squares[to] == Board.Empty) Mark(to, Color.green, 0.35f);
                else Mark(to, Color.red, 1f);
        }

        // Flat slab on a square; `fraction` is its width as a share of the square.
        void Mark(int sq, Color colour, float fraction)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(m.GetComponent<Collider>());
            m.transform.SetParent(transform, false);
            m.transform.position = SquarePos(sq) + Vector3.up * (FieldY + 0.01f);
            m.transform.localScale = new Vector3(squareSize * fraction, 0.02f, squareSize * fraction);
            var r = m.GetComponent<Renderer>();
            r.sharedMaterial = highlight;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", colour);
            r.SetPropertyBlock(block);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marks.Add(m);
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textGo = new GameObject("Status", typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);
            statusText = textGo.GetComponent<Text>();
            statusText.font = font; statusText.fontSize = 32; statusText.color = Color.white;
            statusText.alignment = TextAnchor.UpperCenter;
            Place(textGo, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(900, 50));

            var buttonGo = new GameObject("New Game", typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(canvasGo.transform, false);
            buttonGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            buttonGo.GetComponent<Button>().onClick.AddListener(NewGame);
            Place(buttonGo, new Vector2(1, 1), new Vector2(-110, -40), new Vector2(180, 50));
            var label = new GameObject("Label", typeof(Text));
            label.transform.SetParent(buttonGo.transform, false);
            var t = label.GetComponent<Text>();
            t.font = font; t.fontSize = 26; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.text = "New Game";
            Place(label, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 50));

            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        static void Place(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
