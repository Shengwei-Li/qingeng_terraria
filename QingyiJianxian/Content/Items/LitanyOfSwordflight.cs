using QingyiJianxian.Content.Mounts;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>腾云诀 / Cloudrider's Litany：念一句口诀，脚下生云（坐骑）。类名沿用旧名，免得已有存档里的物品丢失。</summary>
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
