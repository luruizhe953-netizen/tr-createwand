using System;
using System.Reflection;
using HarmonyLib;
using ImproveGamePatch.Content;
using ImproveGamePatch.UI;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;

namespace ImproveGamePatch.Patches
{
    /// <summary>
    /// Press H to teleport home using any teleport item in inventory
    /// (Magic Mirror, Recall Potion, Cell Phone, etc.).
    /// Items are NOT consumed — only checked for ownership.
    /// </summary>
    [HarmonyPatch]
    public class HomeTeleportPatch
    {
        private static bool _wasKeyDown;

        private static readonly int[] TeleportItems =
        {
            ItemID.PotionOfReturn,
            ItemID.RecallPotion,
            ItemID.MagicMirror,
            ItemID.CellPhone,
            ItemID.IceMirror,
            ItemID.Shellphone,
            ItemID.ShellphoneOcean,
            ItemID.ShellphoneHell,
            ItemID.ShellphoneSpawn,
        };

        private static bool HasTeleportItem(Player player, out int itemType, out bool isPotionOfReturn)
        {
            isPotionOfReturn = false;
            itemType = 0;

            foreach (int tid in TeleportItems)
            {
                for (int i = 0; i < 50; i++)
                {
                    if (player.inventory[i].type == tid && player.inventory[i].stack > 0)
                    {
                        itemType = tid;
                        isPotionOfReturn = (tid == ItemID.PotionOfReturn);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Call Player.Spawn(PlayerSpawnContext.RecallFromItem) via reflection
        /// to avoid compile-time issues with the enum type in some Terraria builds.
        /// </summary>
        private static void SpawnAtHome(Player player)
        {
            player.RemoveAllGrapplingHooks();
            bool savedImmune = player.immune;
            int savedImmuneTime = player.immuneTime;

            try
            {
                var method = typeof(Player).GetMethod("Spawn");
                if (method != null)
                {
                    var paramType = method.GetParameters()[0].ParameterType;
                    // RecallFromItem = 7 in vanilla 1.4.4+
                    var contextVal = Enum.ToObject(paramType, 7);
                    method.Invoke(player, new[] { contextVal });
                }
            }
            catch
            {
                // Fallback: just restore immunity
            }

            player.immune = savedImmune;
            player.immuneTime = savedImmuneTime;
        }

        [HarmonyPatch(typeof(Main), "Update")]
        [HarmonyPostfix]
        private static void Postfix_MainUpdate()
        {
            if (!Config.Get("HomeTeleport", true)) return;
            var player = Main.LocalPlayer;
            if (player == null || player.dead)
            {
                _wasKeyDown = false;
                return;
            }

            var ks = ControlPanel.Kb;
            bool keyDown = ks.IsKeyDown(Keys.H);

            if (keyDown && !_wasKeyDown)
            {
                if (HasTeleportItem(player, out int itemType, out bool isPotionOfReturn))
                {
                    if (isPotionOfReturn)
                    {
                        player.DoPotionOfReturnTeleportationAndSetTheComebackPoint();
                    }
                    else
                    {
                        SpawnAtHome(player);
                    }

                    Main.NewText("Teleported home (H)", 100, 255, 100);
                }
                else
                {
                    Main.NewText("No teleport item in inventory (need Magic Mirror, Recall Potion, etc.)", 255, 200, 0);
                }
            }

            _wasKeyDown = keyDown;
        }
    }
}
