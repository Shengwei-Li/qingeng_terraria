using QingyiJianxian.Common;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items.Armor
{
	/// <summary>
	/// 青衫·剑袍 / Wayfarer's Robe. 上身是躯干和宽袖；下摆作为长袍画在腿部图层（同原版法师袍的做法）。
	/// 套装"青衫"只需要剑袍 + 云履两件，头盔随意搭配。
	/// </summary>
	[AutoloadEquip(EquipType.Body)]
	public class AzureWayfarerRobe : ModItem
	{
		public static LocalizedText SetBonusText { get; private set; }

		public override void Load()
		{
			if (Main.netMode != NetmodeID.Server)
				EquipLoader.AddEquipTexture(Mod, $"{Texture}_{EquipType.Legs}", EquipType.Legs, this);
		}

		public override void SetStaticDefaults()
		{
			ArmorIDs.Body.Sets.HidesHands[Item.bodySlot] = false;
			SetBonusText = this.GetLocalization("SetBonus");
		}

		public override void SetDefaults()
		{
			Item.width = 26;
			Item.height = 32;
			Item.defense = 15;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(gold: 6);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetDamage(DamageClass.Melee) += 0.08f;
			player.GetCritChance(DamageClass.Melee) += 8f;
		}

		public override void SetMatch(bool male, ref int equipSlot, ref bool robes)
		{
			// 下摆画在腿部图层，盖住腿
			robes = true;
			equipSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Legs);
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
			=> body.type == Type && legs.type == ModContent.ItemType<AzureWayfarerBoots>();

		public override void UpdateArmorSet(Player player)
		{
			player.setBonus = SetBonusText.Value;
			player.GetModPlayer<SwordPlayer>().WayfarerSet = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<CloudweaveSilk>(10)
				.AddIngredient(ItemID.Silk, 12)
				.AddIngredient(ItemID.PixieDust, 10)
				.AddIngredient(ItemID.CrystalShard, 8)
				.AddTile(TileID.Loom)
				.Register();
		}
	}
}
