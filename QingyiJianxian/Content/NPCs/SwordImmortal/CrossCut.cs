using System;
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
	/// 剑气纵横 / Sword-Will Unbound: 一道贯穿整个领域的剑气。先闪一线极细的寒光（不画红线，三阶段的规矩），
	/// 片刻后整条斩下，只在斩下的那几帧有伤害。
	/// Center = 这一刀经过的点；ai[0] = 刀的角度。
	/// </summary>
	public class CrossCut : ModProjectile
	{
		public const int WarnTime = 24;     // 寒光预警 0.4 秒
		private const int ActiveTime = 8;    // 斩下后有伤害的帧数
		private const int FadeTime = 18;
		private const float Length = 3400f;
		private const float Width = 64f;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private ref float Age => ref Projectile.localAI[0];
		private Vector2 Dir => Projectile.ai[0].ToRotationVector2();
		private Vector2 A => Projectile.Center - Dir * Length / 2f;
		private Vector2 B => Projectile.Center + Dir * Length / 2f;
		private bool Active => Age >= WarnTime && Age < WarnTime + ActiveTime;

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = WarnTime + ActiveTime + FadeTime + 2;
		}

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			if (Age == WarnTime)
			{
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.6f, Volume = 0.8f }, Main.LocalPlayer.Center);
				if (!Main.dedServ)
				{
					// 沿刀身迸出几点光屑
					for (int i = 0; i < 24; i++)
					{
						Vector2 p = Vector2.Lerp(A, B, Main.rand.NextFloat());
						Dust d = Dust.NewDustPerfect(p, DustID.Frost, Dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-3f, 3f), 80, default, 1.2f);
						d.noGravity = true;
					}
				}
			}
			Age++;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle target)
		{
			if (!Active)
				return false;
			float point = 0f;
			return Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), A, B, Width, ref point);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			if (Age < WarnTime)
			{
				// 寒光：一线极细的光，越来越亮，最后几帧闪一下
				float t = Age / WarnTime;
				float flicker = Age > WarnTime - 6 && (int)Age % 2 == 0 ? 1.5f : 1f;
				Fx.Line(sb, A, B, new Color(200, 245, 240) * (0.15f + 0.45f * t) * flicker, 1f + t);
				return false;
			}
			// 斩：一道白芯青边的宽剑气，斩下后迅速收细淡去
			float k = 1f - MathHelper.Clamp((Age - WarnTime) / (ActiveTime + FadeTime), 0f, 1f);
			Fx.Line(sb, A, B, Fx.Teal * 0.55f * k, Width * (0.6f + 0.6f * k));
			Fx.Line(sb, A, B, Color.White * k, 10f * k + 2f);
			return false;
		}
	}
}
