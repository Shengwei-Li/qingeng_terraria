using QingyiJianxian.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>青衣剑仙纪念杯：每次问剑后 1/10 的机会得到。</summary>
	public class SwordImmortalTrophy : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<SwordImmortalTrophyTile>());
			Item.width = 48;
			Item.height = 48;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.buyPrice(gold: 1);
		}
	}

	/// <summary>青衣剑仙遗物：大师模式下每次问剑必得。</summary>
	public class SwordImmortalRelic : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<SwordImmortalRelicTile>());
			Item.width = 48;
			Item.height = 48;
			Item.rare = ItemRarityID.Master;
			Item.master = true;
			Item.value = Item.buyPrice(gold: 5);
		}
	}
}
