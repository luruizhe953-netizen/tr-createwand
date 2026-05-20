using CreateWandPatch.Content;
using CreateWandPatch.Gameplay;
using CreateWandPatch.Infrastructure;
using CreateWandPatch.Rendering;
using CreateWandPatch.UI;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.UI;

namespace CreateWandPatch.Patches
{
	[HarmonyPatch(typeof(Player), nameof(Player.PlaceThing))]
	public static class Player_PlaceThing_CreateWandPrefix
	{
		[HarmonyPrefix]
		public static bool Prefix(Player __instance, bool doPlacementAction, ref Player.ItemCheckContext context)
		{
			if (Main.netMode == 2)
				return true;
			if (__instance.whoAmI != Main.myPlayer || Main.dedServ)
				return true;
			if (__instance.inventory[__instance.selectedItem].type != CreateWandIds.ItemType)
				return true;

			PlaceThingPrelude.Run(__instance, ref context);
			if (__instance.noBuilding)
				return false;

			if (!Main.inFancyUI)
			{
				if (PlayerInput.Triggers.JustPressed.MouseRight)
				{
					CreateWandPngLibrary.EnsureReload();
					IngameFancyUI.OpenUIState(new CreateWandModePanel());
					return false;
				}

				if (PlayerInput.Triggers.JustPressed.MouseLeft &&
				    TryAdvanceBoxExport(__instance))
					return false;

				if (PlayerInput.Triggers.JustPressed.MouseLeft)
					CreateWandSinglePlayerService.TryPlaceFromLocalPlayer(__instance);
			}

			return false;
		}

		private static bool TryAdvanceBoxExport(Player player)
		{
			switch (CreateWandBoxExportState.Phase)
			{
				case CreateWandBoxExportPhase.WaitingFirstCorner:
					CreateWandBoxExportState.FirstCorner = Main.MouseWorld.ToTileCoordinates();
					CreateWandBoxExportState.Phase = CreateWandBoxExportPhase.WaitingSecondCorner;
					Terraria.CombatText.NewText(player.getRect(), Color.LightYellow,
						"[魔杖] 已记录第一角，再左键第二角完成导出", false, false);
					SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
					return true;
				case CreateWandBoxExportPhase.WaitingSecondCorner:
				{
					Point b = Main.MouseWorld.ToTileCoordinates();
					Point a = CreateWandBoxExportState.FirstCorner;
					CreateWandBoxExportState.Phase = CreateWandBoxExportPhase.Inactive;
					if (CreateWandWorldToPngExport.TryExportRectToNewPng(a.X, a.Y, b.X, b.Y, out string path, out string err))
					{
						CreateWandPngLibrary.Reload();
						CreateWandWorldPreview.InvalidateCache();
						Terraria.CombatText.NewText(player.getRect(), Color.LightGreen,
							"[魔杖] 已保存 " + System.IO.Path.GetFileName(path), false, false);
						SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
					}
					else
					{
						Terraria.CombatText.NewText(player.getRect(), Color.OrangeRed, err ?? "[魔杖] 导出失败", false, false);
						SoundEngine.PlaySound(12, -1, -1, 1, 0.6f, 0f);
					}

					return true;
				}
				default:
					return false;
			}
		}
	}
}
