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
	/// 万剑悬空 / Ten Thousand Swords Hang in Heaven: an ink sword hanging in the blank sky, point down.
	/// A red line marks its column, then it drops and shatters into ink on the ground.
	/// ai[0] = ticks before it drops.
	/// </summary>
	public class InkSword : ModProjectile
	{
		private const int WarnTime = 30;
		private const float MaxFall = 26f;

		public override string Texture => "QingyiJianxian/Content/NPCs/SwordImmortal/Azurebrand";

		private ref float Age => ref Projectile.localAI[0];
		private int DropAt => (int)Projectile.ai[0];
		private bool Falling => Age >= DropAt;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 5;
			ProjectileID.Sets.TrailingMode[Type] = 0;
		}

		public override void SetDefaults()
		{
			Projectile.width = 18;
			Projectile.height = 18;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false; // 悬空时不碰地形，落下后再开
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 600;
		}

		public override bool CanHitPlayer(Player target) => Falling;

		public override void AI()
		{
			Projectile.rotation = MathHelper.PiOver2 + MathHelper.PiOver4; // 剑尖朝下
			if (Falling)
			{
				Projectile.tileCollide = Age > DropAt + 6;
				Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + 1.6f, MaxFall);
				if (Age == DropAt)
					Projectile.timeLeft = 120;
			}
			else
			{
				// 悬着时微微颤动
				Projectile.velocity = new Vector2(0f, (float)System.Math.Sin((Age + Projectile.identity * 7) * 0.2f) * 0.3f);
			}
			Age++;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.netMode == NetmodeID.Server)
				return;
			for (int i = 0; i < 10; i++)
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, Main.rand.NextVector2Circular(4f, 3f) - Vector2.UnitY * 2f, 60, Fx.Ink, 1.8f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			Texture2D tex = TextureAssets.Projectile[Type].Value;

			if (!Falling && Age > DropAt - WarnTime)
			{
				float t = (Age - (DropAt - WarnTime)) / WarnTime;
				Fx.Line(sb, Projectile.Center, Projectile.Center + Vector2.UnitY * 1100f, Fx.Cinnabar * (0.15f + 0.55f * t), 1f + t * 2f);
			}
			if (Falling)
			{
				Vector2[] pts = FlyingSword.Centers(Projectile, Vector2.Zero);
				Trail.Draw(pts, p => 16f * (1f - p), p => Fx.Ink * 0.7f * (1f - p), Trail.Brush.Value);
			}

			float fadeIn = MathHelper.Clamp(Age / 20f, 0f, 1f);
			// 墨剑：整把剑染成墨色
			sb.Draw(tex, Projectile.Center - Main.screenPosition, null, new Color(28, 32, 40) * fadeIn, Projectile.rotation, tex.Size() / 2f, 1f, SpriteEffects.None, 0f);
			return false;
		}
	}
}
