using QingyiJianxian.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>剑穗 / Inkwake Tassel: 剑类伤害 +15%、暴击 +10%，暴击留墨痕，一秒后炸开。</summary>
	public class InkwakeTassel : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 56;
			Item.accessory = true;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(gold: 5);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetModPlayer<SwordPlayer>().Tassel = true;
		}
	}
}
