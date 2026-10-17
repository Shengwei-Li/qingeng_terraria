using Terraria;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Buffs
{
	/// <summary>微醺 / A Pleasant Tipple: 伤害 +10%、防御 +8、生命再生 +4、移速 +10%。</summary>
	public class Tipsy : ModBuff
	{
		public override void Update(Player player, ref int buffIndex)
		{
			player.GetDamage(DamageClass.Generic) += 0.10f;
			player.statDefense += 8;
			player.lifeRegen += 4;
			player.moveSpeed += 0.10f;
		}
	}
}
