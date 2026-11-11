using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Editor
{
    /// <summary>
    /// The main menu's fonts and sprites, made ready on demand: TextMeshPro font assets from the TTFs
    /// in Art/Fonts (made once, then reused), and the PNGs in Art/UI/Menu imported as crisp UI sprites.
    /// </summary>
    public static class MenuArt
    {
        public const string FontFolder = "Assets/_Project/Art/Fonts";
        public const string SpriteFolder = "Assets/_Project/Art/UI/Menu";

        // Sampled at 64 px with 6 px of SDF padding: sharp from 13 px labels to 40 px titles.
        private const int SamplingSize = 64;
        private const int Padding = 6;
        private const int AtlasSize = 1024;

        /// <summary>
        /// The TextMeshPro font asset for <paramref name="fileName"/> (e.g. "Inter-Bold"), created the
        /// first time. Static, holding printable ASCII, Latin-1 and the menu's symbols: a static atlas
        /// doesn't change as the game runs, so it never dirties the asset in git. Anything outside it
        /// falls back to TextMeshPro's default font.
        /// </summary>
        public static TMP_FontAsset Font(string fileName)
        {
            var path = $"{FontFolder}/{fileName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{fileName}.ttf");
            if (source == null)
            {
                Debug.LogError($"[MenuArt] Missing {FontFolder}/{fileName}.ttf.");
                return null;
            }

            var asset = TMP_FontAsset.CreateFontAsset(source, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = $"{fileName} SDF";
            if (!asset.TryAddCharacters(Characters(), out var missing) && !string.IsNullOrEmpty(missing))
                Debug.Log($"[MenuArt] {fileName} has no glyph for: {missing}");
            asset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(asset, path);
            for (var i = 0; i < asset.atlasTextures.Length; i++)
            {
                asset.atlasTextures[i].name = $"{fileName} Atlas {i}";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
            }
            asset.material.name = $"{fileName} Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MenuArt] Made {path}.");
            return asset;
        }

        public const string DashboardFolder = "Assets/_Project/Art/UI/Dashboard";
        private const string HudStylePath = "Assets/_Project/Data/Settings/HudStyle.asset";

        /// <summary>
        /// Gives the HUD style the mockups' fonts and forkcast's art, so the Review dashboard picks
        /// them up without rebuilding the kitchen scene. Batch: -executeMethod Game.Editor.MenuArt.ApplyUiArt
        /// </summary>
        [MenuItem("Tools/Game/Apply UI Art")]
        public static void ApplyUiArt()
        {
            var style = AssetDatabase.LoadAssetAtPath<Game.Presentation.Hud.HudStyle>(HudStylePath);
            var so = new SerializedObject(style);
            so.FindProperty("_medium").objectReferenceValue = Font("Inter-Medium");
            so.FindProperty("_semiBold").objectReferenceValue = Font("Inter-SemiBold");
            so.FindProperty("_bold").objectReferenceValue = Font("Inter-Bold");
            so.FindProperty("_extraBold").objectReferenceValue = Font("Inter-ExtraBold");
            so.FindProperty("_serifItalic").objectReferenceValue = Font("Lora-Italic");
            so.FindProperty("_serif").objectReferenceValue = Font("Lora-Regular");
            so.FindProperty("_forkcastLogo").objectReferenceValue = Sprite("TEX_Dash_Logo", DashboardFolder);
            so.FindProperty("_searchIcon").objectReferenceValue = Sprite("TEX_Dash_IconSearch", DashboardFolder);
            so.FindProperty("_starFull").objectReferenceValue = Sprite("TEX_Dash_StarFull", DashboardFolder);
            so.FindProperty("_starHalf").objectReferenceValue = Sprite("TEX_Dash_StarHalf", DashboardFolder);
            so.FindProperty("_starEmpty").objectReferenceValue = Sprite("TEX_Dash_StarEmpty", DashboardFolder);
            so.FindProperty("_warningIcon").objectReferenceValue = Sprite("TEX_Dash_Warning", DashboardFolder);
            so.FindProperty("_replyMark").objectReferenceValue = Sprite("TEX_Dash_ReplyMark", DashboardFolder);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            Debug.Log("[MenuArt] Applied the UI fonts and forkcast art to the HUD style.");
        }

        /// <summary>A sprite from Art/UI/Menu (or <paramref name="folder"/>), imported uncompressed with no mipmaps so UI stays sharp.</summary>
        public static Sprite Sprite(string fileName, string folder = SpriteFolder)
        {
            var path = $"{folder}/{fileName}.png";
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                var changed = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.mipmapEnabled
                    || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || !importer.alphaIsTransparency
                    || importer.wrapMode != TextureWrapMode.Clamp
                    // The PNGs are drawn at twice the size they're shown, for Retina screens.
                    || !Mathf.Approximately(importer.spritePixelsPerUnit, 200f);
                if (changed)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.spritePixelsPerUnit = 200f;
                    importer.SaveAndReimport();
                }
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogError($"[MenuArt] Missing sprite {path}.");
            return sprite;
        }

        private static string Characters()
        {
            var text = new StringBuilder();
            for (var c = 0x20; c <= 0x7e; c++)
                text.Append((char)c);
            for (var c = 0xa0; c <= 0xff; c++)
                text.Append((char)c);
            text.Append("€·–—‘’“”…•−‹›×←→↑↓");
            return new string(text.ToString().Distinct().ToArray());
        }
    }
}
