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
	/// 剑河 / River of Blades: one sword in a wave of rows that flows across the screen along a shared sine path,
	/// so the gap left between the rows drifts up and down and the player has to follow it.
	/// ai[0] = row's base Y, ai[1] = horizontal direction (±1).
	/// </summary>
	public class RiverSword : ModProjectile
	{
		public const float Speed = 26f;
		public const float Freq = 0.0045f;   // 沿行进距离的正弦频率（弧度/像素）
		public const float Amp = 70f;        // 河道上下摆动的幅度
		public const float Spacing = 44f;    // 行距
		public const int RowsHalf = 8;       // 河宽：中心上下各 8 行
		public const int GapHalf = 1;        // 缺口：3 行宽
		public const float StartOffset = 1100f;

		public override string Texture => "QingyiJianxian/Content/NPCs/SwordImmortal/Azurebrand";

		private ref float Age => ref Projectile.localAI[0];

		/// <summary>Y of a river row after travelling the given distance. Shared with the boss's telegraph.</summary>
		public static float RowY(float baseY, float travelled) => baseY + MathF.Sin(travelled * Freq) * Amp;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 6;
			ProjectileID.Sets.TrailingMode[Type] = 0;
		}

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = (int)(StartOffset * 2.2f / Speed);
		}

		public override void AI()
		{
			float dir = Projectile.ai[1];
			float travelled = Age * Speed;
			float y = RowY(Projectile.ai[0], travelled);
			float nextY = RowY(Projectile.ai[0], travelled + Speed);
			Projectile.velocity = new Vector2(dir * Speed, nextY - y);
			Projectile.Center = new Vector2(Projectile.Center.X, y);
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
			Age++;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2[] pts = FlyingSword.Centers(Projectile, Vector2.Zero);
			Trail.Draw(pts, p => 12f * (1f - p), p => Fx.Teal * 0.6f * (1f - p), Trail.Glow.Value, additive: true);
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, tex.Size() / 2f, 0.8f, SpriteEffects.None, 0f);
			return false;
		}
	}
}
