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
	/// 剑阵 / The Bladeward Ring: swords stand in a ring around where the player was, all pointing inward,
	/// with one gap; then they close in. Get out through the gap before the ring shuts.
	/// ai[0], ai[1] = ring center; ai[2] = ticks before closing in.
	/// </summary>
	public class RingSword : ModProjectile
	{
		public const int Slots = 18;
		public const float Radius = 560f;
		private const float MaxSpeed = 15f;
		private const int WarnTime = 30;

		public override string Texture => "QingyiJianxian/Content/NPCs/SwordImmortal/Azurebrand";

		private ref float Age => ref Projectile.localAI[0];
		private Vector2 RingCenter => new(Projectile.ai[0], Projectile.ai[1]);
		private int CloseAt => (int)Projectile.ai[2];

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 6;
			ProjectileID.Sets.TrailingMode[Type] = 0;
		}

		public override void SetDefaults()
		{
			Projectile.width = 22;
			Projectile.height = 22;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 600;
		}

		public override void AI()
		{
			Vector2 inward = Projectile.DirectionTo(RingCenter);
			if (Age < CloseAt)
			{
				// 立阵：剑尖朝内，微微颤动
				Projectile.velocity = Vector2.Zero;
				Projectile.rotation = inward.ToRotation() + MathHelper.PiOver4 + MathF.Sin(Age * 0.5f + Projectile.identity) * 0.04f;
			}
			else
			{
				if (Age == CloseAt)
					Projectile.timeLeft = 130;
				// 合阵：越收越快，穿过圆心继续飞出去
				float speed = MathF.Min(MaxSpeed, (Age - CloseAt + 1) * 0.5f);
				if (Projectile.velocity == Vector2.Zero)
					Projectile.velocity = inward;
				Projectile.velocity = Projectile.velocity.SafeNormalize(inward) * speed;
				Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
			}
			Age++;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			Texture2D tex = TextureAssets.Projectile[Type].Value;

			if (Age < CloseAt && Age > CloseAt - WarnTime)
			{
				float t = (Age - (CloseAt - WarnTime)) / WarnTime;
				Vector2 inward = Projectile.DirectionTo(RingCenter);
				Fx.Line(sb, Projectile.Center, Projectile.Center + inward * Radius * 0.35f, Fx.Cinnabar * (0.15f + 0.4f * t), 1.5f);
			}
			if (Age >= CloseAt)
			{
				Vector2[] pts = FlyingSword.Centers(Projectile, Vector2.Zero);
				Trail.Draw(pts, p => 14f * (1f - p), p => Fx.Teal * 0.7f * (1f - p), Trail.Glow.Value, additive: true);
			}

			float fadeIn = MathHelper.Clamp(Age / 12f, 0f, 1f);
			sb.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White * fadeIn, Projectile.rotation, tex.Size() / 2f, 1f, SpriteEffects.None, 0f);
			return false;
		}
	}
}
