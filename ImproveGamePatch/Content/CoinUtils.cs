using System;
using Terraria;
using Terraria.ID;

namespace ImproveGamePatch.Content
{
    public static class CoinUtils
    {
        // Copper=1, Silver=100, Gold=10000, Platinum=1000000
        private const ulong CopperValue = 1;
        private const ulong SilverValue = 100;
        private const ulong GoldValue = 100 * 100;
        private const ulong PlatinumValue = 100 * 100 * 100;

        public static bool IsCoin(int type) => type >= ItemID.CopperCoin && type <= ItemID.PlatinumCoin;

        public static ulong CalculateCoinValue(int type, int stack)
        {
            return type switch
            {
                ItemID.CopperCoin => (ulong)stack * CopperValue,
                ItemID.SilverCoin => (ulong)stack * SilverValue,
                ItemID.GoldCoin => (ulong)stack * GoldValue,
                ItemID.PlatinumCoin => (ulong)stack * PlatinumValue,
                _ => 0,
            };
        }

        /// <summary>
        /// Move coins from inventory slots 50-53 to the player's piggy bank.
        /// </summary>
        public static void MoveCoinsToBank(Player player)
        {
            // First pass: try to combine with existing bank coins
            for (int invSlot = 50; invSlot < 54; invSlot++)
            {
                var coin = player.inventory[invSlot];
                if (!IsCoin(coin.type) || coin.stack <= 0)
                    continue;

                // Try stack onto existing coins in bank
                for (int bankSlot = 0; bankSlot < player.bank.item.Length; bankSlot++)
                {
                    var bankCoin = player.bank.item[bankSlot];
                    if (bankCoin.type != coin.type || bankCoin.stack >= bankCoin.maxStack)
                        continue;

                    int space = bankCoin.maxStack - bankCoin.stack;
                    int transfer = Math.Min(coin.stack, space);
                    if (transfer <= 0) continue;

                    bankCoin.stack += transfer;
                    coin.stack -= transfer;
                    if (coin.stack <= 0)
                    {
                        coin.TurnToAir();
                        break;
                    }
                }

                if (coin.stack <= 0 || coin.IsAir)
                    continue;

                // Try find empty bank slot
                for (int bankSlot = 0; bankSlot < player.bank.item.Length; bankSlot++)
                {
                    if (player.bank.item[bankSlot].IsAir)
                    {
                        player.bank.item[bankSlot] = coin.Clone();
                        coin.TurnToAir();
                        break;
                    }
                }
            }
        }
    }
}
