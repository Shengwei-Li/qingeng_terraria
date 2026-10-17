using System;
using Microsoft.Xna.Framework;
using QingyiJianxian.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Projectiles
{
	/// <summary>
	/// 清月剑气：挥剑甩出的一道月牙，越飞越淡。ai[0] = 1 为旋斩的大月牙（更大、更远、穿透更多），0 为前两剑的小月牙。
	/// </summary>
	public class ClearmoonQi : ModProjectile
	{
		private const float HalfArc = 0.95f;
		private bool Big => Projectile.ai[0] >= 1f;
		private float Radius => Big ? 58f : 34f;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		public override void SetDefaults()
		{
			Projectile.width = 56;
			Projectile.height = 56;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = 5;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.timeLeft = 55;
		}

		public override void AI()
		{
			if (Projectile.localAI[0] == 0f)
			{
				// 第一帧按大小定型（每个客户端都会跑，联机时各端一致）。小月牙飞约半屏，大月牙飞约一屏
				Projectile.localAI[0] = 1f;
				Projectile.timeLeft = Big ? 55 : 30;
				if (!Big)
					Projectile.penetrate = 2;
				int size = Big ? 80 : 40;
				Projectile.Resize(size, size);
			}
			Projectile.velocity *= 0.985f;
			Projectile.rotation = Projectile.velocity.ToRotation();
			if (!Main.dedServ && Main.rand.NextBool(2))
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f), DustID.Frost, -Projectile.velocity * 0.1f, 100, default, 0.9f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			const int n = 16;
			var pts = new Vector2[n];
			Vector2 center = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * Radius * 0.6f;
			for (int i = 0; i < n; i++)
			{
				float a = Projectile.rotation - HalfArc + 2f * HalfArc * i / (n - 1);
				pts[i] = center + a.ToRotationVector2() * Radius;
			}
			float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
			// 月色：外圈淡青，内芯月白
			float w = Big ? 30f : 18f;
			Trail.Draw(pts, p => w * MathF.Sin(MathF.PI * p) + 2f, p => Fx.PaleTeal * 0.8f * fade, Trail.Glow.Value, additive: true);
			Trail.Draw(pts, p => w * 0.4f * MathF.Sin(MathF.PI * p) + 1f, p => new Color(250, 252, 240) * fade, Trail.Glow.Value, additive: true);
			if (Big)
			{
				// 大月牙后面再拖一道淡的余光
				var back = new Vector2[n];
				Vector2 offset = -Projectile.velocity.SafeNormalize(Vector2.UnitX) * 22f;
				for (int i = 0; i < n; i++)
					back[i] = pts[i] + offset;
				Trail.Draw(back, p => w * 0.7f * MathF.Sin(MathF.PI * p) + 1f, p => Fx.Teal * 0.35f * fade, Trail.Glow.Value, additive: true);
			}
			return false;
		}
	}
}
