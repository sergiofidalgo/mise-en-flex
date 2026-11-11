using System;
using System.IO;
using Game.Presentation.Hud;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Screenshots of forkcast's review cards (option B, 2026-11-11) at 1920×1080 without Play mode:
    /// an unanswered card chosen, an answered one, a walk-out and a critic, with the guests'
    /// portraits. Menu: Tools/Game/Capture Review Cards. Batch mode (editor closed):
    /// Unity -batchmode -projectPath . -executeMethod Game.Editor.ReviewCardsCapture.CaptureFromCommandLine
    /// </summary>
    public static class ReviewCardsCapture
    {
        private const string Folder = "Screenshots/Dashboard";

        [MenuItem("Tools/Game/Capture Review Cards")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Capture();
            EditorUtility.RevealInFinder(Path.GetFullPath(Folder));
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                Capture();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static ReviewRecord[] Samples() => new[]
        {
            new ReviewRecord { Guest = "Pavla K.", Day = 3, Look = 2, Persona = (int)Persona.Local, Visit = 2, Stars = 2f,
                Topic = (int)ReviewTopic.SlowService, Line = "Took forever.", Order = "steak, rare", Paid = 20f },
            new ReviewRecord { Guest = "Marco L.", Day = 3, Look = 1, Persona = (int)Persona.Regular, Stars = 3f,
                Topic = (int)ReviewTopic.Cold, Line = "Steak came out cold. Shame, the crust was spot on.", Order = "steak, medium", Paid = 24f,
                Reply = (int)ReviewReply.Apologise },
            new ReviewRecord { Guest = "Inés R.", Day = 3, Look = 3, Persona = (int)Persona.Tourist, WalkedOut = true, Penalty = 12f,
                Topic = (int)ReviewTopic.WalkedOut, Line = "Gave up and left hungry.", Order = "steak, medium" },
            new ReviewRecord { Guest = "Jonas W.", Day = 2, Look = 4, Persona = (int)Persona.Critic, Stars = 3.5f,
                Topic = (int)ReviewTopic.Praise, Line = "A confident sear, let down by a rushed rest. Worth a second look, if the kitchen can keep its nerve on a busy night.",
                Order = "steak, medium rare", Paid = 28f, Tip = 3f },
        };

        private static void Capture()
        {
            Directory.CreateDirectory(Folder);
            var language = Game.Data.Locale.Language;
            try
            {
                Game.Data.Locale.Language = "en";
                EditorSceneManager.OpenScene(GreyboxKitchenBuilder.ScenePath, OpenSceneMode.Single);
                var dashboard = Object.FindFirstObjectByType<ReviewDashboard>(FindObjectsInactive.Include);
                if (dashboard == null)
                    throw new InvalidOperationException("No ReviewDashboard in the kitchen: rebuild it (Tools/Game/Build Greybox Kitchen).");
                Shoot(dashboard, "review_cards.png");
                Game.Data.Locale.Language = "es";
                Shoot(dashboard, "review_cards_es.png");
                Debug.Log($"[ReviewCardsCapture] Saved to {Folder}.");
            }
            finally
            {
                Game.Data.Locale.Language = language;
            }
        }

        private static void Shoot(ReviewDashboard dashboard, string file)
        {
            Canvas canvas = null;
            try
            {
                canvas = dashboard.DrawCardsForCapture(Samples(), today: 3, selected: 0);
                UiCapture.Save(Camera.main, canvas, Path.Combine(Folder, file));
            }
            finally
            {
                if (canvas != null)
                    Object.DestroyImmediate(canvas.gameObject);
            }
        }
    }
}
