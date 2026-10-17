using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Projectiles
{
	/// <summary>
	/// 罡气护体：被击中后身边浮起的小青剑，绕身旋转，碰到的敌人会被斩伤。
	/// ai[0] = 第几把（决定起始角度），ai[1] = 一共几把。
	/// </summary>
	public class GuardSword : ModProjectile
	{
		public const int Duration = 180;
		private const float Radius = 58f;
		private const float Spin = 0.09f;

		public override string Texture => "QingyiJianxian/Content/Items/Clearmoon";

		private ref float Age => ref Projectile.localAI[0];

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 6;
			ProjectileID.Sets.TrailingMode[Type] = 0;
		}

		public override void SetDefaults()
		{
			Projectile.width = 26;
			Projectile.height = 26;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
			Projectile.timeLeft = Duration;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead)
			{
				Projectile.Kill();
				return;
			}
			float angle = MathHelper.TwoPi * Projectile.ai[0] / Math.Max(Projectile.ai[1], 1f) + Age * Spin;
			// 出现时从身上飞出，消失前收回
			float grow = MathHelper.Clamp(Age / 10f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
			Projectile.Center = owner.MountedCenter + angle.ToRotationVector2() * Radius * grow;
			Projectile.rotation = angle + MathHelper.PiOver2; // 剑身顺着旋转方向
			Projectile.velocity = Vector2.Zero;
			Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.45f);
			Age++;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2[] pts = Trail.Centers(Projectile);
			Trail.Draw(pts, p => 10f * (1f - p), p => Fx.Teal * 0.6f * (1f - p), Trail.Glow.Value, additive: true);
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.Lerp(Color.White, Fx.PaleTeal, 0.4f) * 0.9f * fade,
				Projectile.rotation + MathHelper.PiOver4, tex.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
			return false;
		}
	}
}
