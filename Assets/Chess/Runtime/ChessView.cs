using System.Collections.Generic;
using System.Linq;
using Chess.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Events;
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
        // Search depth and move-choice tolerance (see Ai.Best): the easier the level, the shallower and looser the AI.
        static readonly (string name, int depth, int tolerance)[] Levels = { ("Easy", 1, 60), ("Normal", 3, 15), ("Hard", 4, 0) };

        const float FieldY = 0.068f; // surface of the playing squares on the board model
        const string Order = "KQBNRP";

        Game game = new Game();
        readonly GameObject[] pieces = new GameObject[64];
        readonly char[] shown = Enumerable.Repeat(Board.Empty, 64).ToArray();
        readonly List<GameObject> marks = new List<GameObject>();
        int selected = -1, promotionTo;
        float aiTimer = -1;
        Text statusText;
        GameObject picker; // the Queen / Rook / Bishop / Knight choice shown when a pawn reaches the last rank
        Button undoButton, levelButton;
        int level = 1;
        Vector3 cameraOffset; // where the camera starts, relative to the board centre
        float yaw, targetYaw;
        Font font;

        public string StatusText => statusText.text;
        public bool PromotionPending => picker != null && picker.activeSelf;
        public string LevelName => Levels[level].name;

        void Start()
        {
            cameraOffset = Camera.main.transform.position;
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
                game.AiMove(Levels[level].depth, Levels[level].tolerance);
                Refresh();
            }

            OrbitCamera();

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                int sq = SquareAt(Mouse.current.position.ReadValue());
                if (sq >= 0) Click(sq);
            }
        }

        // The square under a screen point, or -1 off the board.
        public int SquareAt(Vector2 screen)
        {
            var ray = Camera.main.ScreenPointToRay(screen);
            if (!new Plane(Vector3.up, new Vector3(0, FieldY, 0)).Raycast(ray, out float d)) return -1;
            var p = ray.GetPoint(d);
            int f = Mathf.FloorToInt(p.x / squareSize + 4), r = Mathf.FloorToInt(p.z / squareSize + 4);
            return f >= 0 && f < 8 && r >= 0 && r < 8 ? r * 8 + f : -1;
        }

        // Turns the camera round the board: A/D or the arrow keys, right mouse drag, or the Flip View button.
        void OrbitCamera()
        {
            var keys = Keyboard.current;
            if (keys != null)
                targetYaw += ((keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0)) * 90f * Time.deltaTime;
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                targetYaw += Mouse.current.delta.ReadValue().x * 0.3f;

            yaw = Mathf.LerpAngle(yaw, targetYaw, 1 - Mathf.Exp(-10 * Time.deltaTime));
            var cam = Camera.main.transform;
            cam.position = Quaternion.Euler(0, yaw, 0) * cameraOffset;
            cam.LookAt(Vector3.zero);
        }

        public void RotateView(float degrees) => targetYaw += degrees;

        // Starts a game from an arbitrary position (used by tests).
        public void StartFrom(string fen)
        {
            game = new Game(fen);
            NewGame();
        }

        // Takes back the AI reply (if it has moved) and the player's last move.
        public void Undo()
        {
            aiTimer = -1;
            picker.SetActive(false);
            selected = -1;
            game.Undo();
            Refresh();
        }

        public void CycleLevel()
        {
            level = (level + 1) % Levels.Length;
            levelButton.GetComponentInChildren<Text>().text = "Level: " + LevelName;
        }

        public void NewGame()
        {
            game.Restart();
            selected = -1;
            aiTimer = -1;
            picker.SetActive(false);
            for (int sq = 0; sq < 64; sq++) { Destroy(pieces[sq]); pieces[sq] = null; shown[sq] = Board.Empty; }
            Refresh();
            SnapAll();
        }

        public void Click(int sq)
        {
            if (game.Status != GameStatus.Ongoing || !game.HumanTurn) return;
            if (PromotionPending) // clicking the board instead of a piece cancels the choice
            {
                picker.SetActive(false);
                selected = -1;
                Refresh();
                return;
            }
            if (selected >= 0 && game.IsPromotion(selected, sq))
            {
                promotionTo = sq;
                picker.SetActive(true);
                statusText.text = "Promote to...";
                return;
            }
            if (selected >= 0 && game.TryMove(selected, sq)) { Moved(); return; }
            selected = game.TargetsFrom(sq).Any() ? sq : -1;
            Refresh();
        }

        // `piece` is q, r, b or n.
        public void Promote(char piece)
        {
            picker.SetActive(false);
            if (selected >= 0 && game.TryMove(selected, promotionTo, piece)) Moved();
        }

        void Moved()
        {
            selected = -1;
            Refresh();
            if (game.Status == GameStatus.Ongoing) aiTimer = 0.4f;
        }

        // Jump every piece to its square at once, skipping the slide animation.
        void SnapAll()
        {
            for (int sq = 0; sq < 64; sq++)
                if (pieces[sq] != null) pieces[sq].transform.position = SquarePos(sq);
        }

        public Vector3 SquarePos(int sq) => new Vector3((sq % 8 - 3.5f) * squareSize, 0, (sq / 8 - 3.5f) * squareSize);

        void Refresh()
        {
            SyncPieces();
            ShowMarks();
            undoButton.interactable = game.CanUndo;
            var b = game.Board;
            bool check = MoveGen.InCheck(b, b.WhiteToMove);
            statusText.text = game.Status switch
            {
                GameStatus.Checkmate => b.WhiteToMove ? "Checkmate - Black (AI) wins" : "Checkmate - You win!",
                GameStatus.Stalemate => "Stalemate - draw",
                GameStatus.DrawRepetition => "Draw - threefold repetition",
                GameStatus.DrawFiftyMove => "Draw - 50-move rule",
                GameStatus.DrawInsufficientMaterial => "Draw - insufficient material",
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
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // not Overlay, so camera renders (screenshots) include the UI
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textGo = new GameObject("Status", typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);
            statusText = textGo.GetComponent<Text>();
            statusText.font = font; statusText.fontSize = 32; statusText.color = Color.white;
            statusText.alignment = TextAnchor.UpperCenter;
            Place(textGo, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(900, 50));

            MakeButton(canvasGo.transform, "New Game", new Vector2(1, 1), new Vector2(-110, -40), new Vector2(180, 50), NewGame);
            undoButton = MakeButton(canvasGo.transform, "Undo", new Vector2(1, 1), new Vector2(-110, -100), new Vector2(180, 50), Undo);
            levelButton = MakeButton(canvasGo.transform, "", new Vector2(1, 1), new Vector2(-110, -160), new Vector2(180, 50), CycleLevel);
            levelButton.GetComponentInChildren<Text>().text = "Level: " + LevelName;
            MakeButton(canvasGo.transform, "Flip View", new Vector2(1, 1), new Vector2(-110, -220), new Vector2(180, 50), () => RotateView(180));

            picker = new GameObject("Promotion", typeof(Image));
            picker.transform.SetParent(canvasGo.transform, false);
            picker.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            Place(picker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 90));
            string[] names = { "Queen", "Rook", "Bishop", "Knight" };
            for (int i = 0; i < 4; i++)
            {
                char piece = "qrbn"[i];
                MakeButton(picker.transform, names[i], new Vector2(0.5f, 0.5f), new Vector2(-255 + i * 170, 0), new Vector2(160, 60), () => Promote(piece));
            }

            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            Place(go, anchor, pos, size);

            var text = new GameObject("Label", typeof(Text));
            text.transform.SetParent(go.transform, false);
            var t = text.GetComponent<Text>();
            t.font = font; t.fontSize = 26; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.text = label;
            Place(text, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return button;
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
