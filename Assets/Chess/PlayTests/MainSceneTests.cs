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
