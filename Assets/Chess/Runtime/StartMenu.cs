using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Chess.View
{
    // Small helpers for the code-built UI.
    public static class UiKit
    {
        public static void Place(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static Text Label(Transform parent, Font font, string text, int fontSize, Vector2 anchor, Vector2 pos, Vector2 size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(text, typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.color = Color.white; t.alignment = align; t.text = text;
            go.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f); // stays readable over grass and sky
            Place(go, anchor, pos, size);
            return t;
        }

        public static Button Button(Transform parent, Font font, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, int fontSize = 26)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Normal;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            Place(go, anchor, pos, size);

            var t = Label(go.transform, font, label, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Destroy(t.GetComponent<Outline>());
            return button;
        }

        public static readonly Color Normal = new Color(0.15f, 0.15f, 0.2f, 0.9f), Chosen = new Color(0.2f, 0.5f, 0.25f, 0.95f);

        static void Destroy(UnityEngine.Object o) => UnityEngine.Object.Destroy(o);
    }

    // The title screen (Start Game / Exit Game) and the setup screen (difficulty, colour) shown over the board.
    public class StartMenu
    {
        readonly GameObject title, setup;
        readonly Button[] levelButtons = new Button[3], colourButtons = new Button[2]; // colour: [0] White, [1] Black

        public int Level { get; private set; }
        public bool PlaysWhite { get; private set; }
        public bool IsOpen => title.activeSelf || setup.activeSelf;
        public bool OnSetup => setup.activeSelf;

        public StartMenu(Transform canvas, Font font, string[] levelNames, int level, bool playsWhite, UnityAction startGame, UnityAction play, UnityAction exit)
        {
            title = Screen(canvas, "Title screen");
            UiKit.Label(title.transform, font, "FOREST CHESS", 92, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1000, 120));
            UiKit.Button(title.transform, font, "Start Game", new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(340, 80), startGame, 36);
            UiKit.Button(title.transform, font, "Exit Game", new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(340, 80), exit, 36);

            setup = Screen(canvas, "Setup screen");
            var middle = new Vector2(0.5f, 0.5f);
            UiKit.Label(setup.transform, font, "NEW GAME", 64, middle, new Vector2(0, 250), new Vector2(800, 90));
            UiKit.Label(setup.transform, font, "Difficulty", 34, middle, new Vector2(0, 150), new Vector2(400, 50));
            for (int i = 0; i < levelButtons.Length; i++)
            {
                int index = i;
                levelButtons[i] = UiKit.Button(setup.transform, font, levelNames[i], middle, new Vector2(-200 + i * 200, 85), new Vector2(180, 64), () => PickLevel(index), 30);
            }
            UiKit.Label(setup.transform, font, "Play as", 34, middle, new Vector2(0, 5), new Vector2(400, 50));
            colourButtons[0] = UiKit.Button(setup.transform, font, "White", middle, new Vector2(-110, -60), new Vector2(200, 64), () => PickColour(true), 30);
            colourButtons[1] = UiKit.Button(setup.transform, font, "Black", middle, new Vector2(110, -60), new Vector2(200, 64), () => PickColour(false), 30);
            UiKit.Button(setup.transform, font, "Play", middle, new Vector2(0, -170), new Vector2(300, 76), play, 36);
            UiKit.Button(setup.transform, font, "Back", middle, new Vector2(0, -255), new Vector2(200, 56), ShowTitle, 28);

            PickLevel(level);
            PickColour(playsWhite);
            Hide();
        }

        public void PickLevel(int level)
        {
            Level = level;
            for (int i = 0; i < levelButtons.Length; i++) levelButtons[i].image.color = i == level ? UiKit.Chosen : UiKit.Normal;
        }

        public void PickColour(bool white)
        {
            PlaysWhite = white;
            colourButtons[0].image.color = white ? UiKit.Chosen : UiKit.Normal;
            colourButtons[1].image.color = white ? UiKit.Normal : UiKit.Chosen;
        }

        public void ShowTitle() { title.SetActive(true); setup.SetActive(false); }
        public void ShowSetup() { title.SetActive(false); setup.SetActive(true); }
        public void Hide() { title.SetActive(false); setup.SetActive(false); }

        // A full-screen dimmed layer; the board stays visible, turning slowly, behind it.
        static GameObject Screen(Transform canvas, string name)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(canvas, false);
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }
    }
}
