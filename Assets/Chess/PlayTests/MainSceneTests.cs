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

        [UnitySetUp]
        public IEnumerator Load()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            view = Object.FindFirstObjectByType<ChessView>();
            Assert.IsNotNull(view, "ChessView missing from Main.unity");
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

        // The mouse maps a screen point to a square through the camera; check every square from the start view.
        [UnityTest]
        public IEnumerator ScreenPoints_MapToTheirSquares_FromTheDefaultCamera()
        {
            yield return null;
            AssertEverySquareIsClickable();
        }

        [UnityTest]
        public IEnumerator RotatingTheView_KeepsEverySquareClickable_AndSwingsTheCamera()
        {
            Assert.Less(Camera.main.transform.position.z, 0, "White's side to begin with");
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
