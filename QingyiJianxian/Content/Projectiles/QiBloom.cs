using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Projectiles
{
	/// <summary>
	/// 命中时溅开的一圈水墨晕：中间一团淡墨洇开，外面一圈淡青光，几点光屑。只是画面，不伤人。
	/// ai[0] = 大小（直径，像素）。
	/// </summary>
	public class QiBloom : ModProjectile
	{
		private const int Life = 26;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private ref float Age => ref Projectile.localAI[0];
		private float Size => Projectile.ai[0] > 0 ? Projectile.ai[0] : 60f;

		public static void Spawn(Projectile source, Vector2 at, float size)
		{
			if (source.owner == Main.myPlayer)
				Projectile.NewProjectile(source.GetSource_FromThis(), at, Vector2.Zero, ModContent.ProjectileType<QiBloom>(), 0, 0f, source.owner, size);
		}

		public override void SetDefaults()
		{
			Projectile.width = 8;
			Projectile.height = 8;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = Life;
		}

		public override void AI()
		{
			if (Age == 0)
			{
				for (int i = 0; i < 6; i++)
					Fx.Mote(Projectile.Center, Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY, 0.7f);
				Lighting.AddLight(Projectile.Center, 0.3f, 0.5f, 0.48f);
			}
			Age++;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			float t = Age / Life;
			float ease = 1f - (1f - t) * (1f - t);
			// 淡墨洇开：随环境光变暗变亮，和场景是一层
			Color ink = Color.Lerp(Fx.Ink, lightColor, 0.15f);
			Fx.Circle(sb, Fx.SoftGlow.Value, Projectile.Center, Size * (0.4f + 0.8f * ease), ink * (0.35f * (1f - t)));
			Fx.Circle(sb, Fx.Ring.Value, Projectile.Center, Size * (0.3f + 0.9f * ease), Fx.PaleTeal * (0.5f * (1f - t) * (1f - t)));
			return false;
		}
	}
}
