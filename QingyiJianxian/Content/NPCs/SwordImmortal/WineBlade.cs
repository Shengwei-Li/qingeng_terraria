using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.NPCs.SwordImmortal
{
	/// <summary>
	/// 醉剑 / Drunken Blades: 剑仙一口酒喷出，酒雾在空中一顿，凝成细小的剑光，醉醺醺地扭着飞来。
	/// velocity = 喷出的方向和速度；凝剑之前没有伤害。
	/// </summary>
	public class WineBlade : ModProjectile
	{
		private const int MistTime = 14;      // 酒雾在空中停顿多久再凝成剑
		private const float BladeSpeed = 17f;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private ref float Age => ref Projectile.localAI[0];
		private bool IsBlade => Age >= MistTime;
		private Vector2 dir;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 6;
			ProjectileID.Sets.TrailingMode[Type] = 0;
		}

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = MistTime + 90;
		}

		public override bool CanHitPlayer(Player target) => IsBlade;

		public override void AI()
		{
			if (Age == 0)
				dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			if (!IsBlade)
			{
				// 酒雾：喷出去，越飘越慢
				Projectile.velocity *= 0.86f;
			}
			else
			{
				// 凝剑：加速飞出，左右扭两下，像醉步
				float wobble = MathF.Sin((Age - MistTime) * 0.25f + Projectile.identity) * 2.2f;
				Vector2 side = new(-dir.Y, dir.X);
				float speed = MathF.Min(BladeSpeed, (Age - MistTime) * 1.6f + 3f);
				Projectile.velocity = dir * speed + side * wobble;
			}
			Projectile.rotation = Projectile.velocity.ToRotation();
			Age++;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			Color wine = new(250, 232, 190);
			if (!IsBlade)
			{
				float grow = Age / MistTime;
				Fx.Circle(sb, Fx.SoftGlow.Value, Projectile.Center, 26f + 10f * grow, wine * (0.35f + 0.3f * grow));
				return false;
			}
			Trail.Draw(Trail.Centers(Projectile), p => 7f * (1f - p), p => wine * 0.7f * (1f - p), Trail.Glow.Value, additive: true);
			Vector2 half = Projectile.velocity.SafeNormalize(dir) * 11f;
			Fx.Line(sb, Projectile.Center - half, Projectile.Center + half, Fx.Teal, 3f);
			Fx.Line(sb, Projectile.Center - half * 0.8f, Projectile.Center + half, Color.White, 1.5f);
			return false;
		}
	}
}
