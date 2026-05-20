using System;
using CreateWandPatch.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
namespace CreateWandPatch.Rendering
{
	/// <summary>世界空间蓝图叠层；纹理在 Update 中同步，绘制在 Main.Draw 结束后单独一批。</summary>
	public static class CreateWandWorldPreview
	{
		private static Texture2D _texture;
		private static int _cacheKey = int.MinValue;
		private static SpriteBatch _overlaySpriteBatch;

		public static void InvalidateCache()
		{
			_cacheKey = int.MinValue;
			_texture?.Dispose();
			_texture = null;
		}

		/// <summary>
		/// 在 <see cref="Main.Update"/> 中调用（SpriteBatch 未 Begin 时）。
		/// 不得在 <see cref="Draw"/> 内创建/SetData 纹理，否则会破坏批状态并在后续 MapHeadRenderer 等处触发「Begin cannot be called again until End…」。
		/// </summary>
		public static void SyncPreviewTexture()
		{
			if (Main.dedServ || Main.gameMenu)
				return;
			var gd = Main.graphics?.GraphicsDevice;
			if (gd == null)
				return;

			var player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead || player.noBuilding ||
			    player.inventory[player.selectedItem].type != CreateWandIds.ItemType)
			{
				if (_texture != null)
					InvalidateCache();
				return;
			}

			BuildingData data = TryGetPreviewData();
			if (data == null)
			{
				if (_texture != null)
					InvalidateCache();
				return;
			}

			int key = HashSelection();
			if (_texture != null && key == _cacheKey)
				return;

			_texture?.Dispose();
			_cacheKey = key;
			int len = data.Width * data.Height;
			var arr = new Color[len];
			for (int i = 0; i < len; i++)
				arr[i] = data.TileInfos[i].ToColor();
			_texture = new Texture2D(gd, data.Width, data.Height, false, SurfaceFormat.Color);
			_texture.SetData(arr);
		}

		/// <summary>
		/// 与原版地表相同 <see cref="Main.Transform"/>，在 <see cref="Main.Draw"/> 末尾绘制。
		/// 使用独立 <see cref="SpriteBatch"/>，避免与 <see cref="Main.spriteBatch"/> 状态纠缠；同一 GPU 上仍须在主批已 End 后调用。
		/// </summary>
		public static void DrawAfterFrameWithOwnSpriteBatch()
		{
			if (Main.inFancyUI || Main.onlyDrawFancyUI)
				return;

			var gd = Main.instance?.GraphicsDevice;
			if (gd == null)
				return;

			SpriteBatchSafety.TryUnwindMainSpriteBatch();

			if (_overlaySpriteBatch == null)
				_overlaySpriteBatch = new SpriteBatch(gd);

			try
			{
				_overlaySpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
					DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
				try
				{
					Draw(_overlaySpriteBatch);
				}
				finally
				{
					_overlaySpriteBatch.End();
				}
			}
			catch
			{
				try
				{
					_overlaySpriteBatch.End();
				}
				catch
				{
					// ignored
				}
			}
		}

		public static void Draw(SpriteBatch spriteBatch)
		{
			if (Main.dedServ || Main.gameMenu)
				return;
			var player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead || player.noBuilding)
				return;
			if (player.inventory[player.selectedItem].type != CreateWandIds.ItemType)
				return;

			BuildingData data = TryGetPreviewData();
			if (data == null || _texture == null || HashSelection() != _cacheKey)
				return;

			Point anchor = Main.MouseWorld.ToTileCoordinates();
			int ox = anchor.X - data.Width / 2;
			int oy = anchor.Y - data.Height / 2;
			// Main.Transform 批内仍需减去摄像机偏移，否则预览画在屏幕外
			var pos = new Vector2(ox * 16f - Main.screenPosition.X, oy * 16f - Main.screenPosition.Y);
			spriteBatch.Draw(_texture, pos, null, Color.White * 0.45f, 0f, Vector2.Zero, new Vector2(16f, 16f),
				SpriteEffects.None, 0f);
		}

		private static BuildingData TryGetPreviewData()
		{
			if (CreateWandSelectionState.SelectedKind == BlueprintKind.DataMap)
			{
				CreateWandPngLibrary.EnsureReload();
				if (CreateWandPngLibrary.Entries.Count == 0 ||
				    CreateWandSelectionState.SelectedDatamapIndex < 0 ||
				    CreateWandSelectionState.SelectedDatamapIndex >= CreateWandPngLibrary.Entries.Count)
					return null;
				return CreateWandPngLibrary.Entries[CreateWandSelectionState.SelectedDatamapIndex].Data;
			}

			return CreatePresetBuildingData(CreateWandSelectionState.SelectedPreset);
		}

		private static BuildingData CreatePresetBuildingData(PlacePreset preset)
		{
			switch (preset)
			{
				case PlacePreset.Stone3x3:
					return FillPresetGrid(3, 3, BuildingData.TileSort.Block);
				case PlacePreset.WoodPlatform5:
					return FillPresetGrid(5, 1, BuildingData.TileSort.Platform);
				case PlacePreset.Dirt2x2:
					return FillPresetGrid(2, 2, BuildingData.TileSort.Block);
				default:
					return null;
			}
		}

		private static BuildingData FillPresetGrid(int w, int h, BuildingData.TileSort sort)
		{
			var ti = new BuildingData.TileInfo { Sort = sort, HasWall = false, Flip = false };
			var c = ti.ToColor();
			var colors = new Color[w * h];
			for (int i = 0; i < colors.Length; i++)
				colors[i] = c;
			return BuildingData.FromColorGrid(w, h, colors);
		}

		private static int HashSelection()
		{
			unchecked
			{
				int h = (int)CreateWandSelectionState.SelectedKind;
				h = h * 397 ^ (int)CreateWandSelectionState.SelectedPreset;
				return h * 397 ^ CreateWandSelectionState.SelectedDatamapIndex;
			}
		}
	}
}
