using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items.Armor
{
	/// <summary>
	/// 青衣剑仙的斗笠：时装，占 Boss 面具的位置。PixelLab 画的竹编斗笠，斜戴，帽檐垂纱，遮住大半张脸。
	/// 玩家自己的头照常画，头发按"戴帽"的样子压短，斗笠盖在上面。
	/// </summary>
	[AutoloadEquip(EquipType.Head)]
	public class SwordImmortalHat : ModItem
	{
		public override void SetStaticDefaults()
		{
			ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = true;
		}

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 28;
			Item.vanity = true;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.sellPrice(silver: 75);
		}
	}
}
