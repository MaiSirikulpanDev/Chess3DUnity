using System.Collections;
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
        public const string LevelKey = "chess.level", WhiteKey = "chess.white";

        Game game = new Game();
        readonly GameObject[] pieces = new GameObject[64];
        readonly char[] shown = Enumerable.Repeat(Board.Empty, 64).ToArray();
        readonly List<GameObject> marks = new List<GameObject>();
        int selected = -1, promotionTo;
        float aiTimer = -1;
        Text statusText;
        GameObject picker; // the Queen / Rook / Bishop / Knight choice shown when a pawn reaches the last rank
        Button undoButton, levelButton;
        Transform marksRoot; // keeps highlight slabs out of the pieces' parent
        Text moveListText;
        StartMenu menu;
        GameObject hud;
        bool humanWhite = true;
        public System.Action quitAction = QuitApplication; // replaced by tests
        int level = 1;
        float cameraDistance; // from the middle of the board, kept while orbiting
        float yaw, targetYaw, pitch, targetPitch;
        AudioSource speaker;
        Font font;

        public string StatusText => statusText.text;
        public bool PromotionPending => picker != null && picker.activeSelf;
        public string LevelName => Levels[level].name;
        public bool MenuOpen => menu.IsOpen;
        public bool OnSetupScreen => menu.OnSetup;
        public int MenuLevel => menu.Level;
        public bool MenuPlaysWhite => menu.PlaysWhite;
        public bool HumanIsWhite => game.HumanIsWhite;
        public int MarkCount => marks.Count;
        public string MoveListText => moveListText.text;
        public string LastSound { get; private set; } = ""; // "move" or "capture"; empty until the first one
        public int BurstsStarted { get; private set; }
        public AudioClip MoveClip { get; private set; }
        public AudioClip CaptureClip { get; private set; }

        void Start()
        {
            level = Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, level), 0, Levels.Length - 1);
            humanWhite = PlayerPrefs.GetInt(WhiteKey, 1) == 1;
            marksRoot = new GameObject("Marks").transform;
            var start = Camera.main.transform.position - new Vector3(0, FieldY, 0);
            cameraDistance = start.magnitude;
            pitch = targetPitch = Mathf.Asin(start.y / cameraDistance) * Mathf.Rad2Deg;
            speaker = gameObject.AddComponent<AudioSource>();
            MoveClip = Thump("move", 180, 0.09f, 0.5f, 45, 0.5f);
            CaptureClip = Thump("capture", 90, 0.28f, 0.6f, 14, 0.9f);
            BuildUi();
            NewGame();
            OpenMenu();
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

        // Turns the camera round the board (A/D or left/right arrows, right-drag sideways, Flip View) and tilts it
        // (W/S or up/down arrows, right-drag up and down). It can come down level with the playing surface but no
        // lower, so the underside of the board is never in view, and up to almost straight above.
        void OrbitCamera()
        {
            if (menu.IsOpen) targetYaw += 8f * Time.deltaTime; // the board turns slowly behind the menu
            var keys = menu.IsOpen ? null : Keyboard.current;
            if (keys != null)
            {
                targetYaw += ((keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0)) * 90f * Time.deltaTime;
                TiltView(((keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0)) * 60f * Time.deltaTime);
            }
            if (!menu.IsOpen && Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                var drag = Mouse.current.delta.ReadValue();
                targetYaw += drag.x * 0.3f;
                TiltView(-drag.y * 0.3f); // pulling the board towards you lifts the camera
            }

            float ease = 1 - Mathf.Exp(-10 * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, targetYaw, ease);
            pitch = Mathf.Lerp(pitch, targetPitch, ease);
            var cam = Camera.main.transform;
            var aim = new Vector3(0, FieldY, 0);
            var direction = new Vector3(0, Mathf.Sin(pitch * Mathf.Deg2Rad), -Mathf.Cos(pitch * Mathf.Deg2Rad));
            cam.position = aim + Quaternion.Euler(0, yaw, 0) * (direction * cameraDistance);
            cam.LookAt(aim);
        }

        public void RotateView(float degrees) => targetYaw += degrees;

        // Degrees above the horizon: 0 is level with the board, 85 almost straight down.
        public void TiltView(float degrees) => targetPitch = Mathf.Clamp(targetPitch + degrees, 0, 85);

        // ---- start menu: Start Game -> setup (level, colour) -> Play ----
        public void StartGame() => menu.ShowSetup();
        public void PickLevel(int index) => menu.PickLevel(index);
        public void PickColour(bool white) => menu.PickColour(white);

        public void OpenMenu()
        {
            hud.SetActive(false);
            picker.SetActive(false);
            menu.ShowTitle();
        }

        // Begins a game with the level and colour picked on the setup screen, and remembers them for next time.
        public void Play()
        {
            level = menu.Level;
            humanWhite = menu.PlaysWhite;
            PlayerPrefs.SetInt(LevelKey, level);
            PlayerPrefs.SetInt(WhiteKey, humanWhite ? 1 : 0);
            PlayerPrefs.Save();
            ShowLevel();
            menu.Hide();
            hud.SetActive(true);
            game = new Game(Board.StartFen, humanWhite);
            yaw = targetYaw = humanWhite ? 0 : 180; // sit on your own side of the board
            NewGame();
        }

        public void ExitGame() => quitAction();

        static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // When you play Black the AI has the first move; after an Undo it can be back on the move too.
        void AiToMoveIfItsTurn()
        {
            if (!game.HumanTurn && game.Status == GameStatus.Ongoing) aiTimer = 0.6f;
        }

        // Starts a game from an arbitrary position (used by tests).
        public void StartFrom(string fen)
        {
            game = new Game(fen, humanWhite);
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
            AiToMoveIfItsTurn();
        }

        public void CycleLevel()
        {
            level = (level + 1) % Levels.Length;
            PlayerPrefs.SetInt(LevelKey, level);
            PlayerPrefs.Save();
            ShowLevel();
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
            AiToMoveIfItsTurn();
        }

        public void Click(int sq)
        {
            if (menu.IsOpen || game.Status != GameStatus.Ongoing || !game.HumanTurn) return;
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
            moveListText.text = game.MoveListText();
            var b = game.Board;
            bool check = MoveGen.InCheck(b, b.WhiteToMove);
            statusText.text = game.Status switch
            {
                GameStatus.Checkmate => game.HumanTurn ? "Checkmate - AI wins" : "Checkmate - You win!",
                GameStatus.Stalemate => "Stalemate - draw",
                GameStatus.DrawRepetition => "Draw - threefold repetition",
                GameStatus.DrawFiftyMove => "Draw - 50-move rule",
                GameStatus.DrawInsufficientMaterial => "Draw - insufficient material",
                _ => (check ? "Check! " : "") + (game.HumanTurn ? (check ? "Your move" : "Your move (" + (game.HumanIsWhite ? "White" : "Black") + ")") : "AI is thinking..."),
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
            // The very first fill of an empty board (new game, restart) is silent.
            int before = shown.Count(c => c != Board.Empty), after = board.Count(c => c != Board.Empty);
            if (before > 0 && !shown.SequenceEqual(board))
            {
                bool capture = after < before; // a promotion keeps the count, so it is not a capture
                Play(capture ? "capture" : "move");
                if (capture) foreach (var v in vacated) StartCoroutine(Burst(v.go.transform.position, char.IsUpper(v.piece) ? new Color(0.9f, 0.9f, 0.95f) : new Color(0.15f, 0.15f, 0.18f)));
            }
            foreach (var v in vacated) Destroy(v.go);
            System.Array.Copy(board, shown, 64);
        }

        void Play(string sound)
        {
            LastSound = sound;
            speaker.PlayOneShot(sound == "capture" ? CaptureClip : MoveClip);
        }

        // A short percussive hit made in code (no audio files in the project): a sine mixed with noise that decays.
        static AudioClip Thump(string name, float frequency, float seconds, float noise, float decay, float volume)
        {
            const int rate = 44100;
            var samples = new float[(int)(rate * seconds)];
            var rng = new System.Random(7);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float tone = Mathf.Sin(2 * Mathf.PI * frequency * t), hiss = (float)(rng.NextDouble() * 2 - 1);
                samples[i] = ((1 - noise) * tone + noise * hiss) * Mathf.Exp(-decay * t) * volume;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // Little cubes fly up from a captured piece, fall and shrink, then remove themselves.
        IEnumerator Burst(Vector3 origin, Color colour)
        {
            BurstsStarted++;
            const int count = 20;
            const float life = 0.8f, size = 0.32f;
            var parent = new GameObject("Debris").transform;
            var bits = new Transform[count];
            var velocity = new Vector3[count];
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", colour);
            for (int i = 0; i < count; i++)
            {
                var bit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(bit.GetComponent<Collider>());
                bit.GetComponent<Renderer>().sharedMaterial = highlight;
                bit.GetComponent<Renderer>().SetPropertyBlock(block);
                bit.transform.SetParent(parent, false);
                bit.transform.position = origin + Vector3.up * (FieldY + 0.6f);
                bits[i] = bit.transform;
                velocity[i] = Random.insideUnitSphere * 3.5f + Vector3.up * 4f;
            }
            for (float t = 0; t < life; t += Time.deltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    velocity[i] += Vector3.down * 14f * Time.deltaTime;
                    bits[i].position += velocity[i] * Time.deltaTime;
                    bits[i].localScale = Vector3.one * size * (1 - t / life);
                }
                yield return null;
            }
            Destroy(parent.gameObject);
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

            if (game.LastMove is Move last)
            {
                Ring(last.From, new Color(1f, 0.6f, 0.1f), 0.1f);
                Ring(last.To, new Color(1f, 0.6f, 0.1f), 0.1f);
            }
            var b = game.Board;
            if (MoveGen.InCheck(b, b.WhiteToMove))
                Ring(System.Array.IndexOf(b.Squares, b.WhiteToMove ? 'K' : 'k'), new Color(0.9f, 0.1f, 0.1f), 0.16f);

            if (selected < 0) return;
            Slab(SquarePos(selected), 1f, 1f, Color.yellow);
            foreach (int to in game.TargetsFrom(selected))
                if (b.Squares[to] == Board.Empty) Slab(SquarePos(to), 0.35f, 0.35f, Color.green);
                else Slab(SquarePos(to), 1f, 1f, Color.red);
        }

        // A frame round a square: four thin slabs, `thickness` in world units.
        void Ring(int sq, Color colour, float thickness)
        {
            var c = SquarePos(sq);
            float edge = (squareSize - thickness) / 2, bar = (squareSize - 2 * thickness) / squareSize;
            Slab(c + new Vector3(0, 0, edge), 1f, thickness / squareSize, colour);
            Slab(c - new Vector3(0, 0, edge), 1f, thickness / squareSize, colour);
            Slab(c + new Vector3(edge, 0, 0), thickness / squareSize, bar, colour);
            Slab(c - new Vector3(edge, 0, 0), thickness / squareSize, bar, colour);
        }

        // Flat slab centred on `centre`; its width (x) and depth (z) are shares of a square.
        void Slab(Vector3 centre, float width, float depth, Color colour)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(m.GetComponent<Collider>());
            m.transform.SetParent(marksRoot, false);
            m.transform.position = centre + Vector3.up * (FieldY + 0.01f);
            m.transform.localScale = new Vector3(squareSize * width, 0.02f, squareSize * depth);
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

            // Everything shown while a game is on; hidden behind the start menu.
            hud = new GameObject("HUD", typeof(RectTransform));
            hud.transform.SetParent(canvasGo.transform, false);
            var hudRect = hud.GetComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero; hudRect.anchorMax = Vector2.one; hudRect.offsetMin = hudRect.offsetMax = Vector2.zero;

            var top = new Vector2(0.5f, 1);
            statusText = UiKit.Label(hud.transform, font, "", 32, top, new Vector2(0, -20), new Vector2(900, 50), TextAnchor.UpperCenter);
            moveListText = UiKit.Label(hud.transform, font, "", 24, new Vector2(0, 1), new Vector2(20, -90), new Vector2(230, 420), TextAnchor.UpperLeft);

            var corner = new Vector2(1, 1);
            UiKit.Button(hud.transform, font, "New Game", corner, new Vector2(-110, -40), new Vector2(180, 50), NewGame);
            undoButton = UiKit.Button(hud.transform, font, "Undo", corner, new Vector2(-110, -100), new Vector2(180, 50), Undo);
            levelButton = UiKit.Button(hud.transform, font, "Level", corner, new Vector2(-110, -160), new Vector2(180, 50), CycleLevel);
            UiKit.Button(hud.transform, font, "Flip View", corner, new Vector2(-110, -220), new Vector2(180, 50), () => RotateView(180));
            UiKit.Button(hud.transform, font, "Menu", corner, new Vector2(-110, -280), new Vector2(180, 50), OpenMenu);
            ShowLevel();

            picker = new GameObject("Promotion", typeof(Image));
            picker.transform.SetParent(canvasGo.transform, false);
            picker.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            UiKit.Place(picker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 90));
            string[] names = { "Queen", "Rook", "Bishop", "Knight" };
            for (int i = 0; i < 4; i++)
            {
                char piece = "qrbn"[i];
                UiKit.Button(picker.transform, font, names[i], new Vector2(0.5f, 0.5f), new Vector2(-255 + i * 170, 0), new Vector2(160, 60), () => Promote(piece));
            }

            menu = new StartMenu(canvasGo.transform, font, Levels.Select(l => l.name).ToArray(), level, humanWhite, StartGame, Play, ExitGame);

            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        void ShowLevel() => levelButton.GetComponentInChildren<Text>().text = "Level: " + LevelName;
    }
}
