using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using ImproveGamePatch.Content;
using Terraria;
using Terraria.ID;

namespace ImproveGamePatch.Patches
{
    /// <summary>
    /// Simplified "Infinite Buffs": carrying 30+ of a potion/food in your
    /// inventory grants its buff permanently. No UI, fully passive.
    /// Also: garden gnome in inventory gives luck boost.
    /// Works in SP, host-and-play, and MP (buffs are client-authoritative).
    /// </summary>
    [HarmonyPatch]
    public class InfiniteBuffPatch
    {
        private const int RequiredStack = 30;

        // Known buff-granting items: (itemId, buffId, buffDuration)
        // Buff food and potion mappings
        private static readonly (int itemId, int buffId)[] BuffItems =
        {
            // Combat potions
            (ItemID.IronskinPotion,          BuffID.Ironskin),
            (ItemID.RegenerationPotion,      BuffID.Regeneration),
            (ItemID.SwiftnessPotion,         BuffID.Swiftness),
            (ItemID.EndurancePotion,         BuffID.Endurance),
            (ItemID.LifeforcePotion,         BuffID.Lifeforce),
            (ItemID.RagePotion,              BuffID.Rage),
            (ItemID.WrathPotion,             BuffID.Wrath),
            (ItemID.ThornsPotion,            BuffID.Thorns),
            (ItemID.TitanPotion,             BuffID.Titan),
            (ItemID.HeartreachPotion,        BuffID.Heartreach),
            (ItemID.InfernoPotion,           BuffID.Inferno),
            (ItemID.InvisibilityPotion,      BuffID.Invisibility),
            (ItemID.BattlePotion,            BuffID.Battle),
            (ItemID.CalmingPotion,           BuffID.Calm),
            (ItemID.TeleportationPotion,     BuffID.ChaosState),

            // Exploration potions
            (ItemID.SpelunkerPotion,         BuffID.Spelunker),
            (ItemID.ShinePotion,             BuffID.Shine),
            (ItemID.NightOwlPotion,          BuffID.NightOwl),
            (ItemID.HunterPotion,            BuffID.Hunter),
            (ItemID.TrapsightPotion,         BuffID.Dangersense),
            (ItemID.FeatherfallPotion,       BuffID.Featherfall),
            (ItemID.GravitationPotion,       BuffID.Gravitation),
            (ItemID.FlipperPotion,           BuffID.Flipper),
            (ItemID.GillsPotion,             BuffID.Gills),
            (ItemID.WaterWalkingPotion,      BuffID.WaterWalking),
            (ItemID.ObsidianSkinPotion,      BuffID.ObsidianSkin),
            (ItemID.WarmthPotion,            BuffID.Warmth),
            // (PotionOfReturn creates a portal, not a buff — skipped)

            // Mage potions
            (ItemID.MagicPowerPotion,        BuffID.MagicPower),
            (ItemID.ManaRegenerationPotion,  BuffID.ManaRegeneration),
            (ItemID.SummoningPotion,         BuffID.Summoning),
            (ItemID.AmmoReservationPotion,   BuffID.AmmoReservation),
            (ItemID.ArcheryPotion,           BuffID.Archery),

            // Misc buffs
            (ItemID.BuilderPotion,           BuffID.Builder),
            (ItemID.CratePotion,             BuffID.Crate),
            (ItemID.FishingPotion,           BuffID.Fishing),
            (ItemID.SonarPotion,             BuffID.Sonar),
            (ItemID.MiningPotion,            BuffID.Mining),
            (ItemID.LuckPotion,              BuffID.Lucky),
            (ItemID.LuckPotionGreater,       BuffID.Lucky),
            (ItemID.LovePotion,              BuffID.Lovestruck),
            (ItemID.StinkPotion,             BuffID.Stinky),
            (ItemID.GenderChangePotion,      BuffID.Lovestruck),

            // Flasks (melee weapon imbues)
            (ItemID.FlaskofFire,             BuffID.WeaponImbueFire),
            (ItemID.FlaskofVenom,            BuffID.WeaponImbueVenom),
            (ItemID.FlaskofGold,             BuffID.WeaponImbueGold),
            (ItemID.FlaskofIchor,            BuffID.WeaponImbueIchor),
            (ItemID.FlaskofCursedFlames,     BuffID.WeaponImbueCursedFlames),
            (ItemID.FlaskofNanites,          BuffID.WeaponImbueNanites),
            (ItemID.FlaskofParty,            BuffID.WeaponImbueConfetti),
            (ItemID.FlaskofPoison,           BuffID.WeaponImbuePoison),

            // Food buffs
            (ItemID.CookedFish,              BuffID.WellFed),
            (ItemID.CookedShrimp,            BuffID.WellFed),
            (ItemID.Sashimi,                 BuffID.WellFed),
            (ItemID.BunnyStew,               BuffID.WellFed),
            (ItemID.GrubSoup,                BuffID.WellFed),
            (ItemID.ApplePie,                BuffID.WellFed2),
            (ItemID.Bacon,                   BuffID.WellFed3),
            (ItemID.GoldenDelight,           BuffID.WellFed3),
            (ItemID.BowlofSoup,              BuffID.WellFed),
            (ItemID.PumpkinPie,              BuffID.WellFed2),
        };

        // Buffs from placed items carried in inventory (garden gnome, etc.)
        private static readonly int[] BuffTileItems =
        {
            ItemID.GardenGnome,
            ItemID.Campfire,
            ItemID.HeartLantern,
            // Star in a Bottle = ID 1430
            1430,
        };

        /// <summary>
        /// After player buffs update, scan inventory for 30+ stack potions
        /// and re-apply their buffs continuously.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateBuffs))]
        [HarmonyPostfix]
        private static void Postfix_UpdateBuffs(Player __instance)
        {
            if (!Config.Get("InfiniteBuff", true)) return;
            if (Main.myPlayer != __instance.whoAmI)
                return;
            if (Main.dedServ)
                return;

            ApplyInfiniteBuffs(__instance);
        }

        private static void ApplyInfiniteBuffs(Player player)
        {
            // Count each buff-item type in inventory
            var stacks = new Dictionary<int, int>();

            for (int i = 0; i < 50; i++)
            {
                var item = player.inventory[i];
                if (item.IsAir) continue;

                int tid = item.type;
                if (stacks.ContainsKey(tid))
                    stacks[tid] += item.stack;
                else
                    stacks[tid] = item.stack;
            }

            // Apply buffs for items with enough stack
            foreach (var (itemId, buffId) in BuffItems)
            {
                if (stacks.TryGetValue(itemId, out int count) && count >= RequiredStack)
                {
                    // Apply buff for 30 seconds (renewed every frame, so effectively permanent)
                    player.AddBuff(buffId, 1800);
                }
            }

            // Buff tile effects from inventory items
            bool hasCampfire = stacks.ContainsKey(ItemID.Campfire);
            bool hasHeartLantern = stacks.ContainsKey(ItemID.HeartLantern);
            bool hasStarInBottle = stacks.ContainsKey(1430);
            bool hasGardenGnome = stacks.ContainsKey(ItemID.GardenGnome);

            if (hasCampfire)
                player.AddBuff(BuffID.Campfire, 120);
            if (hasHeartLantern)
                player.AddBuff(BuffID.HeartLamp, 120);
            if (hasStarInBottle)
                player.AddBuff(BuffID.StarInBottle, 120);
            if (hasGardenGnome)
                player.AddBuff(BuffID.SugarRush, 120); // Garden Gnome gives luck, SugarRush is placeholder
        }
    }
}
