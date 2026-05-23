using System;
using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Rendering
{
    /// <summary>
    /// World-space blueprint overlay placeholder.
    /// Full Unity rendering implementation requires runtime testing on device.
    /// Uses Terraria's Texture2D API (IL2CPP-compatible subset) for preview generation.
    /// </summary>
    public static class CreateWandWorldPreview
    {
        private static int _cacheKey = int.MinValue;
        private static bool _previewVisible;

        public static void InvalidateCache()
        {
            _cacheKey = int.MinValue;
            _previewVisible = false;
        }

        public static void SyncPreviewTexture()
        {
            if (Main.dedServ || Main.gameMenu) return;

            var player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead || player.noBuilding ||
                player.inventory[player.selectedItem].type != CreateWandIds.ItemType)
            {
                _previewVisible = false;
                return;
            }

            BuildingData data = TryGetPreviewData();
            if (data == null)
            {
                _previewVisible = false;
                return;
            }

            _previewVisible = true;
        }

        /// <summary>
        /// Draw preview. Uses Terraria's SpriteBatch (available in IL2CPP build).
        /// The Main.spriteBatch exists on Android for Chromebook/keyboard support.
        /// Full implementation pending runtime testing.
        /// </summary>
        public static void DrawAfterFrameWithOwnSpriteBatch()
        {
            if (!_previewVisible) return;
            if (Main.inFancyUI) return;
            // TODO: Runtime Unity rendering implementation
            // Will use Main.spriteBatch or Unity GL after device testing
        }

        private static BuildingData TryGetPreviewData()
        {
            CreateWandPngLibrary.EnsureReload();
            if (CreateWandSelectionState.SelectedKind == BlueprintKind.BuiltinPreset)
                return null;

            if (CreateWandSelectionState.SelectedKind == BlueprintKind.DataMap &&
                CreateWandPngLibrary.Entries.Count > 0 &&
                CreateWandSelectionState.SelectedDatamapIndex >= 0 &&
                CreateWandSelectionState.SelectedDatamapIndex < CreateWandPngLibrary.Entries.Count)
            {
                return CreateWandPngLibrary.Entries[CreateWandSelectionState.SelectedDatamapIndex].Data;
            }

            return null;
        }
    }
}
