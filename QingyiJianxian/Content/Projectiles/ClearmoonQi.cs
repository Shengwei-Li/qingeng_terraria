using System;
using Microsoft.Xna.Framework;
using QingyiJianxian.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Projectiles
{
	/// <summary>
	/// 清月剑气。跟三阶段的"剑气纵横"一个画法——白芯青边的一线光，只是小得多。
	/// ai[0] = 0：横扫 / 回撩甩出的一线剑气，横着飞，略带弧度，穿 2 个敌人；
	/// ai[0] = 1：前刺刺出的长剑气，顺着飞，穿 3 个敌人，刺中的敌人片刻后再绽开一道斩痕。
	/// 每穿过一个敌人伤害打七折。不撞墙（剑气本就穿物），但飞不远。
	/// ai[1] = 弧度朝向（±1），让横扫和回撩的剑气弯得不一样。
	/// </summary>
	public class ClearmoonQi : ModProjectile
	{
		private bool Lance => Projectile.ai[0] >= 1f;
		private float Bow => Projectile.ai[1] >= 0 ? 1f : -1f;
		private float HalfLength => Lance ? 80f : 56f;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		public override void SetDefaults()
		{
			Projectile.width = 24;
			Projectile.height = 24;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = 2;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.timeLeft = 30;
		}

		public override void AI()
		{
			if (Projectile.localAI[0] == 0f)
			{
				// 第一帧按种类定型（每个客户端都会跑，联机时各端一致）。一线剑气飞约 450 像素，长剑气约 600
				Projectile.localAI[0] = 1f;
				Projectile.penetrate = Lance ? 3 : 2;
				Projectile.timeLeft = Lance ? 26 : 28;
			}
			Projectile.velocity *= Lance ? 0.97f : 0.95f;
			Projectile.rotation = Projectile.velocity.ToRotation();
			if (!Main.dedServ && Main.rand.NextBool(3))
			{
				Vector2 along = Lance ? Projectile.velocity.SafeNormalize(Vector2.UnitX) : Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
				Dust d = Dust.NewDustPerfect(Projectile.Center + along * Main.rand.NextFloat(-HalfLength, HalfLength), DustID.Frost, -Projectile.velocity * 0.05f, 120, default, 0.7f);
				d.noGravity = true;
			}
		}

		/// <summary>剑气这一线的若干个点：一线剑气横着、向后弯一点；长剑气顺着飞行方向。</summary>
		private Vector2[] Points(int n)
		{
			var pts = new Vector2[n];
			Vector2 fwd = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			Vector2 side = fwd.RotatedBy(MathHelper.PiOver2);
			for (int i = 0; i < n; i++)
			{
				float u = -1f + 2f * i / (n - 1);
				pts[i] = Lance
					? Projectile.Center + fwd * (u * HalfLength)
					// 两端往后收，中间往前凸；整条略偏向挥剑那一侧
					: Projectile.Center + side * (u * HalfLength * Bow) - fwd * (u * u * 22f) + side * (Bow * 6f);
			}
			return pts;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Vector2[] pts = Points(5);
			float point = 0f;
			for (int i = 0; i < pts.Length - 1; i++)
			{
				if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), pts[i], pts[i + 1], Lance ? 16f : 20f, ref point))
					return true;
			}
			return false;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Lance)
				ClearmoonScar.Mark(Projectile, target, Projectile.damage);
			// 每穿过一个敌人，剑气弱一分
			Projectile.damage = (int)(Projectile.damage * 0.7f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2[] pts = Points(16);
			// 飞到最后几帧淡去
			float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
			// 两头尖、中间宽：一线利落的光
			if (Lance)
			{
				Trail.Draw(pts, p => 16f * MathF.Pow(p, 0.6f) * (1f - p) * 2f + 1f, p => Fx.Teal * (0.6f * fade), Trail.Glow.Value, additive: true);
				Trail.Draw(pts, p => 5f * MathF.Pow(p, 0.6f) * (1f - p) * 2f + 1f, p => Color.White * fade, Trail.Glow.Value, additive: true);
			}
			else
			{
				Trail.Draw(pts, p => 14f * MathF.Sin(MathF.PI * p) + 1f, p => Fx.Teal * (0.6f * fade), Trail.Glow.Value, additive: true);
				Trail.Draw(pts, p => 4f * MathF.Sin(MathF.PI * p) + 1f, p => Color.White * fade, Trail.Glow.Value, additive: true);
				// 身后一道淡淡的余光，看得出它在往前飞
				Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitX) * 14f;
				for (int i = 0; i < pts.Length; i++)
					pts[i] += back;
				Trail.Draw(pts, p => 8f * MathF.Sin(MathF.PI * p) + 1f, p => Fx.PaleTeal * (0.25f * fade), Trail.Glow.Value, additive: true);
			}
			return false;
		}
	}
}
