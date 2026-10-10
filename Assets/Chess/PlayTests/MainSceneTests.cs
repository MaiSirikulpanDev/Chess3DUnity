using System.Collections;
using System.IO;
using System.Linq;
using Chess.Core;
using Chess.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chess.PlayTests
{
    // Drives the real scene: loads Main.unity, clicks squares through ChessView.Click, checks state and saves screenshots.
    [Timeout(60000)]
    public class MainSceneTests
    {
        ChessView view;


        static IEnumerator Shot(string name)
        {
            yield return null;
            Directory.CreateDirectory("Logs/shots");
            // The camera only: the overlay UI is checked through ChessView.StatusText instead.
            var cam = Camera.main;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes("Logs/shots/" + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
        }

        static int Sq(string s) => Board.Sq(s);

        [TearDown]
        public void ForgetTheChoices()
        {
            PlayerPrefs.DeleteKey(ChessView.LevelKey);
            PlayerPrefs.DeleteKey(ChessView.WhiteKey);
        }

        [UnitySetUp]
        public IEnumerator Load()
        {
            PlayerPrefs.DeleteKey(ChessView.LevelKey); // each test starts from the defaults: Normal, playing White
            PlayerPrefs.DeleteKey(ChessView.WhiteKey);
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            view = Object.FindFirstObjectByType<ChessView>();
            Assert.IsNotNull(view, "ChessView missing from Main.unity");
            view.Play(); // the scene opens on the start menu; most tests want a game in progress
        }

        [UnityTest]
        public IEnumerator StartsWithAllThirtyTwoPieces_AndWhitesTurn()
        {
            Assert.AreEqual("Your move (White)", view.StatusText);
            Assert.AreEqual(32, view.transform.childCount);
            yield return Shot("1_start");
        }

        [UnityTest]
        public IEnumerator HumanMoves_ThenAiReplies()
        {
            view.Click(Sq("e2"));
            yield return Shot("2_selected");
            view.Click(Sq("e4"));
            Assert.AreEqual("AI is thinking...", view.StatusText);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual("Your move (White)", view.StatusText);
            yield return Shot("3_after_ai_reply");
        }

        [UnityTest]
        public IEnumerator ClickingAnEmptyOrIllegalSquare_DoesNothing()
        {
            view.Click(Sq("e4"));
            view.Click(Sq("e5"));
            yield return null;
            Assert.AreEqual("Your move (White)", view.StatusText);
            Assert.AreEqual(32, view.transform.childCount);
        }

        [UnityTest]
        public IEnumerator Promotion_AsksForAPiece_ThenPlacesIt()
        {
            view.StartFrom("7k/P7/8/8/8/8/8/K7 w - - 0 1");
            yield return null;
            view.Click(Sq("a7"));
            view.Click(Sq("a8"));
            Assert.IsTrue(view.PromotionPending);
            Assert.IsNotNull(view.transform.Find("P a7"), "the pawn waits on a7 until a piece is chosen");
            yield return Shot("4_promotion_picker");

            view.Promote('n');
            Assert.IsFalse(view.PromotionPending);
            yield return null;
            Assert.IsNotNull(view.transform.Find("N a8"));
            yield return Shot("5_promoted_knight");
        }

        [UnityTest]
        public IEnumerator Promotion_ClickingTheBoard_CancelsTheChoice()
        {
            view.StartFrom("7k/P7/8/8/8/8/8/K7 w - - 0 1");
            yield return null;
            view.Click(Sq("a7"));
            view.Click(Sq("a8"));
            view.Click(Sq("e4"));
            yield return null;
            Assert.IsFalse(view.PromotionPending);
            Assert.AreEqual("Your move (White)", view.StatusText);
            Assert.IsNotNull(view.transform.Find("P a7"));
        }

        [UnityTest]
        public IEnumerator Undo_TakesBackTheMoveAndTheAiReply()
        {
            view.Click(Sq("e2"));
            view.Click(Sq("e4"));
            yield return new WaitForSeconds(1.5f);
            yield return Shot("6_before_undo");

            view.Undo();
            yield return new WaitForSeconds(1f); // let the pieces slide back
            Assert.AreEqual("Your move (White)", view.StatusText);
            Assert.AreEqual(32, view.transform.childCount);
            var pawn = view.transform.Find("P e2");
            Assert.AreEqual(0.75f, pawn.position.x, 0.01f, "the e-pawn is back on e2");
            Assert.AreEqual(-3.75f, pawn.position.z, 0.01f);
            yield return Shot("7_after_undo");
        }

        [UnityTest]
        public IEnumerator LevelButton_CyclesEasyNormalHard_AndTheAiStillAnswers()
        {
            Assert.AreEqual("Normal", view.LevelName);
            view.CycleLevel();
            Assert.AreEqual("Hard", view.LevelName);
            view.CycleLevel();
            Assert.AreEqual("Easy", view.LevelName);

            view.Click(Sq("e2"));
            view.Click(Sq("e4"));
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual("Your move (White)", view.StatusText, "the Easy AI replied");
            yield return Shot("8_level_easy");
        }

        [UnityTest]
        public IEnumerator RotatingTheView_KeepsEverySquareClickable_AndSwingsTheCamera()
        {
            // The mouse maps a screen point to a square through the camera: check every square before and after turning it.
            Assert.Less(Camera.main.transform.position.z, 0, "White's side to begin with");
            AssertEverySquareIsClickable();
            view.RotateView(180);
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(Camera.main.transform.position.z, 10f, "now looking from Black's side");
            AssertEverySquareIsClickable();
            yield return Shot("9_view_from_black");

            view.RotateView(-90);
            yield return new WaitForSeconds(1.5f);
            AssertEverySquareIsClickable();
            yield return Shot("10_view_from_the_side");
        }

        [UnityTest]
        public IEnumerator Tilting_StopsAtParallelWithTheBoard_NeverSeeingUnderIt()
        {
            view.TiltView(-500); // far more than is possible
            yield return new WaitForSeconds(1.5f);
            var cam = Camera.main.transform;
            Assert.AreEqual(0.068f, cam.position.y, 0.01f, "eye level with the playing surface, not below it");
            Assert.AreEqual(0f, cam.forward.y, 0.01f, "looking along the board");
            yield return Shot("15_view_parallel_to_the_board");

            view.TiltView(500);
            yield return new WaitForSeconds(1.5f);
            var fromAim = cam.position - new Vector3(0, 0.068f, 0);
            Assert.AreEqual(85f, Mathf.Asin(fromAim.y / fromAim.magnitude) * Mathf.Rad2Deg, 1.5f, "almost straight down, not flipped over");
            yield return Shot("16_view_from_above");
        }

        [UnityTest]
        public IEnumerator Tilting_KeepsEverySquareClickable_AtAnAngle()
        {
            view.TiltView(-30); // from about 49 degrees to about 19
            yield return new WaitForSeconds(1.5f);
            AssertEverySquareIsClickable();
            view.RotateView(90);
            yield return new WaitForSeconds(1.5f);
            AssertEverySquareIsClickable();
        }

        // ---- start menu ----
        static IEnumerator FreshScene(System.Action<ChessView> got)
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            got(Object.FindFirstObjectByType<ChessView>());
        }

        [UnityTest]
        public IEnumerator TheGameOpensOnTheStartMenu_WhichBlocksTheBoard()
        {
            ChessView v = null;
            yield return FreshScene(x => v = x);
            Assert.IsTrue(v.MenuOpen);
            Assert.IsFalse(v.OnSetupScreen, "the title screen comes first");
            v.Click(Sq("e2"));
            Assert.AreEqual(0, v.MarkCount, "clicks on the board do nothing behind the menu");
            yield return Shot("20_start_menu");
        }

        [UnityTest]
        public IEnumerator StartGame_OpensSetup_AndPlayUsesTheChosenLevelAndColour()
        {
            ChessView v = null;
            yield return FreshScene(x => v = x);
            v.StartGame();
            Assert.IsTrue(v.OnSetupScreen);
            v.PickLevel(2);
            v.PickColour(false);
            yield return Shot("21_setup_screen");

            v.Play();
            Assert.IsFalse(v.MenuOpen);
            Assert.AreEqual("Hard", v.LevelName);
            Assert.IsFalse(v.HumanIsWhite);
            yield return new WaitForSeconds(3f); // White (the AI) opens
            Assert.AreEqual("Your move (Black)", v.StatusText);
            StringAssert.StartsWith("1. ", v.MoveListText);
            Assert.Greater(Camera.main.transform.position.z, 5f, "the camera looks from Black's side");
            yield return Shot("22_playing_black");
        }

        [UnityTest]
        public IEnumerator ExitGame_AsksTheApplicationToQuit()
        {
            bool quit = false;
            view.quitAction = () => quit = true;
            view.ExitGame();
            Assert.IsTrue(quit);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuButton_GoesBackToTheTitle_AndTheChoicesAreRemembered()
        {
            view.OpenMenu();
            Assert.IsTrue(view.MenuOpen);
            view.StartGame();
            view.PickLevel(0);
            view.PickColour(false);
            view.Play();

            ChessView v = null;
            yield return FreshScene(x => v = x);
            v.StartGame();
            Assert.AreEqual(0, v.MenuLevel, "Easy was chosen last time");
            Assert.IsFalse(v.MenuPlaysWhite);
        }

        // ---- the forest around the board ----
        [Test]
        public void TheBoard_SitsOnAStump_WhoseTopIsRightUnderIt()
        {
            var stump = GameObject.Find("Stump");
            Assert.IsNotNull(stump, "the stump the board stands on");
            var bounds = stump.GetComponentInChildren<Renderer>().bounds;
            Assert.AreEqual(-0.277f, bounds.max.y, 0.03f, "its top meets the underside of the board");
            Assert.Greater(bounds.size.x, 14.6f, "wide enough to carry the board");
            Assert.Greater(bounds.size.z, 14.6f);
            Assert.Less(Mathf.Abs(bounds.center.x), 8f, "centred under the board");
        }

        [Test]
        public void TheForest_StaysOutsideTheCameraOrbit_AndFillsTheDistance()
        {
            var forest = GameObject.Find("Forest");
            Assert.IsNotNull(forest);
            var trees = forest.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("PT_Pine_Tree") || t.name.StartsWith("PT_Fruit_Tree")).ToList();
            Assert.Greater(trees.Count, 40, "a forest, not a few trees");
            Assert.IsTrue(trees.All(t => new Vector2(t.position.x, t.position.z).magnitude > 80f), "far enough that even a wide canopy stays clear of the camera orbit and the board");
            Assert.IsTrue(RenderSettings.fog, "mist thins the far trees");
        }

        [Test]
        public void TheStump_IsNotSquashed_ItKeepsItsModelsProportions()
        {
            var bounds = GameObject.Find("Stump").GetComponentInChildren<Renderer>().bounds;
            float ratio = bounds.size.y / bounds.size.x; // the model is about 0.74 tall for its width
            Assert.That(ratio, Is.InRange(0.6f, 0.95f), "a natural stump, not a flattened plinth");
        }

        [Test]
        public void TheTrees_AreAsBigAsTheStumpTheyWouldHaveGrownFrom()
        {
            var stump = GameObject.Find("Stump").GetComponentInChildren<Renderer>().bounds;
            var tree = GameObject.Find("Forest").GetComponentsInChildren<Transform>()
                .First(t => t.name.StartsWith("PT_Pine_Tree_03_green")).GetComponentInChildren<Renderer>().bounds;
            Assert.Greater(tree.size.y, stump.size.y * 4, "pines tower over the stump");
            Assert.Greater(tree.size.x, stump.size.x, "and their branches spread wider than its base");
        }

        [Test]
        public void TheGround_IsBelowTheStump()
        {
            var ground = GameObject.Find("Ground");
            Assert.IsNotNull(ground);
            Assert.Less(ground.transform.position.y, GameObject.Find("Stump").GetComponentInChildren<Renderer>().bounds.center.y);
        }

        void AssertEverySquareIsClickable()
        {
            for (int sq = 0; sq < 64; sq++)
            {
                var onScreen = Camera.main.WorldToScreenPoint(view.SquarePos(sq) + Vector3.up * 0.068f);
                Assert.AreEqual(sq, view.SquareAt(onScreen), Board.Name(sq));
            }
        }

        [UnityTest]
        public IEnumerator Sounds_AreSilentAtStart_ThenTapForMoves_AndThudForCaptures()
        {
            Assert.AreEqual("", view.LastSound, "setting up the board is silent");
            Assert.Greater(Peak(view.MoveClip), 0.1f);
            Assert.Greater(Peak(view.CaptureClip), 0.1f);
            Assert.Greater(view.CaptureClip.length, view.MoveClip.length, "a capture rings longer than a plain move");

            view.Click(Sq("e2"));
            view.Click(Sq("e4"));
            Assert.AreEqual("move", view.LastSound);
            Assert.AreEqual(0, view.BurstsStarted, "nothing was taken");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capturing_PlaysTheThud_AndThrowsDebris()
        {
            view.StartFrom("7k/8/8/3p4/4P3/8/8/K7 w - - 0 1");
            yield return null;
            view.Click(Sq("e4"));
            view.Click(Sq("d5"));
            Assert.AreEqual("capture", view.LastSound);
            Assert.AreEqual(1, view.BurstsStarted);
            yield return new WaitForSeconds(0.25f);
            Assert.IsNotNull(GameObject.Find("Debris"), "the pieces of the captured pawn are in the air");
            yield return Shot("11_capture_burst");
            yield return new WaitForSeconds(1f);
            Assert.IsNull(GameObject.Find("Debris"), "and they clean themselves up");
        }

        [UnityTest]
        public IEnumerator PromotionWithoutCapture_IsNotACapture()
        {
            view.StartFrom("7k/P7/8/8/8/8/8/K7 w - - 0 1");
            yield return null;
            view.Click(Sq("a7"));
            view.Click(Sq("a8"));
            view.Promote('q');
            Assert.AreEqual("move", view.LastSound);
            Assert.AreEqual(0, view.BurstsStarted);
        }

        static float Peak(AudioClip clip)
        {
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            return data.Max(Mathf.Abs);
        }

        [UnityTest]
        public IEnumerator Level_IsRememberedWhenTheSceneIsReloaded()
        {
            view.CycleLevel();
            Assert.AreEqual("Hard", view.LevelName);

            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            view = Object.FindFirstObjectByType<ChessView>();
            Assert.AreEqual("Hard", view.LevelName, "the saved level came back");
        }

        [UnityTest]
        public IEnumerator LastMove_IsRingedOnBothSquares_AndClearedByNewGame()
        {
            Assert.AreEqual(0, view.MarkCount);
            view.Click(Sq("e2"));
            Assert.AreEqual(1 + 2, view.MarkCount, "selection + two targets");
            view.Click(Sq("e4"));
            Assert.AreEqual(8, view.MarkCount, "two rings of four slabs: e2 and e4");
            yield return new WaitForSeconds(1.5f); // the AI answers: its move is now the last one
            Assert.AreEqual(8, view.MarkCount);
            yield return Shot("12_last_move_rings");

            view.NewGame();
            Assert.AreEqual(0, view.MarkCount);
        }

        [UnityTest]
        public IEnumerator KingInCheck_GetsARedRing_AndTheStatusSaysSo()
        {
            view.StartFrom("7k/8/8/8/8/8/R7/K7 w - - 0 1");
            yield return null;
            view.Click(Sq("a2"));
            view.Click(Sq("a8"));
            Assert.AreEqual("Check! AI is thinking...", view.StatusText);
            Assert.AreEqual(8 + 4, view.MarkCount, "last-move rings plus the ring round the black king");
            yield return Shot("13_check_ring");
        }

        [UnityTest]
        public IEnumerator MoveList_ShowsTheGameInNotation()
        {
            Assert.AreEqual("", view.MoveListText);
            view.Click(Sq("e2"));
            view.Click(Sq("e4"));
            Assert.AreEqual("1. e4", view.MoveListText);
            yield return new WaitForSeconds(1.5f);
            StringAssert.StartsWith("1. e4 ", view.MoveListText);
            yield return Shot("14_move_list");

            view.Undo();
            Assert.AreEqual("", view.MoveListText);
        }

        [UnityTest]
        public IEnumerator Undo_WithNothingToTakeBack_ChangesNothing()
        {
            view.Undo();
            yield return null;
            Assert.AreEqual("Your move (White)", view.StatusText);
            Assert.AreEqual(32, view.transform.childCount);
        }

        [UnityTest]
        public IEnumerator NewGame_RestoresTheStartingPosition()
        {
            view.Click(Sq("e2")); view.Click(Sq("e4"));
            yield return new WaitForSeconds(1.5f);
            view.NewGame();
            yield return null;
            Assert.AreEqual("Your move (White)", view.StatusText);
            Assert.AreEqual(32, view.transform.childCount);
        }
    }
}
