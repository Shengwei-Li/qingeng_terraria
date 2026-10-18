using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items.Armor
{
	/// <summary>青衣剑仙面具：时装。像原版 Boss 面具一样整个头换成剑仙的——眉眼、黑发高髻、青发带、脑后长发。</summary>
	[AutoloadEquip(EquipType.Head)]
	public class SwordImmortalMask : ModItem
	{
		public override void SetStaticDefaults()
		{
			ArmorIDs.Head.Sets.DrawHead[Item.headSlot] = false; // 整个头都由面具画，玩家自己的头不画
		}

		public override void SetDefaults()
		{
			Item.width = 36;
			Item.height = 36;
			Item.vanity = true;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.sellPrice(silver: 75);
		}
	}
}
