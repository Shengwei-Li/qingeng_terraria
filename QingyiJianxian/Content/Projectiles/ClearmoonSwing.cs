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
	/// 清月剑的一次挥砍（手持弹幕）。三段连斩：0 上挑，1 下劈，2 旋身一斩。
	/// 每一剑都甩出一道月牙剑气，旋斩那一道最大、飞得最远。
	/// ai[0] = 第几段，ai[1] = 出手时的朝向（±1）；velocity = 瞄准方向。
	/// </summary>
	public class ClearmoonSwing : ModProjectile
	{
		private const float BladeLength = 84f;   // 剑柄到剑尖（屏幕像素，1 倍大小时）
		private const int TipHistory = 10;
		private const int Ghosts = 3;            // 剑身残影

		public override string Texture => "QingyiJianxian/Content/Items/Clearmoon";

		private ref float Age => ref Projectile.localAI[0];
		private int Step => (int)Projectile.ai[0];
		private int Dir => Projectile.ai[1] >= 0 ? 1 : -1;
		private float duration;
		private float aim;
		private bool released;
		private readonly Vector2[] tips = new Vector2[TipHistory];
		private readonly float[] ghostAngles = new float[Ghosts];

		// 剑身做小一号，轻灵些；旋斩稍大
		private float Scale => Step == 2 ? 0.95f : 0.82f;

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

		/// <summary>每一段的起止角（相对瞄准方向），朝左时镜像。</summary>
		private (float from, float to) Arc() => Step switch
		{
			0 => (1.1f, -1.5f),   // 上挑：自下而上
			1 => (-1.5f, 1.1f),   // 下劈：自上而下
			_ => (-2.0f, 2.5f),   // 旋斩：大半圈
		};

		private float Progress => MathHelper.Clamp(Age / duration, 0f, 1f);

		private float SwingAngle()
		{
			var (from, to) = Arc();
			float t = Progress;
			// 出手即快、收势放缓：像甩出去的鞭子，比匀速利落
			float eased = 1f - MathF.Pow(1f - t, 3f);
			return aim + Dir * MathHelper.Lerp(from, to, eased);
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (Age == 0)
			{
				duration = Math.Max(owner.itemAnimationMax, 8);
				aim = Projectile.velocity.ToRotation();
				Projectile.velocity = Vector2.Zero; // 位置每帧跟着玩家，不需要速度
				SoundEngine.PlaySound(Step == 2 ? SoundID.Item71 with { Pitch = 0.2f } : SoundID.Item1 with { Pitch = 0.1f * Step }, owner.Center);
			}
			if (!owner.active || owner.dead || owner.CCed || Age >= duration)
			{
				Projectile.Kill();
				return;
			}

			float angle = SwingAngle();
			Projectile.rotation = angle;
			Projectile.Center = owner.RotatedRelativePoint(owner.MountedCenter);
			owner.heldProj = Projectile.whoAmI;
			owner.ChangeDir(Dir);
			owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, angle - MathHelper.PiOver2);

			for (int i = TipHistory - 1; i > 0; i--)
				tips[i] = tips[i - 1];
			tips[0] = Projectile.Center + angle.ToRotationVector2() * BladeLength * Scale;
			if (Age == 0)
				Array.Fill(ghostAngles, angle);
			for (int i = Ghosts - 1; i > 0; i--)
				ghostAngles[i] = ghostAngles[i - 1];
			ghostAngles[0] = angle;

			// 每一剑挥到中段甩出月牙剑气：前两剑小而快，旋斩那一道大而远
			bool big = Step == 2;
			if (!released && Progress >= (big ? 0.5f : 0.3f))
			{
				released = true;
				if (Projectile.owner == Main.myPlayer)
				{
					Vector2 v = aim.ToRotationVector2() * (big ? 24f : 21f);
					Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, v,
						ModContent.ProjectileType<ClearmoonQi>(), (int)(Projectile.damage * (big ? 0.9f : 0.45f)), Projectile.knockBack, Projectile.owner, big ? 1f : 0f);
				}
			}
			Age++;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float point = 0f;
			Vector2 start = Projectile.Center;
			Vector2 end = start + Projectile.rotation.ToRotationVector2() * BladeLength * Scale;
			return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 26f * Scale, ref point);
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (Step == 2)
				modifiers.SourceDamage *= 1.5f; // 旋斩更重
			modifiers.HitDirectionOverride = Dir;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// 剑光：剑尖扫过的弧线，青白色
			if (Age > 1)
			{
				Trail.Draw(tips, p => 30f * Scale * (1f - p), p => Fx.Teal * 0.6f * (1f - p), Trail.Glow.Value, additive: true);
				Trail.Draw(tips, p => 12f * Scale * (1f - p), p => Color.White * 0.8f * (1f - p), Trail.Glow.Value, additive: true);
			}

			Texture2D tex = TextureAssets.Projectile[Type].Value;
			// 贴图剑尖指向右上（-45°），剑柄在左下
			var hilt = new Vector2(10f, tex.Height - 10f);
			Vector2 at = Projectile.Center - Main.screenPosition;
			// 剑身残影：淡青，越旧越淡
			for (int i = Ghosts - 1; i >= 1; i--)
				Main.spriteBatch.Draw(tex, at, null, Fx.PaleTeal * (0.35f * (1f - i / (float)Ghosts)), ghostAngles[i] + MathHelper.PiOver4, hilt, Scale, SpriteEffects.None, 0f);
			Main.spriteBatch.Draw(tex, at, null, Color.Lerp(lightColor, Color.White, 0.5f), Projectile.rotation + MathHelper.PiOver4, hilt, Scale, SpriteEffects.None, 0f);
			return false;
		}
	}
}
