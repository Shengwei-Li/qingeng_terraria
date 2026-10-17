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
	/// 永字八法 / The Eight Strokes of Eternity: 剑仙以剑为笔，在天地间写一个巨大的"永"字，玩家就站在字里。
	/// 字有两千像素见方，铺满整个领域（领域边界会把人推回来，没有字外可躲）。
	/// 一笔一个弹幕：先洇出淡墨的笔路（预警），再一笔写过去。写过的笔画不消失，一直伤人到整个字写完，
	/// 场地被墨痕越切越碎——要在字的留白里找路。前一笔落下时，下一笔的笔路已经洇出来，要读笔顺躲。
	/// Center = 字的中心；ai[0] = 第几笔（0-7），ai[1] = 这一笔开始洇出的延迟。
	/// </summary>
	public class CalligraphyStroke : ModProjectile
	{
		public const float Size = 2000f;     // 字有多大（边长），铺满领域
		public const int StrokeGap = 18;      // 相邻两笔的间隔
		private const int WarnTime = 26;
		private const int WriteTime = 10;
		private const int HoldAfter = 30;     // 最后一笔写完后，整个字再停留这么久
		private const int FadeTime = 20;
		private const float WidthScale = 1.6f;

		/// <summary>整个字写完、所有墨痕一起淡去的时刻（各笔共用，从这一批弹幕生成算起）。</summary>
		public const int EndAt = (StrokeCount - 1) * StrokeGap + WarnTime + WriteTime + HoldAfter;

		// 永字八法：侧（点）、勒（横）、努（竖）、趯（钩）、策（提）、掠（长撇）、啄（短撇）、磔（捺）。
		// 坐标在 0..1 的方格里（y 向下），每笔是起笔到收笔的几个点；粗细是起笔和收笔处的宽度
		private static readonly (Vector2[] path, float startWidth, float endWidth)[] Strokes =
		{
			(new[] { new Vector2(0.45f, 0.04f), new Vector2(0.53f, 0.14f) }, 90f, 60f),                                // 侧
			(new[] { new Vector2(0.30f, 0.25f), new Vector2(0.43f, 0.24f), new Vector2(0.57f, 0.22f) }, 60f, 70f),    // 勒
			(new[] { new Vector2(0.57f, 0.22f), new Vector2(0.57f, 0.55f), new Vector2(0.56f, 0.86f) }, 72f, 64f),    // 努
			(new[] { new Vector2(0.56f, 0.86f), new Vector2(0.50f, 0.82f), new Vector2(0.45f, 0.79f) }, 64f, 20f),    // 趯
			(new[] { new Vector2(0.20f, 0.49f), new Vector2(0.30f, 0.45f), new Vector2(0.41f, 0.40f) }, 64f, 34f),    // 策
			(new[] { new Vector2(0.41f, 0.40f), new Vector2(0.30f, 0.60f), new Vector2(0.12f, 0.82f) }, 64f, 18f),    // 掠
			(new[] { new Vector2(0.80f, 0.31f), new Vector2(0.72f, 0.39f), new Vector2(0.64f, 0.46f) }, 64f, 26f),    // 啄
			(new[] { new Vector2(0.62f, 0.46f), new Vector2(0.76f, 0.64f), new Vector2(0.93f, 0.86f) }, 40f, 90f),    // 磔
		};
		public const int StrokeCount = 8;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private ref float Age => ref Projectile.localAI[0];
		private int Index => (int)Projectile.ai[0];
		private int Delay => (int)Projectile.ai[1];
		private float Local => Age - Delay;              // 这一笔自己的时间
		private bool Warning => Local >= 0 && Local < WarnTime;
		private float WriteProgress => MathHelper.Clamp((Local - WarnTime) / WriteTime, 0f, 1f);
		private bool Harmful => Local >= WarnTime && Age < EndAt;
		private Vector2 charCenter;

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 2;
		}

		public override void AI()
		{
			if (Age == 0)
			{
				charCenter = Projectile.Center;
				Projectile.timeLeft = EndAt + FadeTime + 2;
			}
			Projectile.Center = charCenter;
			Projectile.velocity = Vector2.Zero;
			if (Local == WarnTime && !Main.dedServ)
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f + 0.08f * Index, Volume = 0.7f }, PointAt(0.5f));
			if (Harmful && WriteProgress < 1f && !Main.dedServ)
			{
				Vector2 head = PointAt(WriteProgress);
				for (int i = 0; i < 3; i++)
				{
					Dust d = Dust.NewDustPerfect(head + Main.rand.NextVector2Circular(24f, 24f), DustID.Smoke, Main.rand.NextVector2Circular(3f, 3f), 40, Fx.Ink, 1.8f);
					d.noGravity = true;
				}
			}
			Age++;
		}

		/// <summary>World position along this stroke, t = 0 (起笔) .. 1 (收笔).</summary>
		private Vector2 PointAt(float t)
		{
			Vector2[] path = Strokes[Index].path;
			float f = t * (path.Length - 1);
			int i = Math.Min((int)f, path.Length - 2);
			Vector2 p = Vector2.Lerp(path[i], path[i + 1], f - i);
			return charCenter + (p - new Vector2(0.5f)) * Size;
		}

		private float WidthAt(float t) => MathHelper.Lerp(Strokes[Index].startWidth, Strokes[Index].endWidth, t) * WidthScale;

		public override bool? Colliding(Rectangle projHitbox, Rectangle target)
		{
			if (!Harmful)
				return false;
			// 只算已经写出来的那一段；每段用起笔到收笔的平均宽度
			const int segments = 6;
			float end = WriteProgress;
			for (int i = 0; i < segments; i++)
			{
				float t0 = end * i / segments, t1 = end * (i + 1) / segments;
				float point = 0f;
				if (Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), PointAt(t0), PointAt(t1), WidthAt((t0 + t1) / 2f) * 0.8f, ref point))
					return true;
			}
			return false;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Local < 0)
				return false;
			const int n = 10;
			var pts = new Vector2[n];
			if (Warning)
			{
				// 笔路：淡墨洇出整笔的形状，越来越浓
				for (int i = 0; i < n; i++)
					pts[i] = PointAt(1f - i / (float)(n - 1));
				float t = Local / WarnTime;
				Trail.Draw(pts, p => WidthAt(1f - p), p => Fx.Ink * (0.06f + 0.14f * t), Trail.Glow.Value);
				return false;
			}
			// 写：笔锋在前，墨痕在后；写完停留，然后淡去
			float written = WriteProgress;
			for (int i = 0; i < n; i++)
				pts[i] = PointAt(written * (1f - i / (float)(n - 1)));
			float fade = 1f - MathHelper.Clamp((Age - EndAt) / FadeTime, 0f, 1f);
			Trail.Draw(pts, p => WidthAt(written * (1f - p)), p => Fx.Ink * 0.95f * fade, Trail.Brush.Value);
			return false;
		}
	}
}
