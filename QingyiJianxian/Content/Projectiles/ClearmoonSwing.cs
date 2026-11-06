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
	/// 清月剑的一次出剑（手持弹幕）。三段：0 横扫（自上而下），1 回撩（自下而上），2 前刺。
	/// 挥剑分三拍：先往反方向蓄一下，再极快地挥过去、略挥过头，最后收回来——有起势、有惯性，不是匀速转一圈。
	/// 剑光是一片扇形残影，按挥剑曲线逐帧细分采样，挥得再快也是一道圆弧。
	/// ai[0] = 第几段，ai[1] = 出手时的朝向（±1）；velocity = 瞄准方向。
	/// </summary>
	public class ClearmoonSwing : ModProjectile
	{
		private const float BladeLength = 84f;   // 剑柄到剑尖（1 倍大小时）
		private const float WindUp = 0.2f;       // 蓄势占整段的比例
		private const float SwingEnd = 0.62f;    // 挥到这里就挥完了，之后是收势
		private const float PullBack = 0.3f;     // 蓄势时往反方向带多少（弧度）
		private const float Overshoot = 0.2f;    // 挥过头多少

		public override string Texture => "QingyiJianxian/Content/Items/Clearmoon";

		private ref float Age => ref Projectile.localAI[0];
		private int Step => (int)Projectile.ai[0];
		private int Dir => Projectile.ai[1] >= 0 ? 1 : -1;
		private bool Thrust => Step == 2;
		private float duration;
		private float aim;
		private bool released;
		private bool lunged;

		private const float Scale = 0.85f;

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.ownerHitCheck = true;            // 不隔墙砍人
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;        // 一刀只砍每个敌人一次
			Projectile.timeLeft = 600;
		}

		/// <summary>挥剑的起止角（相对瞄准方向，朝左时镜像）。</summary>
		private (float from, float to) Arc() => Step == 0 ? (-1.75f, 1.25f) : (1.35f, -1.85f);

		private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
		private static float EaseInOut(float t) => t < 0.5f ? 16f * t * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 5f) / 2f;

		/// <summary>t（0..1）时剑的角度和伸出长度（剑长的倍数）。</summary>
		private (float angle, float reach) Pose(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			if (Thrust)
			{
				// 前刺：剑收到身侧蓄势，一下扎出去，停住，再收回
				float reach;
				float tilt;
				if (t < 0.3f)
				{
					float s = EaseOut(t / 0.3f);
					reach = MathHelper.Lerp(0.8f, 0.5f, s);
					tilt = 0.35f * s;
				}
				else if (t < 0.42f)
				{
					float s = EaseOut((t - 0.3f) / 0.12f);
					reach = MathHelper.Lerp(0.5f, 1.4f, s);
					tilt = 0.35f * (1f - s);
				}
				else if (t < 0.72f)
				{
					reach = 1.4f;
					tilt = 0f;
				}
				else
				{
					float s = EaseInOut((t - 0.72f) / 0.28f);
					reach = MathHelper.Lerp(1.4f, 0.85f, s);
					tilt = -0.25f * s;
				}
				return (aim - Dir * tilt, reach);
			}

			var (from, to) = Arc();
			float sign = Math.Sign(to - from);
			float rel;
			float stretch = 0f;
			if (t < WindUp)
				rel = from - sign * PullBack * EaseOut(t / WindUp);
			else if (t < SwingEnd)
			{
				float s = (t - WindUp) / (SwingEnd - WindUp);
				rel = MathHelper.Lerp(from - sign * PullBack, to + sign * Overshoot, EaseInOut(s));
				stretch = MathF.Sin(MathF.PI * s);
			}
			else
				rel = MathHelper.Lerp(to + sign * Overshoot, to, EaseOut((t - SwingEnd) / (1f - SwingEnd)));
			return (aim + Dir * rel, 1f + 0.12f * stretch);
		}

		private float Progress => MathHelper.Clamp(Age / duration, 0f, 1f);

		private bool Cutting => Thrust ? Progress >= 0.3f && Progress < 0.75f : Progress >= WindUp - 0.02f && Progress < SwingEnd + 0.06f;

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (Age == 0)
			{
				duration = Math.Max(owner.itemAnimationMax, 8);
				aim = Projectile.velocity.ToRotation();
				Projectile.velocity = Vector2.Zero; // 位置每帧跟着玩家，不需要速度
			}
			if (!owner.active || owner.dead || owner.CCed || Age >= duration)
			{
				Projectile.Kill();
				return;
			}

			var (angle, reach) = Pose(Progress);
			Projectile.rotation = angle;
			Projectile.scale = reach;
			Projectile.Center = owner.RotatedRelativePoint(owner.MountedCenter);
			owner.heldProj = Projectile.whoAmI;
			owner.ChangeDir(Dir);
			var stretchArm = reach > 1.1f ? Player.CompositeArmStretchAmount.Full : reach > 0.7f ? Player.CompositeArmStretchAmount.ThreeQuarters : Player.CompositeArmStretchAmount.Quarter;
			owner.SetCompositeArmFront(true, stretchArm, angle - MathHelper.PiOver2);

			// 出手的声音落在真正挥出去的那一下，不是按下键的那一下
			float swingAt = Thrust ? 0.3f : WindUp;
			if (Age == (int)(duration * swingAt))
				SoundEngine.PlaySound(Thrust ? SoundID.Item71 with { Pitch = 0.35f } : SoundID.Item1 with { Pitch = 0.15f + 0.15f * Step }, owner.Center);

			Vector2 aimDir = aim.ToRotationVector2();
			// 前刺带一小步突进
			if (Thrust && !lunged && Progress >= 0.3f)
			{
				lunged = true;
				if (Projectile.owner == Main.myPlayer && owner.grappling[0] < 0 && !owner.mount.Active)
				{
					owner.velocity.X = MathHelper.Lerp(owner.velocity.X, aimDir.X * 8f, 0.7f);
					if (aimDir.Y < -0.3f)
						owner.velocity.Y = Math.Min(owner.velocity.Y, aimDir.Y * 5f);
				}
			}

			// 剑气：横扫、回撩挥到正中甩出一线剑气；前刺扎到头刺出一道长剑气
			if (!released && Progress >= (Thrust ? 0.38f : 0.42f))
			{
				released = true;
				if (Projectile.owner == Main.myPlayer)
				{
					Vector2 v = aimDir * (Thrust ? 26f : 20f);
					Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + aimDir * 40f, v,
						ModContent.ProjectileType<ClearmoonQi>(), (int)(Projectile.damage * (Thrust ? 0.8f : 0.5f)), Projectile.knockBack * 0.5f,
						Projectile.owner, Thrust ? 1f : 0f, Dir * (Step == 0 ? 1f : -1f));
				}
			}
			Age++;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (!Cutting)
				return false;
			float point = 0f;
			Vector2 start = Projectile.Center;
			Vector2 end = start + Projectile.rotation.ToRotationVector2() * BladeLength * Scale * Projectile.scale;
			return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 26f * Scale, ref point);
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (Thrust)
				modifiers.SourceDamage *= 1.4f; // 前刺更重
			modifiers.HitDirectionOverride = Dir;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Thrust)
				ClearmoonScar.Mark(Projectile, target, Projectile.damage);
			if (!Main.dedServ)
			{
				for (int i = 0; i < 6; i++)
				{
					Dust d = Dust.NewDustPerfect(target.Center, DustID.Frost, Main.rand.NextVector2Circular(4f, 4f), 80, default, 1f);
					d.noGravity = true;
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			float t = Progress;
			float step = 1f / duration;
			if (Thrust)
				DrawThrustStreak(sb, t);
			else if (t > WindUp)
				DrawSmear(sb, t, step);

			Texture2D tex = TextureAssets.Projectile[Type].Value;
			// 贴图剑尖指向右上（-45°），剑柄在左下
			var hilt = new Vector2(10f, tex.Height - 10f);
			Vector2 at = Projectile.Center - Main.screenPosition;
			// 挥剑时剑身在挥到正中时略放大（拉伸感）；前刺时整把剑沿刺出方向前送
			float scale = Scale;
			if (Thrust)
				at += Projectile.rotation.ToRotationVector2() * BladeLength * Scale * (Projectile.scale - 1f);
			else
				scale *= Projectile.scale;
			sb.Draw(tex, at, null, Color.Lerp(lightColor, Color.White, 0.5f), Projectile.rotation + MathHelper.PiOver4, hilt, scale, SpriteEffects.None, 0f);
			return false;
		}

		/// <summary>扇形剑光：沿挥剑曲线往回取一小段，画成一片从剑身中段到剑尖的残影。</summary>
		private void DrawSmear(SpriteBatch sb, float t, float step)
		{
			// 挥完后残影迅速收掉
			float life = t < SwingEnd ? 1f : 1f - (t - SwingEnd) / (1f - SwingEnd) * 1.6f;
			if (life <= 0f)
				return;
			const int n = 14;
			float span = step * 4.5f; // 残影覆盖最近 4.5 帧的挥剑
			var mid = new Vector2[n];
			var tip = new Vector2[n];
			float L = BladeLength * Scale;
			for (int i = 0; i < n; i++)
			{
				float ti = MathHelper.Max(WindUp, t - span * i / (n - 1));
				var (a, r) = Pose(ti);
				Vector2 d = a.ToRotationVector2();
				mid[i] = Projectile.Center + d * L * r * 0.62f;
				tip[i] = Projectile.Center + d * L * r * 0.97f;
			}
			Trail.Draw(mid, p => L * 0.62f * (1f - 0.5f * p), p => Fx.Teal * (0.35f * life * (1f - p)), Trail.Glow.Value, additive: true);
			Trail.Draw(tip, p => 9f * (1f - p), p => Color.White * (0.85f * life * (1f - p)), Trail.Glow.Value, additive: true);
		}

		/// <summary>前刺：剑身前后一道细长的光。</summary>
		private void DrawThrustStreak(SpriteBatch sb, float t)
		{
			if (t < 0.3f || t > 0.8f)
				return;
			float k = t < 0.42f ? (t - 0.3f) / 0.12f : 1f - (t - 0.42f) / 0.38f;
			Vector2 d = Projectile.rotation.ToRotationVector2();
			float L = BladeLength * Scale * Projectile.scale;
			Vector2 a = Projectile.Center + d * L * 0.3f, b = Projectile.Center + d * (L + 26f);
			Fx.Line(sb, a, b, Fx.Teal * (0.4f * k), 10f * k);
			Fx.Line(sb, a, b, Color.White * (0.8f * k), 2f);
		}
	}
}
