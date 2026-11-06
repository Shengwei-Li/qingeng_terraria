using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Projectiles
{
	/// <summary>
	/// 清流剑符的一道剑气：与剑仙的"剑气纵横"同一个画法，但有长度、有上限——
	/// 长 384 像素（24 格），宽 44；先闪 0.25 秒寒光，再斩下，只在斩下的几帧有伤害；一笔最多斩 8 个敌人；对 Boss 打七折。
	/// Center = 斩过的点；ai[0] = 这一笔的角度。
	/// </summary>
	public class TalismanStroke : ModProjectile
	{
		private const int WarnTime = 15;
		private const int ActiveTime = 5;
		private const int FadeTime = 16;
		private const float HalfLength = 192f;
		private const float Width = 44f;
		private const int MaxTargets = 8;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private ref float Age => ref Projectile.localAI[0];
		private ref float Hits => ref Projectile.localAI[1];
		private Vector2 Dir => Projectile.ai[0].ToRotationVector2();
		private Vector2 A => Projectile.Center - Dir * HalfLength;
		private Vector2 B => Projectile.Center + Dir * HalfLength;

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Generic;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.timeLeft = WarnTime + ActiveTime + FadeTime + 2;
		}

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			if (Age == WarnTime)
			{
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f }, Projectile.Center);
				if (!Main.dedServ)
				{
					for (int i = 0; i < 18; i++)
					{
						Vector2 p = Vector2.Lerp(A, B, Main.rand.NextFloat());
						Dust d = Dust.NewDustPerfect(p, DustID.Frost, Dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-3f, 3f), 80, default, 1.1f);
						d.noGravity = true;
					}
				}
			}
			Age++;
		}

		public override bool? CanHitNPC(NPC target) => Age >= WarnTime && Age < WarnTime + ActiveTime && Hits < MaxTargets ? null : false;

		public override bool? Colliding(Rectangle projHitbox, Rectangle target)
		{
			float point = 0f;
			return Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), A, B, Width, ref point);
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (target.boss)
				modifiers.SourceDamage *= 0.7f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Hits++;

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			if (Age < WarnTime)
			{
				// 寒光一线，从斩点向两头展开
				float t = Age / WarnTime;
				Fx.Line(sb, Projectile.Center - Dir * HalfLength * t, Projectile.Center + Dir * HalfLength * t, new Color(200, 245, 240) * (0.25f + 0.5f * t), 1f + t);
				return false;
			}
			// 斩下：白芯青边，迅速收细淡去；两头收尖，看得出这一笔到哪里为止
			float k = 1f - MathHelper.Clamp((Age - WarnTime) / (ActiveTime + FadeTime), 0f, 1f);
			const int n = 12;
			var pts = new Vector2[n];
			for (int i = 0; i < n; i++)
				pts[i] = Vector2.Lerp(B, A, i / (n - 1f));
			Trail.Draw(pts, p => Width * (0.5f + 0.6f * k) * System.MathF.Sin(System.MathF.PI * p) + 2f, p => Fx.Teal * (0.6f * k), Trail.Glow.Value, additive: true);
			Trail.Draw(pts, p => (10f * k + 2f) * System.MathF.Sin(System.MathF.PI * p) + 1f, p => Color.White * k, Trail.Glow.Value, additive: true);
			return false;
		}
	}
}
