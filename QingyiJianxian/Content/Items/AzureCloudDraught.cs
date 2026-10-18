using QingyiJianxian.Content.Buffs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>
	/// 青云药酒 / Azure Cloud Draught: 剑仙的疗伤药酒。回 200 血，喝下微醺 5 分钟。
	/// 只从剑仙那里得到（结局时随礼物抛给你），不能合成。
	/// </summary>
	public class AzureCloudDraught : ModItem
	{
		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 20;
		}

		public override void SetDefaults()
		{
			Item.width = 16;
			Item.height = 24;
			Item.useStyle = ItemUseStyleID.DrinkLiquid;
			Item.useTime = 17;
			Item.useAnimation = 17;
			Item.useTurn = true;
			Item.UseSound = SoundID.Item3;
			Item.maxStack = Item.CommonMaxStack;
			Item.consumable = true;
			Item.potion = true;          // 和治疗药水一样受"药水病"限制，也能用快捷回血键
			Item.healLife = 200;         // 强效治疗药水是 150
			Item.buffType = ModContent.BuffType<Tipsy>();
			Item.buffTime = 60 * 60 * 5;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(silver: 50);
		}
	}
}
