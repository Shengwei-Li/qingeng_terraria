using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items.Armor
{
	/// <summary>
	/// 青衫·发冠 / Wayfarer's Jade Crown. 玉冠、白玉簪、一条青发带，不遮头发。
	/// 青衫套装是"两件起"：剑袍 + 云履就有套装效果；再戴上发冠，额外解锁罡气护体。
	/// </summary>
	[AutoloadEquip(EquipType.Head)]
	public class AzureWayfarerCrown : ModItem
	{
		public override void SetStaticDefaults()
		{
			ArmorIDs.Head.Sets.DrawFullHair[Item.headSlot] = true; // 冠只束在头顶，头发照常画
		}

		public override void SetDefaults()
		{
			Item.width = 44;
			Item.height = 48;
			Item.defense = 11;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(gold: 4);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetCritChance(DamageClass.Melee) += 6f;
			player.GetModPlayer<Common.SwordPlayer>().Crown = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<CloudweaveSilk>(6)
				.AddIngredient(ItemID.Silk, 6)
				.AddIngredient(ItemID.SoulofLight, 6)
				.AddTile(TileID.Loom)
				.Register();
		}
	}
}
