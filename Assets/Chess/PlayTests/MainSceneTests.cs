using System.Collections;
using System.IO;
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
