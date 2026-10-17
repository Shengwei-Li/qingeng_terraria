using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.NPCs.SwordImmortal
{
	/// <summary>
	/// 落叶成剑 / Falling Leaves, Rising Blades: a harmless drifting leaf that turns into a sword-light and darts at the player.
	/// ai[0] = ticks until it transforms.
	/// </summary>
	public class FallingLeaf : ModProjectile
	{
		private const int AimTime = 20;   // 化剑后预警多久再出手
		private const float DartSpeed = 22f;

		private ref float Age => ref Projectile.localAI[0];
		private int TransformAt => (int)Projectile.ai[0];
		private bool IsBlade => Age >= TransformAt;
		private bool Fired => Age >= TransformAt + AimTime;
		private Vector2 aimDir = Vector2.UnitY;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 6;
			ProjectileID.Sets.TrailingMode[Type] = 0;
		}

		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 600;
		}

		public override bool CanHitPlayer(Player target) => Fired;

		public override void AI()
		{
			Age++;
			if (!IsBlade)
			{
				// 飘落：左右摇摆，慢慢下坠
				float phase = Projectile.identity * 1.7f;
				Projectile.velocity = new Vector2(MathF.Sin(Age * 0.05f + phase) * 1.6f, 1.4f + MathF.Sin(Age * 0.11f + phase) * 0.4f);
				Projectile.rotation = MathF.Sin(Age * 0.05f + phase) * 0.8f;
				return;
			}

			if (!Fired)
			{
				Projectile.velocity *= 0.8f;
				if (Age < TransformAt + AimTime - 6)
				{
					Player p = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
					Vector2 aim = p.Center;
					if (Projectile.identity % 2 == 0) // 一半的落叶预判走位
						aim += p.velocity * MathHelper.Clamp(Projectile.Distance(p.Center) / DartSpeed, 0f, 30f);
					aimDir = Projectile.DirectionTo(aim);
				}
				Projectile.rotation = aimDir.ToRotation();
				if (Age == TransformAt + AimTime - 1)
				{
					Projectile.velocity = aimDir * DartSpeed;
					Projectile.timeLeft = 120;
					Projectile.netUpdate = true;
				}
			}
			else if (Main.rand.NextBool(3))
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, Vector2.Zero, 120, default, 0.7f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			if (!IsBlade)
			{
				Texture2D leaf = TextureAssets.Projectile[Type].Value;
				sb.Draw(leaf, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, leaf.Size() / 2f, 1f, SpriteEffects.None, 0f);
				return false;
			}

			// 叶化为剑光：白芯青边的细长光刃
			Vector2 c = Projectile.Center;
			Vector2 half = aimDir * 16f;
			if (!Fired)
			{
				float t = (Age - TransformAt) / AimTime;
				Fx.Line(sb, c, c + aimDir * 260f, Fx.Cinnabar * (0.15f + 0.35f * t), 1f);
			}
			else
			{
				Trail.Draw(FlyingSword.Centers(Projectile, Vector2.Zero), p => 8f * (1f - p), p => Fx.Teal * 0.7f * (1f - p), Trail.Glow.Value, additive: true);
			}
			Fx.Line(sb, c - half, c + half, Fx.Teal, 4f);
			Fx.Line(sb, c - half * 0.8f, c + half * 0.9f, Color.White, 2f);
			return false;
		}
	}
}
