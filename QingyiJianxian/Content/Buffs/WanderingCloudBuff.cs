using QingyiJianxian.Content.Mounts;
using Terraria;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Buffs
{
	/// <summary>乘云 / Upon the Cloud: shown while riding the Wandering Cloud.</summary>
	public class WanderingCloudBuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.buffNoTimeDisplay[Type] = true;
			Main.buffNoSave[Type] = true;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			player.mount.SetMount(ModContent.MountType<WanderingCloud>(), player);
			player.buffTime[buffIndex] = 10;
		}
	}
}
