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
	/// 清流剑符放出的一道剑河：从身边起势，朝点下的方向（任意角度）碾过去的白玉色湍流。
	///
	/// 范围有限：宽 7 格（112 像素）、往前 50 格（800 像素），约 0.6 秒碾完；每个敌人只斩一次，最多斩 12 个，对 Boss 打七折；
	/// 被斩到的敌人被冲向剑河流去的方向。
	///
	/// 坐标：沿流向为 s（列号 × Step），横穿流向为 v（偏向"下方"一侧为正）。所有量河道、判定、画面都在这个局部坐标里做，
	/// 水平向左向右只是其中一种情况。
	///
	/// 和地形是一层：出手时按列量出每一处两侧的边界——
	///   贴着地面的一侧被地面压住，气流顺着地形起伏，接触线上翻起白沫、溅起水雾；另一侧碰到方块同理；
	///   整列都是实心（撞上墙）就停在那里，浪头碎在墙上。
	/// 每一列的边界在各个客户端上按同一块地形算出来，联机一致。
	///
	/// 画面：起点从剑符涌出、由细变宽；七缕气流翻滚交缠着往前流；几十柄小剑影顺流疾射；最前面是卷起的浪头，浪尖一排剑光；
	/// 两侧一抹淡墨；碾过去的地方从起点开始慢慢淡成薄雾；流到尽头渐淡渐窄，化成水雾。
	/// ai[0] = 流向的角度。Center = 起点。
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
		private const float Ease = 1.7f;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private ref float Age => ref Projectile.localAI[0];
		private ref float Hits => ref Projectile.localAI[1];

		private Vector2 origin, dir = Vector2.UnitX, nrm = Vector2.UnitY;
		private int count = -1;           // 河道有多少列（撞墙就短）
		private bool walled;              // 是不是撞墙停下的
		private bool crashed;
		private readonly float[] top = new float[Columns];     // 靠 -v 一侧的边界（v 值）
		private readonly float[] bottom = new float[Columns];  // 靠 +v 一侧的边界
		private readonly bool[] floor = new bool[Columns];     // +v 一侧被方块压住
		private readonly bool[] ceiling = new bool[Columns];   // -v 一侧被方块压住

		private int Facing => dir.X >= 0f ? 1 : -1;

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

		/// <summary>局部坐标 → 世界坐标。s 沿流向（像素），v 横穿流向。</summary>
		private Vector2 WS(float s, float v) => origin + dir * s + nrm * v;
		private Vector2 W(float i, float v) => WS(i * Step, v);
		/// <summary>局部方向的速度 → 世界速度。</summary>
		private Vector2 L(float along, float across) => dir * along + nrm * across;

		/// <summary>量河道：每一列从中线出发，往两侧各找到实心方块或河道边界为止。</summary>
		private void Survey()
		{
			origin = Projectile.Center;
			dir = Projectile.ai[0].ToRotationVector2();
			nrm = new Vector2(-dir.Y, dir.X);
			if (nrm.Y < -0.001f)
				nrm = -nrm; // +v 总是偏向下方（地面那一侧）
			count = Columns;
			const float lo = -HalfHeight, hi = HalfHeight;
			for (int i = 0; i < Columns; i++)
			{
				// 中线埋在方块里：先往 -v、再往 +v 找一处空的
				float p = 0f;
				if (Fx.Solid(W(i, 0f)))
				{
					bool found = false;
					for (float v = 0f; v >= lo && !found; v -= 4f)
						if (!Fx.Solid(W(i, v))) { p = v; found = true; }
					for (float v = 0f; v <= hi && !found; v += 4f)
						if (!Fx.Solid(W(i, v))) { p = v; found = true; }
					if (!found)
					{
						count = i;
						walled = true;
						break;
					}
				}
				float t = p, b = p;
				while (t - 4f >= lo && !Fx.Solid(W(i, t - 4f)))
					t -= 4f;
				while (b + 4f <= hi && !Fx.Solid(W(i, b + 4f)))
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

		/// <summary>浪头到了第几列（可以是小数）。越往前越慢，像水势渐尽。</summary>
		private float Front
		{
			get
			{
				float t = MathHelper.Clamp(Age / FlowFrames, 0f, 1f);
				return MathHelper.Min(count - 1, (1f - MathF.Pow(1f - t, Ease)) * (Columns - 1));
			}
		}

		/// <summary>浪头到达第 i 列的时刻（帧）。</summary>
		private static float ArriveAt(float i)
		{
			float u = MathHelper.Clamp(i / (Columns - 1), 0f, 1f);
			return (1f - MathF.Pow(1f - u, 1f / Ease)) * FlowFrames;
		}

		/// <summary>尽头自己散：最后三成的路程里越来越淡、越来越窄，浪头化成水雾（撞墙时不散，浪头碎在墙上）。</summary>
		private float EndFade(float i)
		{
			if (walled)
				return 1f;
			float u = MathHelper.Clamp((i / (Columns - 1) - 0.62f) / 0.38f, 0f, 1f);
			return 1f - u * u * (3f - 2f * u);
		}

		/// <summary>起点渐起：头 10 列（80 像素）从一缕细气慢慢变宽变浓，不是一刀切开。</summary>
		private static float StartFade(float i)
		{
			float u = MathHelper.Clamp(i / 10f, 0f, 1f);
			return u * u * (3f - 2f * u);
		}

		/// <summary>第 i 列的浓淡：浪头刚过时最浓，过了一会儿从起点开始淡去；越近尽头越淡。</summary>
		private float ColumnAlpha(float i)
		{
			float passed = Age - ArriveAt(i);
			float a = 1f - MathHelper.Clamp((passed - 12f) / Linger, 0f, 1f);
			return passed < 0 ? 0f : a * EndFade(i) * (0.15f + 0.85f * StartFade(i));
		}

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			if (count < 0)
			{
				Survey();
				if (!Main.dedServ)
				{
					// 起势：一团水雾和光屑从剑符涌出，往前散
					Vector2 o = W(0, (top[0] + bottom[0]) / 2f);
					for (int k = 0; k < 14; k++)
					{
						Vector2 v = L(Main.rand.NextFloat(0.5f, 3.5f), Main.rand.NextFloat(-2.5f, 2.5f));
						Fx.Mist(o + Main.rand.NextVector2Circular(16f, 16f), v, 1.3f);
						if (k % 2 == 0)
							Fx.Mote(o + Main.rand.NextVector2Circular(12f, 12f), v * 1.3f, 0.9f);
					}
				}
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 0.9f }, Projectile.Center);
				SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.2f }, Projectile.Center);
			}

			int f = (int)Front;
			if (!Main.dedServ)
			{
				Lighting.AddLight(W(f, (top[f] + bottom[f]) / 2f), 0.5f, 0.85f, 0.8f);
				for (int i = 0; i < f; i += 6)
				{
					float a = ColumnAlpha(i);
					if (a > 0.05f)
						Lighting.AddLight(W(i, (top[i] + bottom[i]) / 2f), 0.2f * a, 0.38f * a, 0.36f * a);
				}
				float ef = EndFade(f);
				if (!walled && ef < 0.85f && Age < FlowFrames + 14)
				{
					// 尽头化雾
					for (int k = 0; k < 3; k++)
					{
						int j = Math.Max(0, f - Main.rand.Next(10));
						Vector2 p = W(j, MathHelper.Lerp(top[j], bottom[j], Main.rand.NextFloat()));
						Fx.Mist(p, L(Main.rand.NextFloat(0.3f, 1.5f), Main.rand.NextFloat(-1.2f, 1.2f)) - Vector2.UnitY * 0.5f, 1.4f);
						if (k == 0)
							Fx.Mote(p, L(Main.rand.NextFloat(0.5f, 2f), Main.rand.NextFloat(-1.5f, 1.5f)) - Vector2.UnitY * 0.5f, 0.8f);
					}
				}
				if (Age < FlowFrames && ef > 0.3f)
				{
					// 浪头：贴着方块的一侧翻起水雾，浪尖洒光屑
					if (floor[f])
						for (int k = 0; k < 2; k++)
							Fx.Mist(W(f, bottom[f] - 2f), L(Main.rand.NextFloat(1f, 3.5f), -Main.rand.NextFloat(0.5f, 2.5f)), 1.3f);
					if (ceiling[f] && Main.rand.NextBool(2))
						Fx.Mist(W(f, top[f] + 2f), L(Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(0.3f, 1.5f)), 1.1f);
					for (int k = 0; k < 2; k++)
						Fx.Mote(W(f, MathHelper.Lerp(top[f], bottom[f], Main.rand.NextFloat())), L(Main.rand.NextFloat(1f, 4f), Main.rand.NextFloat(-2f, 2f)), Main.rand.NextFloat(0.6f, 1f));
					// 河身贴着方块的地方零星翻起白沫
					int j = Main.rand.Next(Math.Max(f, 1));
					if (floor[j] && ColumnAlpha(j) > 0.3f)
						Fx.Mist(W(j, bottom[j] - 2f), L(1.2f, -0.8f), 0.9f);
				}
				// 碾过去的地方留一层薄雾
				if (Age > 10 && Main.rand.NextBool(2))
				{
					int j = Main.rand.Next(Math.Max(f, 1));
					if (ColumnAlpha(j) is > 0.1f and < 0.7f)
						Fx.Mist(W(j, MathHelper.Lerp(top[j], bottom[j], Main.rand.NextFloat())), L(0.5f, 0f) - Vector2.UnitY * 0.3f, 1.4f);
				}
			}

			// 撞墙：浪头碎在墙上
			if (walled && !crashed && Front >= count - 1)
			{
				crashed = true;
				int e = count - 1;
				Vector2 at = W(e, (top[e] + bottom[e]) / 2f);
				SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.3f, Volume = 0.8f }, at);
				if (!Main.dedServ)
				{
					for (int k = 0; k < 22; k++)
					{
						Vector2 p = W(e, MathHelper.Lerp(top[e], bottom[e], Main.rand.NextFloat()));
						Fx.Mist(p, L(-Main.rand.NextFloat(0.5f, 4f), Main.rand.NextFloat(-4f, 4f)), 1.5f);
						if (k % 2 == 0)
							Fx.Mote(p, L(-Main.rand.NextFloat(1f, 5f), Main.rand.NextFloat(-4f, 4f)), 0.9f);
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
			// 敌人在流向上覆盖的那几列里，只要有一列在浪头之后、并且和这一列的横截线相交，就斩到
			float sMin = float.MaxValue, sMax = float.MinValue;
			foreach (Vector2 c in new[] { target.TopLeft(), target.TopRight(), target.BottomLeft(), target.BottomRight() })
			{
				float s = Vector2.Dot(c - origin, dir);
				sMin = MathF.Min(sMin, s);
				sMax = MathF.Max(sMax, s);
			}
			int lo = Math.Max(0, (int)(sMin / Step) - 1), hi = Math.Min((int)Front, (int)(sMax / Step) + 1);
			float point = 0f;
			for (int i = lo; i <= hi; i++)
			{
				if (Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), W(i, top[i]), W(i, bottom[i]), Step + 2f, ref point))
					return true;
			}
			return false;
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (target.boss)
				modifiers.SourceDamage *= 0.7f;
			modifiers.HitDirectionOverride = Facing;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			Hits++;
			QiBloom.Spawn(Projectile, target.Center, 70f);
			// 被湍流冲走
			if (!target.boss && target.knockBackResist > 0f)
				target.velocity += dir * 5f * target.knockBackResist;
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
			// -v 一侧起伏：不贴着方块的地方，水面一起一伏，不是一条硬直线
			var dTop = new float[count];
			for (int i = 0; i < count; i++)
			{
				float s = i * Step;
				dTop[i] = ceiling[i] ? top[i]
					: top[i] + 9f + 6f * MathF.Sin(time * 5f - s * 0.018f) + 3f * MathF.Sin(time * 9f - s * 0.05f + 1f);
				dTop[i] = MathF.Min(dTop[i], bottom[i] - 16f);
			}
			// 起点由细变宽、越近尽头越窄：两侧往中线收拢
			var dBot = new float[count];
			for (int i = 0; i < count; i++)
			{
				float mid = (dTop[i] + bottom[i]) / 2f, k = (0.35f + 0.65f * EndFade(i)) * (0.2f + 0.8f * StartFade(i));
				dBot[i] = mid + (bottom[i] - mid) * k;
				dTop[i] = mid - (mid - dTop[i]) * k;
			}

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
				center[k] = W(i, (dTop[i] + dBot[i]) / 2f);
				edgeTop[k] = W(i, dTop[i]);
				edgeBottom[k] = W(i, dBot[i]);
			}
			int Col(float p) => colIndex[Math.Clamp((int)MathF.Round(p * (n - 1)), 0, n - 1)];
			float lead = MathHelper.Clamp(70f / MathF.Max(n * 16f, 1f), 0.02f, 0.3f);
			float Lead(float p) => walled && crashed ? 1f : MathHelper.SmoothStep(0.15f, 1f, MathHelper.Clamp(p / lead, 0f, 1f));
			float Height(float p) { int i = Col(p); return (dBot[i] - dTop[i]) * (0.55f + 0.45f * Lead(p)); }
			float Alpha(float p) => ColumnAlpha(Col(p)) * Lead(p);

			// 起点一团白玉柔光：剑符化开的那一下，十几帧内淡去
			if (Age < 16)
			{
				float g = 1f - Age / 16f;
				Vector2 o = W(0, (dTop[0] + dBot[0]) / 2f);
				Fx.Circle(sb, Fx.SoftGlow.Value, o, 90f * (0.6f + 0.4f * (1f - g)), Fx.DeepTeal * (0.35f * g));
				Fx.Circle(sb, Fx.SoftGlow.Value, o, 60f * (0.6f + 0.4f * (1f - g)), Fx.Jade * (0.5f * g));
			}

			// 两侧一抹淡墨（不发光，和场景是一层）
			Trail.Draw(edgeTop, p => 16f, p => Fx.Ink * (0.22f * Alpha(p)), Trail.Glow.Value);
			Trail.Draw(edgeBottom, p => 16f, p => Fx.Ink * (0.22f * Alpha(p)), Trail.Glow.Value);

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
					float h = dBot[i] - dTop[i];
					float si = i * Step;
					float wave = MathF.Sin(time * 9f - si * 0.025f + phase) * h * 0.13f
						+ MathF.Sin(time * 5f - si * 0.011f + phase * 2f) * h * 0.07f;
					float v = MathHelper.Lerp(dTop[i] + 6f, dBot[i] - 6f, frac) + wave;
					pts[k] = W(i, MathHelper.Clamp(v, dTop[i] + 3f, dBot[i] - 3f));
				}
				bool jade = s % 2 == 0;
				float w = jade ? 7f : 12f;
				float Wd(float p) => w * (0.5f + 0.5f * Height(p) / (HalfHeight * 2f)) * (0.6f + 0.4f * (1f - p));
				// 每缕先垫一道深青，再画白玉 / 淡青，丝缕有轮廓，不会被亮背景吃掉
				Trail.Draw(pts, p => Wd(p) + 4f, p => Fx.DeepTeal * (0.35f * Alpha(p)), Trail.Glow.Value);
				Trail.Draw(pts, Wd, p => (jade ? Fx.Jade : Fx.PaleTeal) * ((jade ? 0.85f : 0.7f) * Alpha(p)),
					jade ? Trail.Glow.Value : Trail.Qi.Value, uRepeat: jade ? 1f : n / 12f, uScroll: jade ? 0f : -time * 3.5f + s);
			}

			// 贴着方块处的白沫：接触线上一道起伏的白
			for (int k = 0; k < n - 1; k++)
			{
				int i = colIndex[k], j = colIndex[k + 1];
				float a = ColumnAlpha(i);
				if (a <= 0.02f)
					continue;
				float foam = 0.5f + 0.5f * MathF.Sin(time * 12f + i * 0.9f);
				if (floor[i] && floor[j])
					Fx.Line(sb, W(i, dBot[i] - 1f), W(j, dBot[j] - 1f), Fx.Jade * (0.85f * a * (0.5f + 0.5f * foam)), 3f + 2f * foam);
				if (ceiling[i] && ceiling[j])
					Fx.Line(sb, W(i, dTop[i] + 1f), W(j, dTop[j] + 1f), Fx.Jade * (0.4f * a * foam), 2f);
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
				Vector2 head = W(i, MathHelper.Lerp(dTop[i] + 8f, dBot[i] - 8f, Hash(k, 3)));
				Vector2 tail = head - dir * MathHelper.Lerp(18f, 34f, Hash(k, 4));
				Fx.Line(sb, tail, head, Fx.DeepTeal * (0.7f * a), 5f);
				Fx.Line(sb, tail, head, Fx.Jade * a, 2f);
			}

			// 浪头：卷起来的一道浪，浪尖一排剑光（流完就散）
			if (Age < FlowFrames + 8 && !(walled && crashed && Age > FlowFrames))
			{
				float fa = (1f - MathHelper.Clamp((Age - FlowFrames) / 8f, 0f, 1f)) * EndFade(f);
				float sf = f * Step, t0 = dTop[f], b0 = dBot[f], h = b0 - t0;
				// 浪身：从 +v 一侧往 -v、往前卷
				const int m = 12;
				var curl = new Vector2[m];
				for (int k = 0; k < m; k++)
				{
					float u = k / (m - 1f);                       // 0 = 浪尖（头）
					float ang = MathHelper.Lerp(-0.4f, 2.4f, u);    // 绕一个圆心卷
					float r = h * 0.42f * (1f - 0.25f * u);
					curl[k] = WS(sf - h * 0.28f + MathF.Cos(ang) * r, t0 + h * 0.42f - MathF.Sin(ang) * 0.9f * r + u * u * h * 0.35f);
				}
				float cw = MathF.Min(1f, h / 80f);
				Trail.Draw(curl, p => 30f * (1f - 0.6f * p) * cw, p => Fx.DeepTeal * (0.55f * fa * (1f - 0.5f * p)), Trail.Glow.Value);
				Trail.Draw(curl, p => 26f * (1f - 0.6f * p) * cw, p => Fx.PaleTeal * (0.85f * fa * (1f - 0.5f * p)), Trail.Qi.Value, uRepeat: 1f, uScroll: -time * 4f);
				Trail.Draw(curl, p => 7f * (1f - 0.7f * p) + 1f, p => Fx.Jade * (fa * (1f - 0.6f * p)), Trail.Glow.Value);
				// 浪尖一排剑光：横穿流向的一线白玉，上面几点闪
				Fx.Line(sb, W(f, t0 + 4f), W(f, b0 - 4f), Fx.DeepTeal * (0.5f * fa), 6f);
				Fx.Line(sb, W(f, t0 + 4f), W(f, b0 - 4f), Fx.Jade * (0.9f * fa), 3f);
				for (int k = 0; k < 4; k++)
				{
					float v = MathHelper.Lerp(t0 + 10f, b0 - 10f, (k + Hash((int)Age / 3, k) * 0.6f) / 4f);
					float g = 6f + 6f * Hash((int)Age, k + 9);
					Vector2 at = WS(sf + 4f, v);
					Fx.Line(sb, at - dir * g, at + dir * g, Color.White * (0.8f * fa), 1.5f);
					Fx.Line(sb, at - nrm * g * 0.6f, at + nrm * g * 0.6f, Color.White * (0.6f * fa), 1.5f);
				}
			}
			return false;
		}
	}
}
