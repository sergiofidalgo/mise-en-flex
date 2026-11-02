using Game.Data;
using Game.Gameplay.CloseUp;
using Game.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Builds the close-up pieces: technique prefabs (each with its own overhead camera) and the
    /// per-player HUD panel the close-up is shown in.
    /// </summary>
    public static class GreyboxCloseUp
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/CloseUp";

        // Hob close-up camera: straight down on the pan from above the counter top, framing a
        // ~0.75 m square (the 0.44 m pan, its handle and the burner ring).
        private const float CameraHeightAboveTop = 1f;
        private const float CameraHalfView = 0.38f;
        private const float CameraNear = 0.05f;
        private const float CameraFar = 3f;
        private const float MarkerSize = 0.04f;
        private static readonly Color MarkerColor = Color.white;

        // HUD: panel border width in pixels; the panel draws above other UI.
        private const float BorderWidth = 6f;
        private const int HudSortingOrder = 10;

        public static GameObject BuildHobTechnique(float counterHeight)
        {
            var root = new GameObject("PF_CloseUp_Hob");

            var cameraObject = new GameObject("CloseUpCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, counterHeight + CameraHeightAboveTop, 0f);
            cameraObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // look straight down, top of image = north
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraHalfView;
            camera.nearClipPlane = CameraNear;
            camera.farClipPlane = CameraFar;
            camera.enabled = false;

            var marker = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Sphere, "SelectionMarker", root.transform, GreyboxAssets.EnsureMaterial("Marker", MarkerColor), keepCollider: false);
            marker.transform.localScale = Vector3.one * MarkerSize;
            marker.SetActive(false);

            var technique = root.AddComponent<HobCloseUpTechnique>();
            var so = new SerializedObject(technique);
            so.FindProperty("_camera").objectReferenceValue = camera;
            so.FindProperty("_selectionMarker").objectReferenceValue = marker.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            GreyboxAssets.EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/PF_CloseUp_Hob.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Adds an overlay canvas with the close-up panel to a player prefab being built.</summary>
        public static void AddHud(GameObject player, PlayerCloseUp closeUp, TuningConfig tuning, CoopSettings settings)
        {
            var hud = new GameObject("CloseUpHud", typeof(RectTransform));
            hud.transform.SetParent(player.transform, false);
            var canvas = hud.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = HudSortingOrder;
            var scaler = hud.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // the view works in screen pixels

            // Panel: anchored at the bottom-left corner so anchoredPosition is in screen pixels.
            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(hud.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var border = panel.gameObject.AddComponent<Image>();

            var view = new GameObject("View", typeof(RectTransform)).GetComponent<RectTransform>();
            view.SetParent(panel, false);
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.offsetMin = Vector2.one * BorderWidth;
            view.offsetMax = -Vector2.one * BorderWidth;
            var image = view.gameObject.AddComponent<RawImage>();

            var panelView = hud.AddComponent<CloseUpPanelView>();
            var so = new SerializedObject(panelView);
            so.FindProperty("_closeUp").objectReferenceValue = closeUp;
            so.FindProperty("_tuning").objectReferenceValue = tuning;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_image").objectReferenceValue = image;
            so.FindProperty("_border").objectReferenceValue = border;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
