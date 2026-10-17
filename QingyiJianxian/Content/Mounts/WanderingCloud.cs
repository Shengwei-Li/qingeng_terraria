using System.Linq;
using Microsoft.Xna.Framework;
using QingyiJianxian.Content.Buffs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Mounts
{
	/// <summary>
	/// 坐骑：云 / Wandering Cloud。踏云而行，可以悬停、自由上下飞。速度不追求最快，肉后前期也不至于超模。
	/// </summary>
	public class WanderingCloud : ModMount
	{
		public override void SetStaticDefaults()
		{
			MountData.buff = ModContent.BuffType<WanderingCloudBuff>();
			MountData.spawnDust = DustID.Cloud;
			MountData.spawnDustNoGravity = true;

			MountData.heightBoost = 8;
			MountData.fallDamage = 0f;
			MountData.runSpeed = 7.5f;
			MountData.dashSpeed = 7.5f;
			MountData.acceleration = 0.2f;
			MountData.jumpHeight = 10;
			MountData.jumpSpeed = 5f;
			MountData.blockExtraJumps = true;
			// 悬停：按上下键自由升降，不耗飞行时间
			MountData.usesHover = true;
			MountData.flightTimeMax = int.MaxValue;
			MountData.fatigueMax = int.MaxValue;
			MountData.swimSpeed = 4f;

			MountData.totalFrames = 2;
			// 人往下沉一点，脚踩进云里；云的下半部分（_Front 贴图）画在人前面盖住脚
			MountData.playerYOffsets = Enumerable.Repeat(8, MountData.totalFrames).ToArray();
			MountData.xOffset = 0;
			MountData.yOffset = 10;
			MountData.bodyFrame = 0;
			MountData.playerHeadOffset = 14;

			// 两帧轻轻鼓动，各种状态都用这两帧循环
			MountData.standingFrameCount = 2;
			MountData.standingFrameDelay = 24;
			MountData.standingFrameStart = 0;
			MountData.runningFrameCount = 2;
			MountData.runningFrameDelay = 16;
			MountData.runningFrameStart = 0;
			MountData.flyingFrameCount = 2;
			MountData.flyingFrameDelay = 16;
			MountData.flyingFrameStart = 0;
			MountData.inAirFrameCount = 2;
			MountData.inAirFrameDelay = 16;
			MountData.inAirFrameStart = 0;
			MountData.idleFrameCount = 2;
			MountData.idleFrameDelay = 24;
			MountData.idleFrameStart = 0;
			MountData.idleFrameLoop = true;
			MountData.swimFrameCount = 2;
			MountData.swimFrameDelay = 16;
			MountData.swimFrameStart = 0;

			if (!Main.dedServ)
			{
				MountData.textureWidth = MountData.backTexture.Width();
				MountData.textureHeight = MountData.backTexture.Height();
			}
		}

		public override void UpdateEffects(Player player)
		{
			// 云边飘雾
			if (!Main.dedServ && Main.rand.NextBool(6))
			{
				Dust d = Dust.NewDustPerfect(player.Bottom + new Vector2(Main.rand.NextFloat(-40f, 40f), 8f), DustID.Cloud,
					new Vector2(-player.velocity.X * 0.1f, 0.3f), 150, default, 1.1f);
				d.noGravity = true;
			}
		}
	}
}
