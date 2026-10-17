using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.NPCs.SwordImmortal
{
	/// <summary>
	/// 御剑术 / Swordflight: a sword that fans out behind the boss, aims (with a warning line), then fires.
	/// ai[0] = boss whoAmI, ai[1] = slot in the fan, ai[2] = ticks until launch.
	/// </summary>
	public class FlyingSword : ModProjectile
	{
		public const int Count = 7;
		private const float LaunchSpeed = 30f;
		private const int AimFreeze = 12;   // 发射前这么多帧锁定方向
		private const int WarnTime = 36;

		public override string Texture => "QingyiJianxian/Content/NPCs/SwordImmortal/Azurebrand";

		private ref float Age => ref Projectile.localAI[0];
		private int LaunchAt => (int)Projectile.ai[2];
		private bool Launched => Age >= LaunchAt;
		private Vector2 aimDir = Vector2.UnitY;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 8;
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
			Projectile.timeLeft = 400;
		}

		public override bool CanHitPlayer(Player target) => Launched;

		public override void AI()
		{
			if (!Launched)
			{
				NPC boss = Main.npc[(int)Projectile.ai[0]];
				if (!boss.active || boss.type != ModContent.NPCType<SwordImmortal>())
				{
					Projectile.Kill();
					return;
				}

				// 剑在剑仙身后排成扇形，轻轻浮动
				int slot = (int)Projectile.ai[1];
				float angle = -MathHelper.PiOver2 + (slot - (Count - 1) / 2f) * 0.34f;
				Vector2 home = boss.Center + angle.ToRotationVector2() * 96f + new Vector2(-boss.spriteDirection * 24f, -10f);
				home.Y += MathF.Sin((Age + slot * 9) * 0.08f) * 5f;
				Projectile.Center = Vector2.Lerp(Projectile.Center, home, 0.16f);

				Player target = Main.player[boss.target];
				if (Age < LaunchAt - AimFreeze)
				{
					// 单数位的剑预判走位，双数位的剑直取：一把封你现在的位置，一把封你要去的地方
					Vector2 aim = target.Center;
					if (slot % 2 == 1)
						aim += target.velocity * MathHelper.Clamp(Projectile.Distance(target.Center) / LaunchSpeed, 0f, 30f);
					aimDir = Projectile.DirectionTo(aim);
				}
				Projectile.rotation = aimDir.ToRotation() + MathHelper.PiOver4;

				if (++Age >= LaunchAt)
				{
					Projectile.velocity = aimDir * LaunchSpeed;
					Projectile.timeLeft = 120;
					Projectile.netUpdate = true;
					SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.3f }, Projectile.Center);
				}
				return;
			}

			Age++;
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
			if (Main.rand.NextBool(2))
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, -Projectile.velocity * 0.1f, 100, default, 0.9f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = tex.Size() / 2f;

			if (!Launched && Age > LaunchAt - WarnTime)
			{
				float t = (Age - (LaunchAt - WarnTime)) / WarnTime;
				bool locked = Age >= LaunchAt - AimFreeze;
				int tier = SwordImmortal.TelegraphTier(Main.npc[(int)Projectile.ai[0]]);
				// 锁定瞬间剑尖寒光一闪（各阶段都有）；红线按阶段递减，见 SwordImmortal.TelegraphTier
				if (locked && Age < LaunchAt - AimFreeze + 6)
					SwordImmortal.Glint(sb, Projectile.Center + aimDir * 30f, 0.7f);
				if (tier == 0 || (tier == 1 && !locked))
					Fx.Line(sb, Projectile.Center, Projectile.Center + aimDir * 1400f, Fx.Cinnabar * (locked ? 0.9f : 0.25f + t * 0.4f), locked ? 3f : 1.5f);
			}

			if (Launched)
			{
				// 剑光拖尾：剑尖在前，尾迹在后
				Vector2[] pts = Centers(Projectile, Projectile.velocity.SafeNormalize(Vector2.Zero) * 24f);
				Trail.Draw(pts, p => 18f * (1f - p), p => Fx.Teal * 0.8f * (1f - p), Trail.Glow.Value, additive: true);
				Trail.Draw(pts, p => 6f * (1f - p), p => Color.White * 0.9f * (1f - p), Trail.Glow.Value, additive: true);
			}

			sb.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, origin, 1f, SpriteEffects.None, 0f);
			return false;
		}

		/// <summary>Current center plus the cached old centers, head first, shifted by an offset.</summary>
		public static Vector2[] Centers(Projectile proj, Vector2 offset)
		{
			var pts = new Vector2[proj.oldPos.Length + 1];
			pts[0] = proj.Center + offset;
			for (int i = 0; i < proj.oldPos.Length; i++)
				pts[i + 1] = proj.oldPos[i] == Vector2.Zero ? Vector2.Zero : proj.oldPos[i] + proj.Size / 2f + offset;
			return pts;
		}
	}
}
