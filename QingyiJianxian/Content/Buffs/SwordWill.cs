using QingyiJianxian.Common;
using Terraria;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Buffs
{
	/// <summary>剑意 / Sword-Will: 青衫套装下剑类命中叠层，最多 5 层；只用来显示层数，效果在 <see cref="SwordPlayer"/>。</summary>
	public class SwordWill : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			int stacks = Main.LocalPlayer.GetModPlayer<SwordPlayer>().WillStacks;
			buffName += $" ×{stacks}";
		}
	}
}
