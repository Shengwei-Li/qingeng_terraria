using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items.Armor
{
	/// <summary>青衣剑仙面具：时装。戴上就是剑仙的发型——黑发高髻、青发带、脑后长发。</summary>
	[AutoloadEquip(EquipType.Head)]
	public class SwordImmortalMask : ModItem
	{
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
