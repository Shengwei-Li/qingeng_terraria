using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items.Armor
{
	/// <summary>青衫·云履 / Wayfarer's Cloudstriders.</summary>
	[AutoloadEquip(EquipType.Legs)]
	public class AzureWayfarerBoots : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 64;
			Item.height = 64;
			Item.defense = 10;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(gold: 4);
		}

		public override void UpdateEquip(Player player)
		{
			player.moveSpeed += 0.10f;
			player.GetAttackSpeed(DamageClass.Melee) += 0.10f;
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
			=> body.type == ModContent.ItemType<AzureWayfarerRobe>() && legs.type == Type;

		public override void UpdateArmorSet(Player player)
		{
			// 套装效果写在剑袍里；这里也认，免得只调用到靴子时漏掉
			player.setBonus = AzureWayfarerRobe.SetBonusText.Value;
			player.GetModPlayer<Common.SwordPlayer>().WayfarerSet = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<CloudweaveSilk>(7)
				.AddIngredient(ItemID.Silk, 8)
				.AddIngredient(ItemID.SoulofFlight, 6)
				.AddTile(TileID.Loom)
				.Register();
		}
	}
}
