using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using QingyiJianxian.Common;
using Terraria;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Projectiles
{
	/// <summary>
	/// 清月剑气。四层叠出"仙气"：
	///   剑芯——白玉色，温润的白；
	///   外焰——一层青气，气纹沿着剑气不停流动，像在烧；
	///   尾迹——身后两缕云丝缠绕着飘，越远越淡越散；
	///   光屑——一路洒下细小的白玉光点，慢慢往上飘。
	/// 匀速飞：一线剑气约 800 像素，长剑气约 1000 像素；飞到尽头放慢、散成一团云气淡去。
	/// 剑气穿物，但不是"不在一个图层"：埋进方块的那一段几乎看不见，进出地面的地方溅起水雾和光屑，沿途照亮四周。
	/// ai[0] = 0：横扫 / 回撩的一线剑气（横着飞，略带弧度，穿 2 个敌人）；1：前刺的长剑气（顺着飞，穿 3 个，刺中留斩痕）。
	/// 每穿过一个敌人伤害打七折。ai[1] = 弧度朝向（±1）。
	/// </summary>
	public class ClearmoonQi : ModProjectile
	{
		private const int FadeFrames = 10;
		private const int N = 16;

		private bool Lance => Projectile.ai[0] >= 1f;
		private float Bow => Projectile.ai[1] >= 0 ? 1f : -1f;
		private float HalfLength => Lance ? 84f : 60f;
		private int Life => Lance ? 36 : 38;
		private float Fade => MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);

		private Vector2 fwd = Vector2.UnitX;
		private readonly bool[] buried = new bool[N];

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		public override void SetStaticDefaults()
		{
			Terraria.ID.ProjectileID.Sets.TrailCacheLength[Type] = 14;
			Terraria.ID.ProjectileID.Sets.TrailingMode[Type] = 0;
		}

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
			Projectile.timeLeft = 38;
		}

		public override void AI()
		{
			if (Projectile.localAI[0] == 0f)
			{
				// 第一帧按种类定型（每个客户端都会跑，联机时各端一致）
				Projectile.localAI[0] = 1f;
				Projectile.penetrate = Lance ? 3 : 2;
				Projectile.timeLeft = Life;
			}
			if (Projectile.velocity.LengthSquared() > 0.01f)
				fwd = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			// 匀速飞，最后几帧放慢散开
			if (Projectile.timeLeft <= FadeFrames)
				Projectile.velocity *= 0.86f;
			Projectile.rotation = fwd.ToRotation();

			Vector2[] pts = Points(N, 1f);
			for (int i = 0; i < N; i++)
				buried[i] = Fx.Solid(pts[i]);

			if (Main.dedServ)
				return;
			for (int i = 0; i < N; i++)
			{
				if (!buried[i] && i % 4 == 0)
					Lighting.AddLight(pts[i], 0.22f * Fade, 0.42f * Fade, 0.4f * Fade);
				// 进出方块的地方：溅起水雾和光屑
				bool edge = i > 0 && buried[i] != buried[i - 1];
				if (edge && Main.rand.NextBool(2))
				{
					Fx.Mist(pts[i], -fwd * 1.5f + Main.rand.NextVector2Circular(1.5f, 1.5f), 0.9f);
					Fx.Mote(pts[i], Main.rand.NextVector2Circular(2.5f, 2.5f), 0.7f);
				}
			}
			// 一路洒下的光屑
			int k = Main.rand.Next(N);
			if (!buried[k])
				Fx.Mote(pts[k], -Projectile.velocity * 0.04f - Vector2.UnitY * 0.6f, Main.rand.NextFloat(0.5f, 0.85f));
			// 尽头散成云气
			if (Projectile.timeLeft <= FadeFrames && Main.rand.NextBool(2))
				Fx.Mist(pts[Main.rand.Next(N)], Main.rand.NextVector2Circular(1f, 1f), 1.1f);
		}

		/// <summary>剑气这一线的点：一线剑气横着、两端往后收中间往前凸；长剑气顺着飞行方向。spread 让散去时变宽。</summary>
		private Vector2[] Points(int n, float spread)
		{
			var pts = new Vector2[n];
			Vector2 side = fwd.RotatedBy(MathHelper.PiOver2);
			for (int i = 0; i < n; i++)
			{
				float u = -1f + 2f * i / (n - 1);
				pts[i] = Lance
					? Projectile.Center + fwd * (u * HalfLength * spread)
					: Projectile.Center + side * (u * HalfLength * Bow * spread) - fwd * (u * u * 24f) + side * (Bow * 6f);
			}
			return pts;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Vector2[] pts = Points(5, 1f);
			float point = 0f;
			for (int i = 0; i < pts.Length - 1; i++)
			{
				if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), pts[i], pts[i + 1], Lance ? 18f : 22f, ref point))
					return true;
			}
			return false;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Lance)
				ClearmoonScar.Mark(Projectile, target, Projectile.damage);
			QiBloom.Spawn(Projectile, target.Center, Lance ? 64f : 52f);
			// 每穿过一个敌人，剑气弱一分
			Projectile.damage = (int)(Projectile.damage * 0.7f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			float fade = Fade;
			float spread = 1f + 0.35f * (1f - fade);
			Vector2[] pts = Points(N, spread);
			float time = Main.GlobalTimeWrappedHourly;
			// 埋进方块的那一段几乎看不见
			float Vis(float p) => buried[Math.Clamp((int)MathF.Round(p * (N - 1)), 0, N - 1)] ? 0.12f : 1f;

			DrawWisps(fade, time);

			if (Lance)
			{
				// 长剑气：前锐后阔，头在 pts 的末端
				float Shape(float p) => MathF.Pow(1f - p, 0.7f) * MathF.Sin(MathF.PI * MathF.Min(1f, p * 1.4f + 0.05f)) + 0.08f;
				Vector2[] head = (Vector2[])pts.Clone();
				Array.Reverse(head);
				Trail.Draw(head, p => 40f * Shape(p) * Vis(1f - p), p => Fx.DeepTeal * (0.55f * fade), Trail.Glow.Value);
				Trail.Draw(head, p => 34f * Shape(p) * Vis(1f - p), p => Fx.PaleTeal * (0.8f * fade), Trail.Qi.Value, uRepeat: 1.6f, uScroll: time * 2.6f);
				Trail.Draw(head, p => 9f * Shape(p) * Vis(1f - p) + 1f, p => Fx.Jade * fade, Trail.Glow.Value);
				Trail.Draw(head, p => 6f * Shape(p) * Vis(1f - p) + 1f, p => Fx.Jade * (0.6f * fade), Trail.Glow.Value, additive: true);
			}
			else
			{
				float Shape(float p) => MathF.Sin(MathF.PI * p);
				// 身子：深青（普通混合）；气纹：淡青丝缕流动；芯：白玉；最后叠一层光
				Trail.Draw(pts, p => (36f * Shape(p) + 2f) * Vis(p) * spread, p => Fx.DeepTeal * (0.55f * fade), Trail.Glow.Value);
				Trail.Draw(pts, p => (32f * Shape(p) + 2f) * Vis(p) * spread, p => Fx.PaleTeal * (0.85f * fade), Trail.Qi.Value, uRepeat: 1.2f, uScroll: time * 2.2f);
				Trail.Draw(pts, p => (8f * Shape(p) + 1f) * Vis(p), p => Fx.Jade * fade, Trail.Glow.Value);
				Trail.Draw(pts, p => (5f * Shape(p) + 1f) * Vis(p), p => Fx.Jade * (0.6f * fade), Trail.Glow.Value, additive: true);
			}
			return false;
		}

		/// <summary>尾迹：两缕云丝从剑气身后拖出来，绕着飞行轨迹缠绕摆动，越远越淡。</summary>
		private void DrawWisps(float fade, float time)
		{
			var history = new List<Vector2> { Projectile.Center };
			foreach (Vector2 old in Projectile.oldPos)
			{
				if (old == Vector2.Zero)
					break;
				history.Add(old + Projectile.Size / 2f);
			}
			if (history.Count < 3)
				return;
			Vector2 side = fwd.RotatedBy(MathHelper.PiOver2);
			float anchor = Lance ? 0f : HalfLength * 0.6f;
			float amp = Lance ? 9f : 7f;
			for (int s = -1; s <= 1; s += 2)
			{
				var pts = new Vector2[history.Count];
				for (int k = 0; k < history.Count; k++)
				{
					float grow = k / (float)history.Count;
					float wave = MathF.Sin(time * 14f + k * 0.75f + (s > 0 ? 0f : MathHelper.Pi)) * amp * (0.3f + grow);
					pts[k] = history[k] + side * (s * anchor * (1f - 0.4f * grow) + (Lance ? s * wave : wave));
				}
				Trail.Draw(pts, p => 14f * (1f - p) + 1f, p => Fx.DeepTeal * (0.3f * fade * (1f - p)), Trail.Glow.Value);
				Trail.Draw(pts, p => 12f * (1f - p) + 1f, p => Fx.PaleTeal * (0.6f * fade * (1f - p)), Trail.Qi.Value, uRepeat: 1f, uScroll: time * 1.5f + s);
				Trail.Draw(pts, p => 3f * (1f - p) + 0.5f, p => Fx.Jade * (0.55f * fade * (1f - p)), Trail.Glow.Value);
			}
		}
	}
}
