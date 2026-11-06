using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Projectiles
{
	/// <summary>
	/// 御剑（清月剑右键）：剑脱手飞到点下的位置，在那里穿过三次、成一个"米"字的三笔，再飞回手里。
	/// 剑在外面的时候手里是空的，左右键都用不了；回手后冷却 2.5 秒。
	/// ai[0], ai[1] = 目标点；localAI[0] = 计时。
	/// </summary>
	public class ClearmoonFlight : ModProjectile
	{
		private const float GoSpeed = 26f;
		private const int SlashFrames = 10;
		private const int Slashes = 3;
		private const float SlashHalf = 90f;   // 每一剑从点的这头穿到那头
		private const float ReturnSpeed = 30f;
		private const float SwordScale = 0.85f;

		public override string Texture => "QingyiJianxian/Content/Items/Clearmoon";

		private Vector2 Point => new(Projectile.ai[0], Projectile.ai[1]);
		private ref float Timer => ref Projectile.localAI[0];
		// 0 = 飞去，1 = 连斩，2 = 飞回
		private ref float Mode => ref Projectile.localAI[1];
		private float baseAngle;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 10;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = SlashFrames; // 每一剑砍每个敌人一次
			Projectile.timeLeft = 240;
		}

		private static float EaseInOut(float t) => t * t * (3f - 2f * t);

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead)
			{
				Projectile.Kill();
				return;
			}
			Projectile.timeLeft = Math.Max(Projectile.timeLeft, 2);

			switch ((int)Mode)
			{
				case 0:
				{
					// 飞向第一剑的起点：落点前方 SlashHalf 处，第一剑顺着飞来的方向穿过去，衔接不跳
					if (Timer == 0)
						baseAngle = Projectile.velocity.ToRotation();
					Vector2 to = Point - baseAngle.ToRotationVector2() * SlashHalf - Projectile.Center;
					float speed = MathHelper.Min(GoSpeed, Projectile.velocity.Length() + 2.5f);
					Projectile.velocity = to.SafeNormalize(Vector2.UnitX) * MathHelper.Min(speed, to.Length());
					if (to.Length() < GoSpeed)
					{
						Mode = 1;
						Timer = 0;
						SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.5f }, Projectile.Center);
					}
					break;
				}
				case 1:
				{
					int k = (int)(Timer / SlashFrames);
					if (k >= Slashes)
					{
						Mode = 2;
						Timer = 0;
						break;
					}
					float s = EaseInOut((Timer % SlashFrames) / (SlashFrames - 1f));
					// 每一剑从点的一头穿到另一头；方向依次转 120°，三剑成一个米字
					Vector2 d = (baseAngle + k * MathHelper.TwoPi / Slashes + (k % 2 == 1 ? MathHelper.Pi : 0f)).ToRotationVector2();
					Vector2 target = Point + d * MathHelper.Lerp(-SlashHalf, SlashHalf, s);
					Projectile.velocity = target - Projectile.Center;
					if (Timer % SlashFrames == 0)
					{
						// 每一剑开始时，命中名单清零（交给 localNPCHitCooldown），换一道声音
						if (k > 0)
							SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.4f + 0.15f * k }, Projectile.Center);
					}
					break;
				}
				default:
				{
					Vector2 to = owner.MountedCenter - Projectile.Center;
					float speed = MathHelper.Min(ReturnSpeed, Projectile.velocity.Length() + 2f);
					Projectile.velocity = to.SafeNormalize(Vector2.UnitX) * speed;
					if (to.Length() < ReturnSpeed + 8f)
					{
						owner.GetModPlayer<SwordPlayer>().FlightCooldown = Items.Clearmoon.FlightCooldown;
						Projectile.Kill();
						return;
					}
					break;
				}
			}
			if (Projectile.velocity.LengthSquared() > 1f)
				Projectile.rotation = Projectile.velocity.ToRotation();
			Timer++;

			if (!Main.dedServ && Main.rand.NextBool(2))
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, -Projectile.velocity * 0.05f, 120, default, 0.8f);
				d.noGravity = true;
			}
		}

		// 飞去、飞回时不伤人：剑只在落点连斩
		public override bool? CanHitNPC(NPC target) => Mode == 1 ? null : false;

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2[] pts = Trail.Centers(Projectile);
			float glow = Mode == 1 ? 1f : 0.55f;
			Trail.Draw(pts, p => 22f * (1f - p), p => Fx.Teal * (0.45f * glow * (1f - p)), Trail.Glow.Value, additive: true);
			Trail.Draw(pts, p => 6f * (1f - p), p => Color.White * (0.8f * glow * (1f - p)), Trail.Glow.Value, additive: true);

			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.6f),
				Projectile.rotation + MathHelper.PiOver4, tex.Size() / 2f, SwordScale, SpriteEffects.None, 0f);
			return false;
		}
	}
}
