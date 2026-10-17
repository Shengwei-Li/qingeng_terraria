using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Buffs
{
	/// <summary>
	/// 剑被夺 / Disarmed: 拿清月剑去打剑仙，剑被他夺走，这一战用不了清月剑。
	/// 物品不会消失；战斗结束（剑仙离开或赠剑）时解除，见 SwordPlayer 和 SwordImmortal 的结局。
	/// </summary>
	public class Disarmed : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
			BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
		}
	}
}
