using System;
using Microsoft.Xna.Framework;
using QingyiJianxian.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.NPCs.SwordImmortal
{
	/// <summary>
	/// 拂袖剑气 / A Flick of the Sleeve: the lazy wave that drives a pre-hardmode challenger away.
	/// A crescent of sword-light that bends a little toward its target at first, then flies straight.
	/// ai[0] = target player.
	/// </summary>
	public class DismissQi : ModProjectile
	{
		private const float Speed = 24f;
		private const float HalfArc = 0.9f;   // 月牙张开的角度（弧度的一半）
		private const float Radius = 46f;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private ref float Age => ref Projectile.localAI[0];

		public override void SetDefaults()
		{
			Projectile.width = 60;
			Projectile.height = 60;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 90;
		}

		public override void AI()
		{
			// 头二十帧微微追人，之后笔直飞走
			Player target = Main.player[(int)Projectile.ai[0]];
			if (Age < 20 && target.active && !target.dead)
			{
				Vector2 want = Projectile.DirectionTo(target.Center) * Speed;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
			}
			Projectile.rotation = Projectile.velocity.ToRotation();
			Age++;

			if (!Main.dedServ && Main.rand.NextBool(2))
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f), DustID.Frost, -Projectile.velocity * 0.1f, 100, default, 1f);
				d.noGravity = true;
			}
		}

		public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
		{
			modifiers.Knockback *= 2.5f; // 被扫出去老远
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// 月牙：沿着飞行方向前方的一段圆弧，两头尖、中间宽
			const int n = 16;
			var pts = new Vector2[n];
			Vector2 center = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * Radius * 0.6f;
			for (int i = 0; i < n; i++)
			{
				float a = Projectile.rotation - HalfArc + 2f * HalfArc * i / (n - 1);
				pts[i] = center + a.ToRotationVector2() * Radius;
			}
			float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 15f, 0f, 1f);
			Trail.Draw(pts, p => 26f * MathF.Sin(MathF.PI * p) + 2f, p => Fx.Teal * 0.85f * fadeOut, Trail.Glow.Value, additive: true);
			Trail.Draw(pts, p => 10f * MathF.Sin(MathF.PI * p) + 1f, p => Color.White * 0.9f * fadeOut, Trail.Glow.Value, additive: true);
			return false;
		}
	}
}
