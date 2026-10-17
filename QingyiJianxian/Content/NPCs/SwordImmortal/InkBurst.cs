using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.NPCs.SwordImmortal
{
	/// <summary>
	/// 墨痕：一剑横江 leaves these along the dash path. A shrinking warning ring, then an ink burst.
	/// ai[0] = ticks before bursting.
	/// </summary>
	public class InkBurst : ModProjectile
	{
		private const int BurstFrames = 8;
		private const float Radius = 52f;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private ref float Age => ref Projectile.localAI[0];
		private int BurstAt => (int)Projectile.ai[0];
		private bool Bursting => Age >= BurstAt && Age < BurstAt + BurstFrames;

		public override void SetDefaults()
		{
			Projectile.width = (int)(Radius * 2);
			Projectile.height = (int)(Radius * 2);
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 600;
		}

		public override bool CanHitPlayer(Player target) => Bursting;

		public override void AI()
		{
			if (Age == BurstAt)
			{
				SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.3f }, Projectile.Center);
				if (Main.netMode != NetmodeID.Server)
				{
					for (int i = 0; i < 14; i++)
					{
						Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, Main.rand.NextVector2Circular(6f, 6f), 80, Fx.Ink, 2f);
						d.noGravity = true;
					}
				}
			}
			if (++Age >= BurstAt + BurstFrames + 10)
				Projectile.Kill();
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			Vector2 c = Projectile.Center;
			if (Age < BurstAt)
			{
				// 墨点：小而浓；红圈从外向内收拢，收到墨点时爆开
				float t = Age / BurstAt;
				Fx.Circle(sb, Fx.Disc.Value, c, 14f, Fx.Ink * 0.9f);
				Fx.Circle(sb, Fx.Ring.Value, c, MathHelper.Lerp(Radius * 2f, 18f, t), Fx.Cinnabar * (0.25f + 0.5f * t));
			}
			else
			{
				float t = (Age - BurstAt) / (BurstFrames + 10f);
				Fx.Circle(sb, Fx.Disc.Value, c, Radius * 2f * (0.7f + 0.3f * t), Fx.Ink * (1f - t));
				Fx.Circle(sb, Fx.Ring.Value, c, Radius * 2.2f * (0.8f + 0.4f * t), Fx.Teal * (0.8f * (1f - t)));
			}
			return false;
		}
	}
}
