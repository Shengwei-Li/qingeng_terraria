using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.NPCs.SwordImmortal
{
	/// <summary>
	/// 一剑开天 / The Stroke That Cleaveth Heaven. 剑仙化光没入天上的法相之后，法相这一剑以笔为剑、自天顶一笔画下：
	///   落笔：剑在高处显形，对准玩家；
	///   行笔：一路往下画，身后留下淋漓的墨痕；
	///   接剑：剑锋逼到跟前时世界放慢，剑锋上一圈青光收拢——这时打碎它就算接住；
	///   收笔：没接住就猛地斩落——这一剑劈开天地，整个领域里的人都躲不掉——重伤（不致死），剑仙再来一次。
	/// 这一剑只能接，不能躲：行笔追得紧，放慢时剑锋就悬在玩家面前。
	/// 它是个 NPC，任何攻击都能打它；血量藏起来，不显示血条和伤害数字。ai[0] = 剑仙的 whoAmI。
	/// </summary>
	public class FinalSword : ModNPC
	{
		private const int AppearTime = 40;
		private const float DrawSpeed = 22f;      // 行笔的速度：比翅膀快，跑不掉
		private const float SlowDistance = 360f;  // 剑锋离玩家这么近时，世界放慢
		private const int MaxDrawTime = 70;       // 最多画这么久也进入放慢
		private const int SlowTime = 120;         // 放慢多久（接剑的窗口）
		private const float HoverGap = 150f;      // 放慢时剑锋悬在玩家面前这么远
		private const float SlamSpeed = 34f;
		private const int BaseLife = 900;

		private enum Phase { Appear, Draw, Slow, Slam }

		private ref float Age => ref NPC.localAI[0];
		private Phase phase = Phase.Appear;
		private int phaseTimer;
		private Vector2 dir = Vector2.UnitY;     // 剑尖方向
		private Vector2 origin;                   // 落笔点：墨痕从这里画起
		private Vector2 aimPoint;
		private bool broken;

		private Texture2D Tex => TextureAssets.Npc[Type].Value;
		private float BladeLength => Tex.Height;
		// 判定框放在靠剑尖的一段（离玩家最近、最好打到的地方）；画面中心在它后面
		private Vector2 VisualCenter => NPC.Center - dir * BladeLength * 0.22f;
		private Vector2 Tip => VisualCenter + dir * BladeLength * 0.5f;

		public override void SetStaticDefaults()
		{
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
		}

		public override void SetDefaults()
		{
			NPC.width = 150;
			NPC.height = 150;
			NPC.lifeMax = BaseLife;
			NPC.damage = 0;
			NPC.defense = 0;
			NPC.knockBackResist = 0f;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.dontCountMe = true;
			NPC.npcSlots = 0f;
			NPC.aiStyle = -1;
			NPC.HitSound = SoundID.NPCHit4;
			NPC.DeathSound = null;
			NPC.HideStrikeDamage = true;
			NPC.ShowNameOnHover = false;
		}

		public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
		{
			// 不随难度翻倍，只随人数略增
			NPC.lifeMax = BaseLife + 600 * Math.Max(numPlayers - 1, 0);
		}

		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

		public override bool CheckActive() => false;

		public override void AI()
		{
			NPC boss = Main.npc[(int)NPC.ai[0]];
			if (!boss.active || boss.ModNPC is not SwordImmortal)
			{
				NPC.active = false;
				return;
			}
			Player target = Main.player[boss.target];
			if (Age == 0)
				origin = NPC.Center;
			Age++;
			phaseTimer++;

			switch (phase)
			{
				case Phase.Appear:
					// 落笔：在高处显形，剑尖慢慢转向玩家
					aimPoint = target.Center;
					dir = Vector2.Normalize(Vector2.Lerp(dir, (aimPoint - NPC.Center).SafeNormalize(Vector2.UnitY), 0.08f));
					NPC.velocity = Vector2.Zero;
					if (phaseTimer >= AppearTime)
						Enter(Phase.Draw, SoundID.Item71 with { Pitch = -0.8f });
					break;

				case Phase.Draw:
					// 行笔：朝玩家一路画下去，方向会微微修正，像笔锋追着人走
					aimPoint = target.Center;
					dir = Vector2.Normalize(Vector2.Lerp(dir, (aimPoint - Tip).SafeNormalize(dir), 0.09f));
					NPC.velocity = dir * DrawSpeed;
					Splatter(2);
					if (Vector2.Distance(Tip, target.Center) < SlowDistance || phaseTimer > MaxDrawTime)
						Enter(Phase.Slow, SoundID.Item29 with { Pitch = -0.9f, Volume = 0.8f });
					break;

				case Phase.Slow:
					// 接剑：世界放慢，剑锋悬在玩家面前、跟着玩家，一圈青光收拢
					aimPoint = target.Center;
					dir = Vector2.Normalize(Vector2.Lerp(dir, (aimPoint - Tip).SafeNormalize(dir), 0.05f));
					Vector2 wantTip = target.Center - dir * HoverGap;
					NPC.velocity = (wantTip - Tip) * 0.12f;
					DomainSystem.TargetLetterbox = 0.6f;
					if (phaseTimer % 30 == 0)
						SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -1f, Volume = 0.4f }, NPC.Center); // 心跳一样的余音
					if (phaseTimer >= SlowTime)
						Enter(Phase.Slam, SoundID.Item71 with { Pitch = -0.4f, Volume = 1f });
					break;

				case Phase.Slam:
					NPC.velocity = dir * SlamSpeed;
					Splatter(4);
					if (Vector2.Dot(Tip - aimPoint, dir) > 40f || Collision.SolidCollision(Tip - new Vector2(8f), 16, 16) || phaseTimer > 40)
						Land(boss);
					break;
			}
		}

		private void Enter(Phase next, SoundStyle sound)
		{
			phase = next;
			phaseTimer = 0;
			SoundEngine.PlaySound(sound, NPC.Center);
		}

		/// <summary>行笔时墨点飞溅</summary>
		private void Splatter(int count)
		{
			if (Main.dedServ)
				return;
			for (int i = 0; i < count; i++)
			{
				Vector2 side = new Vector2(-dir.Y, dir.X) * Main.rand.NextFloat(-50f, 50f);
				Dust d = Dust.NewDustPerfect(VisualCenter + side, DustID.Smoke, side * 0.05f - dir * 2f, 40, Fx.Ink, Main.rand.NextFloat(1.4f, 2.4f));
				d.noGravity = true;
			}
		}

		/// <summary>没接住：重创附近的玩家，但留一口气；天地被这一剑劈开一瞬。</summary>
		private void Land(NPC boss)
		{
			SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f }, Tip);
			// 一剑开天：领域里的人都躲不掉（离剑锋很远的才不受波及）
			Player me = Main.LocalPlayer;
			if (!Main.dedServ && me.active && !me.dead && me.Distance(Tip) < 2400f)
			{
				int damage = Math.Min((int)(me.statLifeMax2 * 0.6f), me.statLife - 1);
				if (damage > 0)
					me.Hurt(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral("")), damage, Math.Sign(dir.X), dodgeable: false, armorPenetration: 9999);
			}
			DomainSystem.Cleave(Tip, dir);
			if (boss.ModNPC is SwordImmortal immortal)
				immortal.StrokeMissed();
			Splatter(40);
			NPC.active = false;
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			if (!Main.dedServ)
			{
				// 每一下都震落几点墨
				for (int i = 0; i < 4; i++)
				{
					Dust d = Dust.NewDustPerfect(Tip + Main.rand.NextVector2Circular(30f, 30f), DustID.Smoke, Main.rand.NextVector2Circular(3f, 3f), 40, Fx.Ink, 1.6f);
					d.noGravity = true;
				}
			}
			if (NPC.life > 0 || broken)
				return;
			// 碎了：接住了
			broken = true;
			NPC boss = Main.npc[(int)NPC.ai[0]];
			if (boss.active && boss.ModNPC is SwordImmortal immortal)
				immortal.StrokeReceived(VisualCenter);
			SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.6f }, NPC.Center); // 一声清越的剑鸣
			SoundEngine.PlaySound(SoundID.Shatter, NPC.Center);
			if (!Main.dedServ)
			{
				// 碎成漫天墨点和青白的光屑
				for (int i = 0; i < 90; i++)
				{
					Vector2 p = VisualCenter + dir * Main.rand.NextFloat(-0.5f, 0.5f) * BladeLength;
					Dust d = Dust.NewDustPerfect(p, Main.rand.NextBool(3) ? DustID.Frost : DustID.Smoke, Main.rand.NextVector2Circular(10f, 10f), 30,
						Fx.Ink, Main.rand.NextFloat(1.6f, 3f));
					d.noGravity = true;
				}
			}
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Texture2D tex = Tex;
			float appear = MathHelper.Clamp(Age / AppearTime, 0f, 1f);
			float hurt = 1f - NPC.life / (float)NPC.lifeMax;
			// 受伤越重抖得越厉害，快碎了
			Vector2 jitter = Main.rand.NextVector2Circular(1f, 1f) * hurt * 6f;
			float rot = dir.ToRotation() - MathHelper.PiOver2; // 贴图剑尖朝下

			// 一笔：从落笔点到剑身，一路淋漓的墨痕
			if (phase != Phase.Appear)
			{
				const int n = 12;
				var pts = new Vector2[n];
				for (int i = 0; i < n; i++)
					pts[i] = Vector2.Lerp(VisualCenter, origin, i / (float)(n - 1));
				Trail.Draw(pts, p => MathHelper.Lerp(110f, 50f, p), p => Fx.Ink * (0.75f - 0.35f * p), Trail.Brush.Value);
			}

			// 接剑：剑锋上一圈青光，从大收到小；收拢的时候就是该出手的时候
			if (phase == Phase.Slow)
			{
				float t = phaseTimer / (float)SlowTime;
				Fx.Circle(spriteBatch, Fx.Ring.Value, Tip, MathHelper.Lerp(300f, 50f, t), new Color(120, 230, 220) * (0.4f + 0.5f * t));
				Fx.Circle(spriteBatch, Fx.SoftGlow.Value, Tip, 120f, new Color(120, 230, 220) * 0.35f);
			}

			// 墨晕：剑身外一层淡淡的墨气
			Fx.Circle(spriteBatch, Fx.SoftGlow.Value, VisualCenter, BladeLength * 1.1f, Fx.Ink * 0.18f * appear);
			spriteBatch.Draw(tex, VisualCenter + jitter - screenPos, null, Color.White * appear, rot, tex.Size() / 2f, 1f, SpriteEffects.None, 0f);
			return false;
		}
	}
}
