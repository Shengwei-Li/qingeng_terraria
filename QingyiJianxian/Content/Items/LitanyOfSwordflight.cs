using QingyiJianxian.Content.Mounts;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>御剑诀 / Litany of Swordflight: 念一句口诀，脚下生云（坐骑）。</summary>
	public class LitanyOfSwordflight : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 23;
			Item.height = 24;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.UseSound = SoundID.Item24;
			Item.noMelee = true;
			Item.mountType = ModContent.MountType<WanderingCloud>();
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(gold: 5);
		}
	}
}
