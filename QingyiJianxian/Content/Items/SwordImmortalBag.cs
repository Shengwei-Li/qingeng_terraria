using QingyiJianxian.Content.Items.Armor;
using QingyiJianxian.Content.NPCs.SwordImmortal;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>
	/// 宝藏袋（青衣剑仙）：专家和大师模式下，剑仙赠的不是一件件东西，而是每人一个袋子（联机各拿各的）。
	/// 比普通模式给得多一些。第一次问剑的清月剑和一壶酒仍由剑仙亲手抛出，不在袋里。
	/// </summary>
	public class SwordImmortalBag : ModItem
	{
		public override void SetStaticDefaults()
		{
			ItemID.Sets.BossBag[Type] = true;
			Item.ResearchUnlockCount = 3;
		}

		public override void SetDefaults()
		{
			Item.width = 52;
			Item.height = 52;
			Item.maxStack = Item.CommonMaxStack;
			Item.consumable = true;
			Item.rare = ItemRarityID.Purple;
			Item.expert = true;
		}

		public override bool CanRightClick() => true;

		public override void ModifyItemLoot(ItemLoot itemLoot)
		{
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<CloudweaveSilk>(), 1, 12, 15));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<AzureCloudDraught>(), 1, 6, 9));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<Clearmoon>(), 3));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<InkwakeTassel>(), 2));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<LitanyOfSwordflight>(), 4));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<SwordImmortalMask>(), 7));
			itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<SwordImmortal>()));
		}
	}
}
