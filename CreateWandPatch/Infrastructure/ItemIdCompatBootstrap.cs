using System;
using System.Reflection;
using System.Text;
using CreateWandPatch.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Prefixes;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace CreateWandPatch.Infrastructure
{
	/// <summary>
	/// 原版最后物品为 6146 时 <see cref="ItemID.Count"/> 多为 6147；使用 6147 作创造魔杖会导致
	/// Lang / Sets 数组不含该下标或物品名为空（弹字判空失败）。
	/// 在 Harmony 注册之后执行：将 Count 提升到 ItemType+1，扩展 Lang 与
	/// <see cref="ItemID.Sets"/>、<see cref="PrefixLegacy.ItemSets"/>、<see cref="AmmoID.Sets"/>，
	/// 并扩展 <see cref="TextureAssets.Item"/> / <see cref="TextureAssets.ItemFlame"/>、<see cref="Item.staff"/>/<see cref="Item.claw"/>、
	/// <see cref="Main.itemAnimations"/>（均按旧 Count 静态分配；否则物品格/动画绘制越界会在 SpriteBatch Begin·End 之间抛错），最后注册 ContentSamples。
	/// </summary>
	public static class ItemIdCompatBootstrap
	{
		public static void Apply(StringBuilder log)
		{
			try
			{
				FieldInfo countField = typeof(ItemID).GetField("Count", BindingFlags.Public | BindingFlags.Static);
				if (countField == null)
				{
					log.AppendLine("  ItemIdCompat: ItemID.Count 未找到，跳过。");
					return;
				}

				short countBefore = (short)countField.GetValue(null);
				int neededCount = CreateWandIds.ItemType + 1;

				if (countBefore < neededCount)
				{
					if (!TrySetStaticInitOnlyField(countField, (short)neededCount))
					{
						log.AppendLine("  ItemIdCompat: 无法写入 ItemID.Count（只读），跳过。");
						return;
					}
					log.AppendLine("  ItemIdCompat: ItemID.Count " + countBefore + " -> " + neededCount);
				}
				else
					log.AppendLine("  ItemIdCompat: ItemID.Count=" + countBefore + " 已满足。");

				short countAfter = (short)countField.GetValue(null);

				ExpandTextureAssetsItemArrays(log, countAfter);
				ExpandMainItemAnimations(log, countBefore, countAfter);
				ExpandItemStaffAndClaw(log, countBefore, countAfter);
				ExpandLangCaches(log, countBefore, countAfter);
				ExpandSetFactoryArrays(log, countBefore, countAfter, typeof(ItemID).GetNestedType("Sets", BindingFlags.Public), "ItemID.Sets");
				ExpandSetFactoryArrays(log, countBefore, countAfter, typeof(PrefixLegacy).GetNestedType("ItemSets", BindingFlags.Public), "PrefixLegacy.ItemSets");
				ExpandSetFactoryArrays(log, countBefore, countAfter, typeof(AmmoID).GetNestedType("Sets", BindingFlags.Public), "AmmoID.Sets");
				EnsureContentSample(log);
				log.AppendLine("  ItemIdCompat: 完成。");
			}
			catch (Exception ex)
			{
				log.AppendLine("  ItemIdCompat: 异常 — " + ex);
			}
		}

		/// <summary>
		/// <see cref="Main.itemAnimations"/>：与 Count 等长；<see cref="ItemSlot"/> / <see cref="Item"/> 绘制用 <c>itemAnimations[type]</c>，缺槽会在 UI 绘制链越界。
		/// </summary>
		private static void ExpandMainItemAnimations(StringBuilder log, short countBefore, short countAfter)
		{
			int oldLen = countBefore;
			int newLen = countAfter;
			if (oldLen >= newLen)
				return;

			FieldInfo f = typeof(Main).GetField("itemAnimations", BindingFlags.Public | BindingFlags.Static);
			if (f == null)
				return;

			Array arr = f.GetValue(null) as Array;
			if (arr == null || arr.Rank != 1 || arr.Length != oldLen)
				return;

			Type elemType = arr.GetType().GetElementType();
			Array n = Array.CreateInstance(elemType, newLen);
			Array.Copy(arr, n, oldLen);
			int tpl = CreateWandIds.FallbackTemplateItem;
			n.SetValue(tpl >= 0 && tpl < oldLen ? arr.GetValue(tpl) : null, oldLen);
			f.SetValue(null, n);
			log.AppendLine("  ItemIdCompat: Main.itemAnimations " + oldLen + " -> " + newLen + "（新槽 <- 模板 " + tpl + "）");
		}

		/// <summary>
		/// <see cref="Item.staff"/> / <see cref="Item.claw"/> 与 <see cref="ItemID.Count"/> 等长；不扩则
		/// <c>Item.staff[item.type]</c>（如 <see cref="PlayerDrawLayers"/>）在手持 6147 时越界。
		/// </summary>
		private static void ExpandItemStaffAndClaw(StringBuilder log, short countBefore, short countAfter)
		{
			int oldLen = countBefore;
			int newLen = countAfter;
			if (oldLen >= newLen)
				return;

			Type itemType = typeof(Item);
			foreach (string fieldName in new[] { "staff", "claw" })
			{
				FieldInfo f = itemType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
				if (f == null)
					continue;

				Array arr = f.GetValue(null) as Array;
				if (arr == null || arr.Rank != 1 || arr.Length != oldLen)
					continue;

				Array n = Array.CreateInstance(typeof(bool), newLen);
				Array.Copy(arr, n, oldLen);
				int tpl = CreateWandIds.FallbackTemplateItem;
				object templateBool = tpl >= 0 && tpl < oldLen ? arr.GetValue(tpl) : arr.GetValue(oldLen - 1);
				n.SetValue(templateBool, oldLen);
				f.SetValue(null, n);
				log.AppendLine("  ItemIdCompat: Item." + fieldName + " " + oldLen + " -> " + newLen + "（新槽 <- 模板 " + tpl + "）");
			}
		}

		/// <summary>
		/// <see cref="TextureAssets.Item"/> 等：静态长度按旧 <see cref="ItemID.Count"/>；仅改 Count 不会拉长数组。
		/// </summary>
		private static void ExpandTextureAssetsItemArrays(StringBuilder log, int newLen)
		{
			Type textureAssets = typeof(TextureAssets);
			int tpl = CreateWandIds.FallbackTemplateItem;

			foreach (string fieldName in new[] { "Item", "ItemFlame" })
			{
				FieldInfo f = textureAssets.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
				if (f == null)
					continue;

				Array arr = f.GetValue(null) as Array;
				if (arr == null || arr.Rank != 1)
					continue;

				if (arr.Length >= newLen)
					continue;

				int curLen = arr.Length;
				Type elemType = arr.GetType().GetElementType();
				Array n = Array.CreateInstance(elemType, newLen);
				Array.Copy(arr, n, curLen);
				object templateAsset = tpl >= 0 && tpl < curLen ? arr.GetValue(tpl) : arr.GetValue(curLen - 1);
				n.SetValue(templateAsset, newLen - 1);
				f.SetValue(null, n);
				log.AppendLine("  ItemIdCompat: TextureAssets." + fieldName + " " + curLen + " -> " + newLen
					+ "（槽 " + (newLen - 1) + " <- 模板 " + tpl + "）");
			}
		}

		private static void ExpandLangCaches(StringBuilder log, short countBefore, short countAfter)
		{
			int newLen = countAfter;
			PatchOrResizeLangArray("_itemNameCache", newLen, (arr, idx) =>
			{
				LocalizedText text = Language.GetText("ItemName.CreateWand");
				arr.SetValue(text, idx);
			});
			PatchOrResizeLangArray("_itemTooltipCache", newLen, (arr, idx) =>
			{
				ItemTooltip tip = ItemTooltip.FromLanguageKey("ItemTooltip.CreateWand");
				arr.SetValue(tip, idx);
			});
			log.AppendLine("  ItemIdCompat: Lang 缓存已对齐长度 " + newLen + "（原 Count=" + countBefore + "）");
		}

		private delegate void WriteSlot(Array arr, int index);

		private static void PatchOrResizeLangArray(string fieldName, int newLen, WriteSlot writeSlot)
		{
			FieldInfo f = typeof(Lang).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
			if (f == null)
				return;

			Array existing = f.GetValue(null) as Array;
			if (existing == null || existing.Rank != 1)
				return;

			int slot = CreateWandIds.ItemType;
			if (fieldName == "_itemNameCache" && existing.Length > slot)
			{
				if (existing.GetValue(slot) != null)
					return;
			}

			if (existing.Length >= newLen)
			{
				if (slot < existing.Length)
					writeSlot(existing, slot);
				return;
			}

			Type elem = existing.GetType().GetElementType();
			Array resized = Array.CreateInstance(elem, newLen);
			Array.Copy(existing, resized, existing.Length);
			if (slot < newLen)
				writeSlot(resized, slot);
			f.SetValue(null, resized);
		}

		/// <summary>
		/// 凡用 <see cref="SetFactory"/> 且长度为扩容前 <see cref="ItemID.Count"/> 的一维数组，与 ItemID 同步延长一格；
		/// 新槽位复制末项（与末件原版物品行为接近）。含 <see cref="PrefixLegacy.ItemSets"/>，否则
		/// <see cref="Item.GetRollablePrefixes"/> 会在拾取弹窗等路径越界。
		/// </summary>
		private static void ExpandSetFactoryArrays(StringBuilder log, short countBefore, short countAfter, Type holder, string label)
		{
			if (holder == null)
				return;

			int oldLen = countBefore;
			int newLen = countAfter;
			if (oldLen >= newLen)
				return;

			int expanded = 0;
			foreach (FieldInfo field in holder.GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				if (field.FieldType == typeof(SetFactory))
					continue;

				object val = field.GetValue(null);
				Array arr = val as Array;
				if (arr == null || arr.Rank != 1 || arr.Length != oldLen)
					continue;

				Array n = Array.CreateInstance(arr.GetType().GetElementType(), newLen);
				Array.Copy(arr, n, oldLen);
				object template = arr.GetValue(oldLen - 1);
				n.SetValue(template, oldLen);
				field.SetValue(null, n);
				expanded++;
			}

			log.AppendLine("  ItemIdCompat: " + label + " 扩容字段数=" + expanded + " (" + oldLen + "->" + newLen + ")");
		}

		private static void EnsureContentSample(StringBuilder log)
		{
			if (ContentSamples.ItemsByType.ContainsKey(CreateWandIds.ItemType))
				return;

			Item sample = new Item();
			sample.SetDefaults(CreateWandIds.ItemType, null);
			ContentSamples.ItemsByType[CreateWandIds.ItemType] = sample;
			log.AppendLine("  ItemIdCompat: ContentSamples.ItemsByType[" + CreateWandIds.ItemType + "] 已添加。");
		}

		private static bool TrySetStaticInitOnlyField(FieldInfo field, object value)
		{
			try
			{
				field.SetValue(null, value);
				return true;
			}
			catch (FieldAccessException) { }
			catch (TargetException) { }

			try
			{
				FieldInfo attrField = typeof(FieldInfo).GetField(
					"m_fieldAttributes",
					BindingFlags.Instance | BindingFlags.NonPublic);
				if (attrField != null)
				{
					var attrs = (FieldAttributes)attrField.GetValue(field);
					attrField.SetValue(field, attrs & ~FieldAttributes.InitOnly);
					field.SetValue(null, value);
					return true;
				}
			}
			catch { }

			return false;
		}
	}
}
