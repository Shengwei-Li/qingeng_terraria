using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>云纹绡 / Cloudweave Silk: 剑仙衣袂上的仙气所化的布料。只在问剑之后从剑仙那里得到，用来织青衫。</summary>
	public class CloudweaveSilk : ModItem
	{
		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 25;
		}

		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 26;
			Item.maxStack = Item.CommonMaxStack;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(silver: 40);
		}
	}
}
