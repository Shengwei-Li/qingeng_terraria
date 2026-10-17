using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.NPCs.SwordImmortal
{
	/// <summary>
	/// 一笔画地 / One Brushstroke Bounds the Earth: a giant brush sweeps across the domain and paints everything
	/// above and below a narrow gap. The gap is the only safe place.
	/// ai[0] = gap center Y, ai[1] = sweep direction (±1), ai[2] = the stroke's start X.
	/// </summary>
	public class BrushSweep : ModProjectile
	{
		public const float GapHalf = 64f;      // 缺口半高：玩家 42 高，留一点余地
		public const float BandHeight = 760f;  // 缺口上下各画这么高
		public const float Length = 3000f;
		private const int WarnTime = 60;
		private const float SweepSpeed = 75f;
		private const int FadeTime = 30;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private ref float Age => ref Projectile.localAI[0];
		private float GapY => Projectile.ai[0];
		private float Dir => Projectile.ai[1];
		private float StartX => Projectile.ai[2];
		private float Painted => MathHelper.Clamp((Age - WarnTime) * SweepSpeed, 0f, Length);
		private bool Sweeping => Age >= WarnTime && Painted < Length;

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = WarnTime + (int)(Length / SweepSpeed) + FadeTime + 2;
		}

		public override void AI()
		{
			if (Age == WarnTime)
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.6f, Volume = 0.9f }, Main.LocalPlayer.Center);
			Age++;
			// 弹幕本体跟着笔锋走，方便声音和镜头
			Projectile.Center = new Vector2(StartX + Dir * Painted, GapY);

			// 笔锋过处墨点飞溅
			if (Sweeping && !Main.dedServ)
			{
				for (int i = 0; i < 6; i++)
				{
					float y = GapY + (Main.rand.NextBool() ? -1 : 1) * (GapHalf + Main.rand.NextFloat(BandHeight));
					Dust d = Dust.NewDustPerfect(new Vector2(Projectile.Center.X, y), DustID.Smoke,
						new Vector2(Dir * Main.rand.NextFloat(4f, 12f), Main.rand.NextFloat(-3f, 3f)), 40, Fx.Ink, Main.rand.NextFloat(1.4f, 2.6f));
					d.noGravity = true;
				}
			}
		}

		/// <summary>Hits anyone inside the painted bands (not the gap) while the brush is still moving.</summary>
		public override bool? Colliding(Rectangle projHitbox, Rectangle target)
		{
			if (!Sweeping)
				return false;
			float x0 = Math.Min(StartX, StartX + Dir * Painted), x1 = Math.Max(StartX, StartX + Dir * Painted);
			if (target.Right < x0 || target.Left > x1)
				return false;
			bool upper = target.Top < GapY - GapHalf && target.Bottom > GapY - GapHalf - BandHeight;
			bool lower = target.Bottom > GapY + GapHalf && target.Top < GapY + GapHalf + BandHeight;
			return upper || lower;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			var sb = Main.spriteBatch;
			float upperY = GapY - GapHalf - BandHeight / 2f;
			float lowerY = GapY + GapHalf + BandHeight / 2f;
			Vector2 a = new(StartX, 0f), b = new(StartX + Dir * Length, 0f);

			if (Age < WarnTime)
			{
				// 预警：要被画满的两片区域先洇上一层淡墨（越来越浓），缝用淡青填出来，上下两道朱砂实线框住
				float t = Age / WarnTime;
				float pulse = 0.75f + 0.25f * MathF.Sin(Age * 0.35f);
				Color wash = Fx.Ink * (0.05f + 0.12f * t) * pulse;
				Fx.Line(sb, a + Vector2.UnitY * upperY, b + Vector2.UnitY * upperY, wash, BandHeight);
				Fx.Line(sb, a + Vector2.UnitY * lowerY, b + Vector2.UnitY * lowerY, wash, BandHeight);
				Fx.Line(sb, a + Vector2.UnitY * GapY, b + Vector2.UnitY * GapY, Fx.Teal * (0.1f + 0.12f * t), GapHalf * 2f);
				DrawGapEdges(sb, a, b, Fx.Cinnabar * (0.4f + 0.5f * t), 3f);
				return false;
			}

			// 一笔浓墨：笔锋在前，从起点一路刷过去；刷完慢慢淡去
			float fade = 1f - MathHelper.Clamp((Age - WarnTime - Length / SweepSpeed) / FadeTime, 0f, 1f);
			float head = Painted;
			const int n = 10;
			var top = new Vector2[n];
			var bottom = new Vector2[n];
			for (int i = 0; i < n; i++)
			{
				float x = StartX + Dir * head * (1f - i / (float)(n - 1));
				top[i] = new Vector2(x, upperY);
				bottom[i] = new Vector2(x, lowerY);
			}
			Trail.Draw(top, p => BandHeight * (1f - 0.08f * p), p => Fx.Ink * 0.95f * fade, Trail.Brush.Value);
			Trail.Draw(bottom, p => BandHeight * (1f - 0.08f * p), p => Fx.Ink * 0.95f * fade, Trail.Brush.Value);
			// 缝的上下沿画实：看到哪里就是哪里，不会出现"边缘看着安全却挨打"
			Vector2 painted = new(StartX + Dir * head, 0f);
			DrawGapEdges(sb, a, painted, Fx.Ink * fade, 6f);
			// 笔锋：一团浓墨
			Fx.Circle(sb, Fx.SoftGlow.Value, new Vector2(painted.X, upperY), BandHeight * 0.9f, Fx.Ink * 0.5f * fade);
			Fx.Circle(sb, Fx.SoftGlow.Value, new Vector2(painted.X, lowerY), BandHeight * 0.9f, Fx.Ink * 0.5f * fade);
			return false;
		}

		private void DrawGapEdges(SpriteBatch sb, Vector2 from, Vector2 to, Color color, float thickness)
		{
			Fx.Line(sb, from + Vector2.UnitY * (GapY - GapHalf), to + Vector2.UnitY * (GapY - GapHalf), color, thickness);
			Fx.Line(sb, from + Vector2.UnitY * (GapY + GapHalf), to + Vector2.UnitY * (GapY + GapHalf), color, thickness);
		}
	}
}
