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
	/// 斩痕：清月剑前刺刺中的敌人身上，先浮出一线细细的寒光，0.3 秒后沿着它绽开一道交叉的剑痕，只伤这一个敌人。
	/// 先中，后开——这把剑独有的手感。同一个敌人身上同时只有一道。
	/// ai[0] = 目标 NPC 的下标，ai[1] = 剑痕的角度。
	/// </summary>
	public class ClearmoonScar : ModProjectile
	{
		private const int Delay = 18;
		private const int Show = 14;
		private const float Half = 34f;

		public override string Texture => "QingyiJianxian/Assets/Effects/Disc";

		private ref float Age => ref Projectile.localAI[0];
		private NPC Target => Main.npc[(int)Projectile.ai[0]];

		/// <summary>在 target 身上留一道斩痕（只在出剑的那个客户端生成，再同步给别人）。</summary>
		public static void Mark(Projectile source, NPC target, int damage)
		{
			if (source.owner != Main.myPlayer || !target.active || target.life <= 0)
				return;
			int type = ModContent.ProjectileType<ClearmoonScar>();
			foreach (Projectile p in Main.ActiveProjectiles)
			{
				if (p.type == type && p.owner == source.owner && (int)p.ai[0] == target.whoAmI)
					return;
			}
			Projectile.NewProjectile(source.GetSource_FromThis(), target.Center, Vector2.Zero, type, (int)(damage * 0.6f), 0f,
				source.owner, target.whoAmI, source.velocity.ToRotation() + MathHelper.PiOver2 + Main.rand.NextFloat(-0.5f, 0.5f));
		}

		public override void SetDefaults()
		{
			Projectile.width = 40;
			Projectile.height = 40;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.timeLeft = Delay + Show + 2;
		}

		public override void AI()
		{
			NPC target = Target;
			if (!target.active || target.life <= 0)
			{
				Projectile.Kill();
				return;
			}
			Projectile.Center = target.Center;
			if (Age == Delay)
			{
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.8f, Volume = 0.6f }, Projectile.Center);
				if (!Main.dedServ)
				{
					Vector2 d = Projectile.ai[1].ToRotationVector2();
					for (int i = 0; i < 10; i++)
					{
						Dust dust = Dust.NewDustPerfect(Projectile.Center + d * Main.rand.NextFloat(-Half, Half), DustID.Frost, d.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-2.5f, 2.5f), 80, default, 1.1f);
						dust.noGravity = true;
					}
				}
			}
			Age++;
		}

		public override bool? CanHitNPC(NPC target) => Age >= Delay && Age < Delay + 3 && target.whoAmI == (int)Projectile.ai[0] ? null : false;

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			Vector2 d = Projectile.ai[1].ToRotationVector2();
			Vector2 c = Projectile.Center;
			if (Age < Delay)
			{
				// 寒光一线，越来越亮
				float t = Age / Delay;
				Fx.Line(sb, c - d * Half * t, c + d * Half * t, Fx.PaleTeal * (0.3f + 0.5f * t), 1f);
				return false;
			}
			// 绽开：一道主痕 + 一道斜着的副痕，迅速收细淡去
			float k = 1f - (Age - Delay) / Show;
			Vector2 d2 = d.RotatedBy(0.9f);
			Fx.Line(sb, c - d * Half * 1.2f, c + d * Half * 1.2f, Fx.Teal * (0.6f * k), 8f * k + 1f);
			Fx.Line(sb, c - d * Half * 1.2f, c + d * Half * 1.2f, Color.White * k, 2f * k + 1f);
			Fx.Line(sb, c - d2 * Half * 0.8f, c + d2 * Half * 0.8f, Fx.Teal * (0.45f * k), 5f * k + 1f);
			Fx.Line(sb, c - d2 * Half * 0.8f, c + d2 * Half * 0.8f, Color.White * (0.8f * k), 1.5f * k);
			return false;
		}
	}
}
