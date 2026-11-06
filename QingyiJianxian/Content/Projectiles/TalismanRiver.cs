using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Projectiles
{
	/// <summary>
	/// 清流剑符放出的一道剑河：从身边起势，朝一侧水平碾过去的白玉色湍流。
	///
	/// 范围有限：高 7 格（112 像素）、往前 50 格（800 像素），约 0.6 秒碾完；每个敌人只斩一次，最多斩 12 个，对 Boss 打七折；
	/// 被斩到的敌人被冲向剑河流去的方向。
	///
	/// 和地形是一层：出手时按列量出每一处的上下边界——
	///   贴着地面的一侧被地面压住，气流顺着地形起伏，接触线上翻起白沫、溅起水雾；
	///   碰到天花板同理；
	///   整列都是实心（撞上墙）就停在那里，浪头碎在墙上。
	/// 每一列的边界在各个客户端上按同一块地形算出来，联机一致。
	///
	/// 画面：五到七缕气流各自翻滚、交缠着往前流；几十柄小剑影顺流疾射；最前面是卷起的浪头，浪尖一排剑光；
	/// 上下沿一抹淡墨；碾过去的地方从起点开始慢慢淡成薄雾。
	/// ai[0] = 方向（±1），ai[1] = 河道中线的高度（世界 Y）。Center.X = 起点。
	/// </summary>
	public class TalismanRiver : ModProjectile
	{
		public const float Reach = 800f;
		public const float HalfHeight = 56f;
		private const float Step = 8f;
		private const int Columns = (int)(Reach / Step) + 1;
		private const int FlowFrames = 36;
		private const int Linger = 30;
		private const int MaxTargets = 12;
		private const int Strands = 7;
		private const int SwordCount = 22;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private int Dir => Projectile.ai[0] >= 0 ? 1 : -1;
		private float LaneY => Projectile.ai[1];
		private ref float Age => ref Projectile.localAI[0];
		private ref float Hits => ref Projectile.localAI[1];

		private float startX;
		private int count = -1;           // 河道有多少列（撞墙就短）
		private bool walled;              // 是不是撞墙停下的
		private bool crashed;
		private readonly float[] top = new float[Columns];
		private readonly float[] bottom = new float[Columns];
		private readonly bool[] floor = new bool[Columns];   // 这一列下沿被地面压住
		private readonly bool[] ceiling = new bool[Columns];

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Generic;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.timeLeft = FlowFrames + Linger + 14; // 最后一列也要淡完
		}

		private float X(int i) => startX + Dir * i * Step;

		/// <summary>量河道：每一列从中线出发，往上、往下各找到实心方块或河道边界为止。</summary>
		private void Survey()
		{
			startX = Projectile.Center.X;
			count = Columns;
			for (int i = 0; i < Columns; i++)
			{
				float x = X(i);
				float lo = LaneY - HalfHeight, hi = LaneY + HalfHeight;
				// 中线埋在地里：先往上、再往下找一处空的
				float p = LaneY;
				if (Fx.Solid(new Vector2(x, p)))
				{
					bool found = false;
					for (float y = LaneY; y >= lo && !found; y -= 4f)
						if (!Fx.Solid(new Vector2(x, y))) { p = y; found = true; }
					for (float y = LaneY; y <= hi && !found; y += 4f)
						if (!Fx.Solid(new Vector2(x, y))) { p = y; found = true; }
					if (!found)
					{
						count = i;
						walled = true;
						break;
					}
				}
				float t = p, b = p;
				while (t - 4f >= lo && !Fx.Solid(new Vector2(x, t - 4f)))
					t -= 4f;
				while (b + 4f <= hi && !Fx.Solid(new Vector2(x, b + 4f)))
					b += 4f;
				if (b - t < 20f)
				{
					// 只剩一条缝，过不去
					count = i;
					walled = true;
					break;
				}
				top[i] = t;
				bottom[i] = b;
				ceiling[i] = t - 4f >= lo;
				floor[i] = b + 4f <= hi;
			}
			// 边界略微抹平，气流顺滑地爬坡
			for (int pass = 0; pass < 2; pass++)
			{
				for (int i = 1; i < count - 1; i++)
				{
					top[i] = MathHelper.Lerp(top[i], (top[i - 1] + top[i + 1]) / 2f, 0.5f);
					bottom[i] = MathHelper.Lerp(bottom[i], (bottom[i - 1] + bottom[i + 1]) / 2f, 0.5f);
				}
			}
			count = Math.Max(count, 1);
		}

		/// <summary>浪头到了第几列（可以是小数）。</summary>
		private float Front => MathHelper.Min(count - 1, Age / FlowFrames * (Columns - 1));

		/// <summary>第 i 列的浓淡：浪头刚过时最浓，过了一会儿从起点开始淡去。</summary>
		private float ColumnAlpha(float i)
		{
			float passed = Age - i / (Columns - 1) * FlowFrames;
			float a = 1f - MathHelper.Clamp((passed - 12f) / Linger, 0f, 1f);
			return passed < 0 ? 0f : a;
		}

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			if (count < 0)
			{
				Survey();
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 0.9f }, Projectile.Center);
				SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.2f }, Projectile.Center);
			}

			int f = (int)Front;
			if (!Main.dedServ)
			{
				float mid = (top[f] + bottom[f]) / 2f;
				Lighting.AddLight(new Vector2(X(f), mid), 0.5f, 0.85f, 0.8f);
				for (int i = 0; i < f; i += 6)
				{
					float a = ColumnAlpha(i);
					if (a > 0.05f)
						Lighting.AddLight(new Vector2(X(i), (top[i] + bottom[i]) / 2f), 0.2f * a, 0.38f * a, 0.36f * a);
				}
				if (Age < FlowFrames)
				{
					// 浪头：贴地处翻起水雾，浪尖洒光屑
					if (floor[f])
						for (int k = 0; k < 2; k++)
							Fx.Mist(new Vector2(X(f), bottom[f] - 2f), new Vector2(Dir * Main.rand.NextFloat(1f, 3.5f), -Main.rand.NextFloat(0.5f, 2.5f)), 1.3f);
					if (ceiling[f] && Main.rand.NextBool(2))
						Fx.Mist(new Vector2(X(f), top[f] + 2f), new Vector2(Dir * Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(0.3f, 1.5f)), 1.1f);
					for (int k = 0; k < 2; k++)
						Fx.Mote(new Vector2(X(f), MathHelper.Lerp(top[f], bottom[f], Main.rand.NextFloat())), new Vector2(Dir * Main.rand.NextFloat(1f, 4f), -Main.rand.NextFloat(0.5f, 2f)), Main.rand.NextFloat(0.6f, 1f));
					// 河身贴地的地方零星翻起白沫
					int j = Main.rand.Next(Math.Max(f, 1));
					if (floor[j] && ColumnAlpha(j) > 0.3f)
						Fx.Mist(new Vector2(X(j), bottom[j] - 2f), new Vector2(Dir * 1.2f, -0.8f), 0.9f);
				}
				// 碾过去的地方留一层薄雾
				if (Age > 10 && Main.rand.NextBool(2))
				{
					int j = Main.rand.Next(Math.Max(f, 1));
					if (ColumnAlpha(j) is > 0.1f and < 0.7f)
						Fx.Mist(new Vector2(X(j), MathHelper.Lerp(top[j], bottom[j], Main.rand.NextFloat())), new Vector2(Dir * 0.5f, -0.3f), 1.4f);
				}
			}

			// 撞墙：浪头碎在墙上
			if (walled && !crashed && Front >= count - 1)
			{
				crashed = true;
				Vector2 at = new(X(count - 1), (top[count - 1] + bottom[count - 1]) / 2f);
				SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.3f, Volume = 0.8f }, at);
				if (!Main.dedServ)
				{
					for (int k = 0; k < 22; k++)
					{
						Vector2 p = new(at.X, MathHelper.Lerp(top[count - 1], bottom[count - 1], Main.rand.NextFloat()));
						Fx.Mist(p, new Vector2(-Dir * Main.rand.NextFloat(0.5f, 4f), Main.rand.NextFloat(-4f, 1.5f)), 1.5f);
						if (k % 2 == 0)
							Fx.Mote(p, new Vector2(-Dir * Main.rand.NextFloat(1f, 5f), Main.rand.NextFloat(-4f, 1f)), 0.9f);
					}
				}
			}
			Age++;
		}

		public override bool? CanHitNPC(NPC target) => count > 0 && Age < FlowFrames + 6 && Hits < MaxTargets ? null : false;

		public override bool? Colliding(Rectangle projHitbox, Rectangle target)
		{
			if (count <= 0)
				return false;
			// 敌人覆盖的那几列里，只要有一列在浪头之后、并且上下和河道重叠，就斩到
			float front = Front;
			int a = (int)((target.Left - startX) * Dir / Step), b = (int)((target.Right - startX) * Dir / Step);
			int lo = Math.Max(0, Math.Min(a, b)), hi = Math.Min((int)front, Math.Max(a, b));
			for (int i = lo; i <= hi; i++)
			{
				if (target.Bottom >= top[i] && target.Top <= bottom[i])
					return true;
			}
			return false;
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (target.boss)
				modifiers.SourceDamage *= 0.7f;
			modifiers.HitDirectionOverride = Dir;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			Hits++;
			QiBloom.Spawn(Projectile, target.Center, 70f);
			// 被湍流冲走
			if (!target.boss && target.knockBackResist > 0f)
				target.velocity.X += Dir * 5f * target.knockBackResist;
		}

		private static float Hash(int i, int salt)
		{
			uint x = (uint)(i * 374761393 + salt * 668265263);
			x = (x ^ (x >> 13)) * 1274126177u;
			return ((x ^ (x >> 16)) & 0xFFFF) / 65535f;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (count <= 0)
				return false;
			SpriteBatch sb = Main.spriteBatch;
			float front = Front;
			int f = (int)front;
			if (f < 1)
				return false;
			float time = Main.GlobalTimeWrappedHourly;

			// 从浪头往回取点（Trail 要求头在前），每两列一个点
			int n = f / 2 + 1;
			var center = new Vector2[n];
			var edgeTop = new Vector2[n];
			var edgeBottom = new Vector2[n];
			var colIndex = new int[n];
			for (int k = 0; k < n; k++)
			{
				int i = Math.Max(f - k * 2, 0);
				colIndex[k] = i;
				center[k] = new Vector2(X(i), (top[i] + bottom[i]) / 2f);
				edgeTop[k] = new Vector2(X(i), top[i]);
				edgeBottom[k] = new Vector2(X(i), bottom[i]);
			}
			int Col(float p) => colIndex[Math.Clamp((int)MathF.Round(p * (n - 1)), 0, n - 1)];
			float Height(float p) { int i = Col(p); return bottom[i] - top[i]; }
			float Alpha(float p) => ColumnAlpha(Col(p));

			// 上下沿一抹淡墨（不发光，和场景是一层）
			Trail.Draw(edgeTop, p => 16f, p => Fx.Ink * (0.22f * Alpha(p)), Trail.Glow.Value);
			Trail.Draw(edgeBottom, p => 16f, p => Fx.Ink * (0.22f * Alpha(p)), Trail.Glow.Value);

			// 河身：一层流动的青气，铺满上下边界
			// 河身：一层半透明的深青水色（普通混合，白天也有分量），上面一层淡青气纹往前流
			Trail.Draw(center, p => Height(p) * 1.1f, p => Fx.DeepTeal * (0.45f * Alpha(p)), Trail.Glow.Value);
			Trail.Draw(center, p => Height(p) * 1.0f, p => Fx.PaleTeal * (0.7f * Alpha(p)), Trail.Qi.Value, uRepeat: n / 10f, uScroll: -time * 2.5f);
			Trail.Draw(center, p => Height(p) * 0.6f, p => Fx.PaleTeal * (0.12f * Alpha(p)), Trail.Glow.Value, additive: true);

			// 气流：几缕各自翻滚、交缠着往前流
			for (int s = 0; s < Strands; s++)
			{
				float frac = (s + 0.5f) / Strands;
				float phase = s * 1.37f;
				var pts = new Vector2[n];
				for (int k = 0; k < n; k++)
				{
					int i = colIndex[k];
					float h = bottom[i] - top[i];
					float wave = MathF.Sin(time * 9f - X(i) * 0.025f * Dir + phase) * h * 0.13f
						+ MathF.Sin(time * 5f - X(i) * 0.011f * Dir + phase * 2f) * h * 0.07f;
					float y = MathHelper.Lerp(top[i] + 6f, bottom[i] - 6f, frac) + wave;
					pts[k] = new Vector2(X(i), MathHelper.Clamp(y, top[i] + 3f, bottom[i] - 3f));
				}
				bool jade = s % 2 == 0;
				float w = jade ? 7f : 12f;
				float Wd(float p) => w * (0.5f + 0.5f * Height(p) / (HalfHeight * 2f)) * (0.6f + 0.4f * (1f - p));
				// 每缕先垫一道深青，再画白玉 / 淡青，丝缕有轮廓，不会被亮背景吃掉
				Trail.Draw(pts, p => Wd(p) + 4f, p => Fx.DeepTeal * (0.35f * Alpha(p)), Trail.Glow.Value);
				Trail.Draw(pts, Wd, p => (jade ? Fx.Jade : Fx.PaleTeal) * ((jade ? 0.85f : 0.7f) * Alpha(p)),
					jade ? Trail.Glow.Value : Trail.Qi.Value, uRepeat: jade ? 1f : n / 12f, uScroll: jade ? 0f : -time * 3.5f + s);
			}

			// 贴地处的白沫：接触线上一道起伏的白
			for (int k = 0; k < n - 1; k++)
			{
				int i = colIndex[k], j = colIndex[k + 1];
				float a = ColumnAlpha(i);
				if (a <= 0.02f)
					continue;
				float foam = 0.5f + 0.5f * MathF.Sin(time * 12f + i * 0.9f);
				if (floor[i] && floor[j])
					Fx.Line(sb, new Vector2(X(i), bottom[i] - 1f), new Vector2(X(j), bottom[j] - 1f), Fx.Jade * (0.85f * a * (0.5f + 0.5f * foam)), 3f + 2f * foam);
				if (ceiling[i] && ceiling[j])
					Fx.Line(sb, new Vector2(X(i), top[i] + 1f), new Vector2(X(j), top[j] + 1f), Fx.Jade * (0.4f * a * foam), 2f);
			}

			// 剑影：顺流疾射，比浪头还快一点，追到浪头为止
			for (int k = 0; k < SwordCount; k++)
			{
				float delay = Hash(k, 1) * FlowFrames * 0.8f;
				float speed = (Columns - 1) / (float)FlowFrames * MathHelper.Lerp(1.25f, 1.7f, Hash(k, 2));
				float col = (Age - delay) * speed;
				if (col < 0f)
					continue;
				col = MathHelper.Min(col, front);
				int i = (int)col;
				float a = ColumnAlpha(i) * MathHelper.Clamp((Age - delay) / 4f, 0f, 1f);
				if (a <= 0.05f || i >= count)
					continue;
				float y = MathHelper.Lerp(top[i] + 8f, bottom[i] - 8f, Hash(k, 3));
				Vector2 head = new(X(i), y);
				Vector2 tail = head - new Vector2(Dir * MathHelper.Lerp(18f, 34f, Hash(k, 4)), 0f);
				Fx.Line(sb, tail, head, Fx.DeepTeal * (0.7f * a), 5f);
				Fx.Line(sb, tail, head, Fx.Jade * a, 2f);
			}

			// 浪头：卷起来的一道浪，浪尖一排剑光（流完就散）
			if (Age < FlowFrames + 8 && !(walled && crashed && Age > FlowFrames))
			{
				float fa = 1f - MathHelper.Clamp((Age - FlowFrames) / 8f, 0f, 1f);
				float xf = X(f), t0 = top[f], b0 = bottom[f], h = b0 - t0;
				// 浪身：从下往上、往前卷
				const int m = 12;
				var curl = new Vector2[m];
				for (int k = 0; k < m; k++)
				{
					float u = k / (m - 1f);                       // 0 = 浪尖（头）
					float ang = MathHelper.Lerp(-0.4f, 2.4f, u);    // 绕一个圆心卷
					Vector2 c = new(xf - Dir * h * 0.28f, t0 + h * 0.42f);
					curl[k] = c + new Vector2(Dir * MathF.Cos(ang), -MathF.Sin(ang) * 0.9f) * h * 0.42f * (1f - 0.25f * u)
						+ new Vector2(0f, u * u * h * 0.35f);
				}
				float cw = MathF.Min(1f, h / 80f);
				Trail.Draw(curl, p => 30f * (1f - 0.6f * p) * cw, p => Fx.DeepTeal * (0.55f * fa * (1f - 0.5f * p)), Trail.Glow.Value);
				Trail.Draw(curl, p => 26f * (1f - 0.6f * p) * cw, p => Fx.PaleTeal * (0.85f * fa * (1f - 0.5f * p)), Trail.Qi.Value, uRepeat: 1f, uScroll: -time * 4f);
				Trail.Draw(curl, p => 7f * (1f - 0.7f * p) + 1f, p => Fx.Jade * (fa * (1f - 0.6f * p)), Trail.Glow.Value);
				// 浪尖一排剑光：竖着的一线白玉，上面几点闪
				Fx.Line(sb, new Vector2(xf, t0 + 4f), new Vector2(xf, b0 - 4f), Fx.DeepTeal * (0.5f * fa), 6f);
				Fx.Line(sb, new Vector2(xf, t0 + 4f), new Vector2(xf, b0 - 4f), Fx.Jade * (0.9f * fa), 3f);
				for (int k = 0; k < 4; k++)
				{
					float y = MathHelper.Lerp(t0 + 10f, b0 - 10f, (k + Hash((int)Age / 3, k) * 0.6f) / 4f);
					float g = 6f + 6f * Hash((int)Age, k + 9);
					Vector2 at = new(xf + Dir * 4f, y);
					Fx.Line(sb, at - new Vector2(g, 0f), at + new Vector2(g, 0f), Color.White * (0.8f * fa), 1.5f);
					Fx.Line(sb, at - new Vector2(0f, g * 0.6f), at + new Vector2(0f, g * 0.6f), Color.White * (0.6f * fa), 1.5f);
				}
			}
			return false;
		}
	}
}
