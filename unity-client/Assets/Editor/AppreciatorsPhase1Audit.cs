#if UNITY_EDITOR
using System;
using System.Linq;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using AppreciatorsTcg.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.EditorTools
{
    public static class AppreciatorsPhase1Audit
    {
        public static void RunAll()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
                SceneBootstrapper bootstrapper = UnityEngine.Object.FindObjectOfType<SceneBootstrapper>();
                Require(bootstrapper != null, "Main scene must contain SceneBootstrapper.");

                AuditCards();
                AuditDeck();
                AuditSceneUi();

                Debug.Log("APPRECIATORS_AUDIT_PASS Phase 1 Unity audit completed.");
            }
            catch (Exception exception)
            {
                Debug.LogError("APPRECIATORS_AUDIT_FAIL\n" + exception);
                throw;
            }
        }

        private static void AuditCards()
        {
            Require(CardCatalog.AllCards.Count == 29, "Approved trait set must contain 29 cards.");
            Require(CardCatalog.AllCards.Count(card => card.type == GameConstants.Original) == 17, "Approved trait set must contain 17 ORIGINALS.");
            Require(CardCatalog.AllCards.Count(card => card.type == GameConstants.Companion) == 5, "Approved trait set must contain 5 COMPANIONS.");
            Require(CardCatalog.AllCards.Count(card => card.type == GameConstants.Item) == 7, "Approved trait set must contain 7 ITEMS.");
            Require(CardCatalog.AllCards.Count(card => card.type == GameConstants.Event) == 0, "Phase 1 should not invent EVENT cards outside the approved list.");
            Require(CardCatalog.AllCards.Select(card => card.id).Distinct().Count() == CardCatalog.AllCards.Count, "Card ids must be unique.");
            Require(CardCatalog.AllCards.All(card => card.artKey == card.id), "Every card artKey must match its stable id.");
            Require(CardCatalog.AllCards.All(card => card.EffectiveArtPath() == $"Art/Cards/{card.id}"), "Every card must have a Unity Resources art path.");
            Require(CardCatalog.AllCards.All(card => !($"{card.id} {card.name} {card.artPath}").ToLowerInvariant().Contains("dreaded ape")), "Dreaded Ape assets must not be referenced.");
        }

        private static void AuditDeck()
        {
            Require(PlayerDeckService.ValidateDeck(CardCatalog.StarterDeckIds()), "Starter deck must be valid.");
        }

        private static void AuditSceneUi()
        {
            Canvas canvas = UIFactory.CreateCanvas("AuditCanvas");
            UIFactory.CreateText(canvas.transform, "Appreciators TCG", 28, TextAnchor.MiddleCenter, UIFactory.TextColor, FontStyle.Bold);
            UIFactory.CreateText(canvas.transform, "Be Original", 24, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.CreateButton(canvas.transform, "Guest Login", delegate { }, UIFactory.Green);
            Canvas.ForceUpdateCanvases();
            Require(HasVisibleText("Appreciators TCG"), "Login screen title should render.");
            Require(HasVisibleText("Be Original"), "Brand slogan should render.");
            Require(HasVisibleText("Guest Login"), "Guest login button should render.");
            UnityEngine.Object.DestroyImmediate(canvas.gameObject);
        }

        private static bool HasVisibleText(string expected)
        {
            Text[] texts = UnityEngine.Object.FindObjectsOfType<Text>();
            return texts.Any(text => text != null && text.gameObject.activeInHierarchy && text.text.Contains(expected));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }
    }
}
#endif
