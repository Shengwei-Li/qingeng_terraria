using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.NPCs.SwordImmortal
{
	/// <summary>
	/// 青衣剑仙 / The Azure-Clad Sword Immortal.
	/// M1 prototype: phase one only (问剑 / The Questioning Blade) with three attacks.
	///
	/// Movement follows "静如云，动如电": he stands on a small cloud and drifts calmly,
	/// repositions by dissolving into mist (blink) instead of flying across the screen,
	/// never enters solid tiles, and only the strike itself is fast.
	/// </summary>
	[AutoloadBossHead]
	public class SwordImmortal : ModNPC
	{
		private enum State
		{
			Intro,
			Hover,       // 空窗：玩家的输出时间
			Stroke,      // 一剑横江 / One Stroke Spans the River
			Swordflight, // 御剑术 / Swordflight
			Leaves,      // 落叶成剑 / Falling Leaves, Rising Blades
			Transition,  // 转入二阶段：剑意，起
			Flurry,      // 瞬身连斩 / Flickerstep Flurry（二阶段）
			River,       // 剑河 / River of Blades（二阶段）
			Ring,        // 剑阵 / The Bladeward Ring（二阶段）
			Hang,        // 万剑悬空 / Ten Thousand Swords Hang in Heaven（三阶段）
			Brush,       // 一笔画地 / One Brushstroke Bounds the Earth（三阶段）
			Eight,       // 永字八法 / The Eight Strokes of Eternity（三阶段）
			Drunk,       // 醉剑 / Drunken Blades（三阶段）
			Final,       // 最后一剑 / The Final Stroke
			Ending,      // 赠剑离去 / A Parting Gift
		}

		// 出招脚本：(招式, 一剑横江连斩几次)。招式之间直接衔接成连招，
		// 空窗（Hover）只出现在每套连招之后，那才是玩家放心输出的时间
		private static readonly (State State, int Strokes)[] Script1 =
		{
			(State.Stroke, 3), (State.Hover, 0),
			(State.Swordflight, 0), (State.Stroke, 1), (State.Hover, 0),
			(State.Leaves, 0), (State.Stroke, 2), (State.Hover, 0),
		};

		// 二阶段 · 剑意：瞬身连斩、剑河、剑阵加入，连招更长
		private static readonly (State State, int Strokes)[] Script2 =
		{
			(State.Flurry, 5), (State.Hover, 0),
			(State.River, 0), (State.Stroke, 2), (State.Hover, 0),
			(State.Ring, 0), (State.Swordflight, 0), (State.Flurry, 3), (State.Hover, 0),
			(State.Leaves, 0), (State.Stroke, 2), (State.Hover, 0),
		};

		// 三阶段 · 领域：天地空白，只剩剑
		private static readonly (State State, int Strokes)[] Script3 =
		{
			(State.Hang, 0), (State.Brush, 0), (State.Flurry, 3), (State.Hover, 0),
			(State.Eight, 0), (State.Drunk, 0), (State.Flurry, 3), (State.Hover, 0),
			(State.Hang, 0), (State.Stroke, 2), (State.Hover, 0),
			(State.Brush, 0), (State.Drunk, 0), (State.Flurry, 4), (State.Hover, 0),
		};

		private const float PhaseTwoLife = 0.6f;
		private const float PhaseThreeLife = 0.25f;
		private const float FinalLife = 0.05f;   // 到这里锁血，出最后一剑

		/// <summary>Timeline of a dash strike, counted from when the blink into position finishes.</summary>
		/// <param name="AimEnd">The warning line locks here; the gap to DashStart is the player's reaction time.</param>
		/// <param name="Lead">Frames of player velocity to lead the aim by.</param>
		private readonly record struct StrikeTiming(int WarnStart, int AimEnd, int DashStart, int DashEnd, int End,
			float Speed, float Distance, int BlinkFrames, float Lead);

		// 一剑横江：比肉后翅膀的水平速度快得多，只能读预警线躲，不能靠跑。锁定后留 14 帧反应
		private static readonly StrikeTiming StrokeT = new(6, 32, 46, 60, 80, 64f, 460f, 10, 12f);
		// 瞬身连斩：近距离、短预警、连续出剑；锁定后留 12 帧
		private static readonly StrikeTiming FlurryT = new(4, 18, 30, 39, 44, 60f, 300f, 6, 8f);

		private const float PursueDistance = 1600f; // 玩家逃出这个距离，剑仙瞬身追斩

		private const int BlinkHalf = 10;       // 化雾、凝形各用多少帧
		private const float DriftSpeed = 3.2f;  // 平时飘移的最高速度：仙人不急

		// 身体在贴图里的中心（朝右时），用于把人物对准判定框；脚底在中心下方约 58 像素
		private static readonly Vector2 FigureCenter = new(70f, 63f);
		private const float FeetY = 56f;

		// 贴图帧布局（见 tools/make_textures.py）：0-7 待机循环，8-15 施法（剑指），16-21 冲刺前倾
		private const int IdleFrames = 8;       // 待机循环：衣袖、长发、发带随风轻摆
		private const int IdleFrameTicks = 7;
		private const int CastFirst = 8, CastFrames = 8;
		private const int LungeFirst = 16, LungeFrames = 6;
		private const int TotalFrames = 22;

		// 各姿势下持剑手相对人物中心的位置（朝右时，屏幕像素），从动画帧里量出来的
		private static readonly Vector2 IdleHand = new(-18f, 8f);
		private static readonly Vector2 CastHand = new(-36f, 1f);   // 施法时剑垂在身后那只手里，前手捏剑指
		private static readonly Vector2 LungeHand = new(28f, -17f);

		private State CurrentState
		{
			get => (State)NPC.ai[0];
			set => NPC.ai[0] = (float)value;
		}

		private ref float Timer => ref NPC.ai[1];
		private ref float Repeats => ref NPC.ai[2];
		private ref float ScriptIndex => ref NPC.ai[3];

		// 需要同步的额外状态
		private Vector2 dashDir = Vector2.UnitX;
		private int side = 1;
		private int blinkTimer;          // >0 时正在瞬身
		private Vector2 blinkDest;
		private int strokeCount = 1;     // 本次一剑横江连斩几次
		private int pursueCooldown;
		private int phase = 1;
		private int blinkHalf = BlinkHalf;
		// 剑河当前这一波：河心 Y、缺口所在行、从哪边流过来、起点 X
		private float riverBaseY;
		private int riverGap;
		private int riverSide = 1;
		private float riverStartX;
		private Vector2 domainCenter;

		// 以剑问剑：本机玩家对他造成的伤害里，剑类占多少。只在本机记，认可之礼也在本机给
		private const float BladeAnswersShare = 0.6f;
		private long localDamage, localSwordDamage;
		// 夺剑：有人用清月剑打他，他夺剑在手，双剑形态（联机同步）
		private bool twin;
		private bool twinAnnounced;
		private bool reclaimedFromMe;   // 被夺的是本机玩家的剑

		// 以下只用于表现，不需要同步
		private int hitstop;             // 剑命中玩家时顿帧
		private int hitFlash;
		private float cloudAlpha = 0f;   // 开场从无到有聚起来
		private readonly Vector2[] trail = new Vector2[8];
		private readonly Vector2[] inkStroke = new Vector2[16]; // 冲刺画下的一笔墨，停下后留在原地慢慢淡去
		private float inkFade;
		private readonly Vector2[] ripplePos = new Vector2[6];
		private readonly int[] rippleAge = new int[6];
		private float swordAngle = MathHelper.PiOver2;
		private Vector2 swordHand;
		private float castPose;          // 0 = 待机，1 = 施法姿势定格；进出都是渐变，不跳帧
		private float lungePose;
		private int idleFrame;
		private int idleCounter;

		private bool Striking => CurrentState == State.Stroke || CurrentState == State.Flurry;
		private StrikeTiming T => CurrentState == State.Flurry ? FlurryT : StrokeT;
		private bool Dashing => Striking && blinkTimer == 0 && Timer >= T.DashStart && Timer < T.DashEnd;
		private (State State, int Strokes)[] Script => phase switch { 1 => Script1, 2 => Script2, _ => Script3 };

		/// <summary>True while any Sword Immortal is in phase two or later; drives the ink-wash sky.</summary>
		public static bool InkWashActive()
		{
			int i = NPC.FindFirstNPC(ModContent.NPCType<SwordImmortal>());
			return i >= 0 && Main.npc[i].ModNPC is SwordImmortal boss && boss.phase >= 2 && boss.CurrentState != State.Ending;
		}
		private bool Blinking => blinkTimer > 0;
		private float BodyAlpha => Blinking ? Math.Abs(blinkTimer - blinkHalf) / (float)blinkHalf : 1f;
		private Player Target => Main.player[NPC.target];
		private Vector2 Feet => NPC.Center + new Vector2(0f, FeetY);

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = TotalFrames;
			NPCID.Sets.MPAllowedEnemies[Type] = true;
			NPCID.Sets.BossBestiaryPriority.Add(Type);
		}

		public override void SetDefaults()
		{
			NPC.width = 44;
			NPC.height = 110;
			NPC.lifeMax = 45000;
			NPC.damage = 90;
			NPC.defense = 40;
			NPC.knockBackResist = 0f;
			NPC.boss = true;
			NPC.noGravity = true;
			NPC.noTileCollide = true; // 地形由 AI 自己避让，见 MoveTo 和 FindClearSpot
			NPC.lavaImmune = true;
			NPC.aiStyle = -1;
			NPC.npcSlots = 10f;
			NPC.value = Item.buyPrice(gold: 10);
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			Music = MusicID.Boss2; // 占位，之后换成原创配乐
		}

		public override void FindFrame(int frameHeight)
		{
			if (++idleCounter >= IdleFrameTicks)
			{
				idleCounter = 0;
				idleFrame = (idleFrame + 1) % IdleFrames;
			}

			int frame;
			if (lungePose > 0.05f)
				frame = LungeFirst + (int)MathF.Round(lungePose * (LungeFrames - 1));
			else if (castPose > 0.05f)
				frame = CastFirst + (int)MathF.Round(castPose * (CastFrames - 1));
			else
				frame = idleFrame;
			NPC.frame.Y = frame * frameHeight;
		}

		public override bool CanHitPlayer(Player target, ref int cooldownSlot) => Dashing;

		/// <summary>问剑，不是杀人：他不会死。致命一击只会把他逼到最后一剑。</summary>
		public override bool CheckDead()
		{
			NPC.life = 1;
			NPC.dontTakeDamage = true;
			if (CurrentState is not (State.Final or State.Ending))
			{
				phase = 3;
				SwitchTo(State.Final);
			}
			return false;
		}

		public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
		{
			hitstop = 5;
			Shake(dashDir, 7f, 14);
		}

		public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
			=> RecordHit(player, damageDone, Swords.IsSword(item), item.type == ModContent.ItemType<Items.Clearmoon>());

		public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;
			bool clearmoon = projectile.type == ModContent.ProjectileType<Projectiles.ClearmoonSwing>()
				|| projectile.type == ModContent.ProjectileType<Projectiles.ClearmoonQi>();
			RecordHit(Main.player[projectile.owner], damageDone, Swords.IsSwordProjectile(projectile), clearmoon);
		}

		private void RecordHit(Player player, int damage, bool sword, bool clearmoon)
		{
			if (player.whoAmI != Main.myPlayer)
				return;
			localDamage += damage;
			if (sword)
				localSwordDamage += damage;
			// 拿我的剑来问我？——夺剑
			if (clearmoon && !twin && CurrentState is not (State.Final or State.Ending))
			{
				reclaimedFromMe = true;
				player.AddBuff(ModContent.BuffType<Buffs.Disarmed>(), 60 * 60 * 30);
				if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					ModPacket packet = Mod.GetPacket();
					packet.Write((byte)QingyiJianxian.Message.Reclaim);
					packet.Write((byte)NPC.whoAmI);
					packet.Send();
				}
				else
					BecomeTwin();
			}
		}

		/// <summary>夺剑在手：之后每一剑冲刺收势时，都用清月剑甩出一道月牙追斩。</summary>
		public void BecomeTwin()
		{
			twin = true;
			NPC.netUpdate = true;
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			hitFlash = 6;
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.WriteVector2(dashDir);
			writer.Write((sbyte)side);
			writer.Write((byte)blinkTimer);
			writer.WriteVector2(blinkDest);
			writer.Write((byte)strokeCount);
			writer.Write((byte)phase);
			writer.Write((byte)blinkHalf);
			writer.Write(riverBaseY);
			writer.Write((sbyte)riverGap);
			writer.Write((sbyte)riverSide);
			writer.Write(riverStartX);
			writer.WriteVector2(domainCenter);
			writer.Write(twin);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			dashDir = reader.ReadVector2();
			side = reader.ReadSByte();
			blinkTimer = reader.ReadByte();
			blinkDest = reader.ReadVector2();
			strokeCount = reader.ReadByte();
			phase = reader.ReadByte();
			blinkHalf = reader.ReadByte();
			riverBaseY = reader.ReadSingle();
			riverGap = reader.ReadSByte();
			riverSide = reader.ReadSByte();
			riverStartX = reader.ReadSingle();
			domainCenter = reader.ReadVector2();
			twin = reader.ReadBoolean();
		}

		public override void AI()
		{
			if (!AcquireTarget())
				return;

			Player player = Target;
			UpdateVisuals(player);
			Lighting.AddLight(NPC.Center, 0.35f, 0.6f, 0.6f);
			UpdateDomain();

			if (twin && !twinAnnounced)
			{
				// 伸手夹住剑锋：画面一顿
				twinAnnounced = true;
				Say("Reclaim");
				hitstop = 10;
				Shake(Vector2.UnitX, 6f, 16);
				SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.3f }, NPC.Center);
			}

			if (hitstop > 0)
			{
				hitstop--;
				NPC.position -= NPC.velocity; // 原地定格一瞬
				return;
			}

			if (Blinking)
			{
				UpdateBlink();
				return;
			}

			NPC.dontTakeDamage = CurrentState is State.Transition or State.Final or State.Ending;

			// 血量过六成之下，转入二阶段（不打断正在出的剑）
			if (phase == 1 && NPC.life < NPC.lifeMax * PhaseTwoLife && !Striking)
			{
				phase = 2;
				ScriptIndex = 0;
				SwitchTo(State.Transition);
			}
			// 两成五之下，展开领域
			else if (phase == 2 && NPC.life < NPC.lifeMax * PhaseThreeLife && !Striking)
			{
				phase = 3;
				ScriptIndex = 0;
				SwitchTo(State.Transition);
			}
			// 只剩一口气：不再打，出最后一剑
			else if (phase == 3 && NPC.life <= NPC.lifeMax * FinalLife && !Striking
				&& CurrentState is not (State.Transition or State.Final or State.Ending))
			{
				SwitchTo(State.Final);
			}

			if (pursueCooldown > 0)
				pursueCooldown--;
			if (!Striking && CurrentState is not (State.Intro or State.Transition or State.Final or State.Ending) && pursueCooldown == 0
				&& NPC.Distance(player.Center) > PursueDistance)
			{
				Pursue();
				return;
			}

			switch (CurrentState)
			{
				case State.Intro: Intro(player); break;
				case State.Hover: Hover(player); break;
				case State.Stroke: Stroke(player); break;
				case State.Swordflight: Swordflight(player); break;
				case State.Leaves: Leaves(player); break;
				case State.Transition: Transition(player); break;
				case State.Flurry: Stroke(player); break;
				case State.River: River(player); break;
				case State.Ring: Ring(player); break;
				case State.Hang: Hang(player); break;
				case State.Brush: Brush(player); break;
				case State.Eight: Eight(player); break;
				case State.Drunk: Drunk(player); break;
				case State.Final: Final(player); break;
				case State.Ending: Ending(player); break;
			}

			// 转身看向玩家；玩家在正下方附近时不转，免得来回抽搐
			if (!Dashing && !(CurrentState == State.Ending && Timer >= EndingDepart))
			{
				float dx = player.Center.X - NPC.Center.X;
				if (Math.Abs(dx) > 40f)
					NPC.direction = NPC.spriteDirection = dx > 0 ? 1 : -1;
				else if (NPC.direction == 0)
					NPC.direction = NPC.spriteDirection = 1;
			}
		}

		// 战斗中他不会因为离玩家远就消失（原版的远离消失规则关掉）。
		// 玩家跑多远，他都会瞬身追过去（见 Pursue）；只有所有人都倒下了，他才离开
		public override bool CheckActive() => false;

		private const int DefeatLeaveTime = 120;
		private int defeatTimer;

		/// <summary>Picks a living target. When everyone is down: "剑心未定，改日再来。" and he drifts up and away.</summary>
		private bool AcquireTarget()
		{
			if (NPC.target < 0 || NPC.target == 255 || Target.dead || !Target.active)
				NPC.TargetClosest(false);

			if (Target.dead || !Target.active)
			{
				if (defeatTimer == 0)
					Say("Defeat");
				defeatTimer++;
				NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0f, -3f), 0.05f);
				if (defeatTimer >= DefeatLeaveTime && Main.netMode != NetmodeID.MultiplayerClient)
				{
					NPC.active = false;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
				}
				return false;
			}
			defeatTimer = 0;
			return true;
		}

		private void Intro(Player player)
		{
			if (Timer == 0)
			{
				// 从云上起身的就在原地开打；乘云远道而来（一壶酒请来的）才瞬身到跟前
				if (NPC.Distance(player.Center) > 900f)
					BlinkTo(player.Center + new Vector2(side * 360f, -160f));
				Timer = 1;
				return;
			}
			if (Timer == 10)
			{
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f, Volume = 0.6f }, NPC.Center); // 拔剑出鞘
				AddRipple(Feet);
				if (WorldFlags.DownedSwordImmortal)
					Say("Again"); // 又是你。
			}
			if (++Timer >= 50)
				NextInScript();
		}

		private void Hover(Player player)
		{
			side = NPC.Center.X < player.Center.X ? -1 : 1;
			Vector2 spot = player.Center + new Vector2(side * 340f, -170f);

			// 离得太远就瞬身，不追着飞
			if (Timer == 0 && NPC.Distance(spot) > 700f)
				BlinkTo(spot);
			else
				Drift(spot);

			if (++Timer >= (phase == 1 ? 50 : 40))
				NextInScript();
		}

		private void Stroke(Player player)
		{
			if (Timer == 0)
			{
				Timer = 1;
				if (CurrentState == State.Flurry)
				{
					// 瞬身连斩：在玩家头顶半圈上换着位置出现，每一剑方向都不同。
					// 位置由连斩序号决定（不用随机数），各客户端算出来一样
					float angle = -MathHelper.PiOver2 + MathF.Sin(Repeats * 2.3f + ScriptIndex) * 1.35f;
					BlinkTo(player.Center + angle.ToRotationVector2() * T.Distance, T.BlinkFrames);
				}
				else
				{
					// 从玩家的另一侧出剑，每次都换边
					side = NPC.Center.X < player.Center.X ? 1 : -1;
					BlinkTo(player.Center + new Vector2(side * T.Distance, -40f), T.BlinkFrames);
				}
				return;
			}

			if (Timer < T.DashStart)
			{
				// 静：立于云上，剑尖锁定玩家；最后十帧方向冻结，给玩家反应时间
				NPC.velocity *= 0.85f;
				if (Timer < T.AimEnd)
				{
					// 一剑横江的第一剑直取当前位置，教会玩家读线；之后的连斩和瞬身连斩都预判走位
					bool lead = CurrentState == State.Flurry || Repeats > 0;
					Vector2 aim = lead ? player.Center + player.velocity * T.Lead : player.Center;
					dashDir = NPC.DirectionTo(aim);
				}
				else if (Timer == T.AimEnd)
				{
					SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f, Volume = 0.7f }, NPC.Center);
					NPC.netUpdate = true;
				}
			}
			else if (Timer == T.DashStart)
			{
				// 动：一剑过去
				NPC.velocity = dashDir * T.Speed;
				NPC.direction = NPC.spriteDirection = dashDir.X >= 0 ? 1 : -1;
				AddRipple(Feet);
				Shake(dashDir, 4f, 10);
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f }, NPC.Center);
			}
			else if (Timer < T.DashEnd)
			{
				// 撞到地形就提前收剑，不穿地。
				// 只检查身体中心一小块：玩家站在地上时，剑仙要贴地掠过才砍得到，
				// 用整个 110 高的判定框去查，脚一碰地就会提前停下，永远冲不到玩家
				Vector2 core = NPC.Center + NPC.velocity - new Vector2(10f);
				if (Collision.SolidCollision(core, 20, 20))
				{
					NPC.velocity = Vector2.Zero;
					Timer = T.DashEnd;
					AddRipple(Feet);
					Shake(dashDir, 5f, 10);
				}
				// 墨痕：沿冲刺路径留下延时爆开的墨点
				else if (Main.netMode != NetmodeID.MultiplayerClient && (int)Timer % 3 == 0)
				{
					int delay = 28 + (int)(Timer - T.DashStart) * 2;
					Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
						ModContent.ProjectileType<InkBurst>(), ProjDamage(65, 50), 0f, Main.myPlayer, delay);
				}
			}
			else
			{
				// 收剑即止：急停，脚下荡开涟漪
				if (Timer == T.DashEnd)
				{
					AddRipple(Feet);
					// 双剑形态：收势时清月剑甩出一道月牙追斩
					if (twin && Main.netMode != NetmodeID.MultiplayerClient)
					{
						Vector2 v = NPC.DirectionTo(player.Center) * 20f;
						Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, v, ModContent.ProjectileType<DismissQi>(),
							ProjDamage(65, 50), 6f, Main.myPlayer, NPC.target);
					}
				}
				NPC.velocity *= 0.6f;
				// 贴地掠过后脚可能陷进地里，慢慢浮起来
				if (Collision.SolidCollision(NPC.position, NPC.width, NPC.height))
					NPC.position.Y -= 4f;
			}

			if (++Timer >= T.End)
			{
				if (++Repeats < strokeCount)
				{
					Timer = 0;
					NPC.netUpdate = true;
				}
				else
				{
					NextInScript();
				}
			}
		}

		private void Swordflight(Player player)
		{
			Vector2 spot = player.Center + new Vector2(side * 420f, -240f);
			if (Timer == 0 && NPC.Distance(spot) > 500f)
			{
				Timer = 1;
				BlinkTo(spot);
				return;
			}
			Drift(spot);

			if (Timer == 10)
			{
				SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.3f }, NPC.Center);
				if (Main.netMode != NetmodeID.MultiplayerClient)
				{
					for (int i = 0; i < FlyingSword.Count; i++)
					{
						Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
							ModContent.ProjectileType<FlyingSword>(), ProjDamage(70, 52), 0f, Main.myPlayer,
							NPC.whoAmI, i, 64 + i * 10);
					}
				}
			}

			// 最后一把剑出手后不等，直接接下一招
			if (++Timer >= 64 + (FlyingSword.Count - 1) * 10 + 14)
				NextInScript();
		}

		private void Leaves(Player player)
		{
			Vector2 spot = player.Center + new Vector2(side * 280f, -300f);
			if (Timer == 0)
			{
				Timer = 1;
				BlinkTo(spot);
				return;
			}
			Drift(spot);

			if (Timer < 45 && (int)Timer % 2 == 1 && Main.netMode != NetmodeID.MultiplayerClient)
			{
				var pos = new Vector2(player.Center.X + Main.rand.NextFloat(-720f, 720f), player.Center.Y - 560f - Main.rand.NextFloat(80f));
				// 所有落叶在同一时刻化剑
				int transformAt = 130 - (int)Timer;
				Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
					ModContent.ProjectileType<FallingLeaf>(), ProjDamage(60, 45), 0f, Main.myPlayer, transformAt);
			}
			if (Timer == 130)
				SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.5f }, player.Center);

			// 落叶出手的同时接下一招
			if (++Timer >= 150)
				NextInScript();
		}

		private void Transition(Player player)
		{
			// 转阶段：升到玩家上方，停手、说话。二阶段铺开水墨山水，三阶段以此为中心展开领域。期间不受伤
			if (Timer == 0)
			{
				Timer = 1;
				if (phase >= 3)
				{
					domainCenter = player.Center;
					NPC.netUpdate = true;
				}
				BlinkTo(player.Center + new Vector2(0f, -300f));
				return;
			}
			NPC.velocity *= 0.9f;
			if (Timer == 20)
				Say(phase >= 3 ? "PhaseThree" : "PhaseTwo");
			if (Timer == 30)
			{
				AddRipple(Feet);
				Shake(Vector2.UnitY, 6f, 30);
				SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.3f }, NPC.Center);
			}
			if (++Timer >= 90)
				NextInScript();
		}

		// 剑河：三波，每波先画出河道（朱砂细线）和缺口（淡青），40 帧后剑流过来
		private const int RiverWaves = 3;
		private const int RiverFirstWarn = 10;
		private const int RiverWaveGap = 55;
		private const int RiverWarnTime = 40;

		private void River(Player player)
		{
			Vector2 spot = player.Center + new Vector2(side * 520f, -300f);
			if (Timer == 0)
			{
				Timer = 1;
				BlinkTo(spot);
				return;
			}
			Drift(spot);

			for (int wave = 0; wave < RiverWaves; wave++)
			{
				int warnAt = RiverFirstWarn + wave * RiverWaveGap;
				if (Timer == warnAt && Main.netMode != NetmodeID.MultiplayerClient)
				{
					riverBaseY = player.Center.Y;
					riverGap = Main.rand.Next(-RiverSword.RowsHalf + 3, RiverSword.RowsHalf - 2);
					riverSide = wave % 2 == 0 ? side : -side; // 每波换一边流
					riverStartX = player.Center.X - riverSide * RiverSword.StartOffset;
					NPC.netUpdate = true;
				}
				if (Timer == warnAt + RiverWarnTime)
				{
					SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.6f }, player.Center);
					if (Main.netMode != NetmodeID.MultiplayerClient)
					{
						for (int row = -RiverSword.RowsHalf; row <= RiverSword.RowsHalf; row++)
						{
							if (Math.Abs(row - riverGap) <= RiverSword.GapHalf)
								continue;
							float y = riverBaseY + row * RiverSword.Spacing;
							Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(riverStartX, y), Vector2.Zero,
								ModContent.ProjectileType<RiverSword>(), ProjDamage(70, 52), 0f, Main.myPlayer, y, riverSide);
						}
					}
				}
			}

			if (++Timer >= RiverFirstWarn + (RiverWaves - 1) * RiverWaveGap + RiverWarnTime + 30)
				NextInScript();
		}

		/// <summary>Is a river wave being warned about right now? Used for the telegraph.</summary>
		private bool RiverWarning()
		{
			if (CurrentState != State.River || Blinking)
				return false;
			for (int wave = 0; wave < RiverWaves; wave++)
			{
				int warnAt = RiverFirstWarn + wave * RiverWaveGap;
				if (Timer >= warnAt && Timer < warnAt + RiverWarnTime)
					return true;
			}
			return false;
		}

		// 剑阵：两重剑圈先后围住玩家，各留一个缺口，立阵之后收拢
		private void Ring(Player player)
		{
			Vector2 spot = player.Center + new Vector2(side * 380f, -280f);
			if (Timer == 0)
			{
				Timer = 1;
				BlinkTo(spot);
				return;
			}
			Drift(spot);

			if ((Timer == 10 || Timer == 70) && Main.netMode != NetmodeID.MultiplayerClient)
			{
				Vector2 center = player.Center;
				int gap = Main.rand.Next(RingSword.Slots);
				float spin = Main.rand.NextFloat(MathHelper.TwoPi);
				for (int i = 0; i < RingSword.Slots; i++)
				{
					// 缺口两把剑宽
					if (i == gap || i == (gap + 1) % RingSword.Slots)
						continue;
					Vector2 pos = center + (spin + MathHelper.TwoPi * i / RingSword.Slots).ToRotationVector2() * RingSword.Radius;
					Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
						ModContent.ProjectileType<RingSword>(), ProjDamage(70, 52), 0f, Main.myPlayer, center.X, center.Y, 60);
				}
			}
			if (Timer == 10 || Timer == 70)
				SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.1f }, player.Center);
			if (Timer == 70 || Timer == 130)
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.4f, Volume = 0.6f }, player.Center);

			if (++Timer >= 150)
				NextInScript();
		}

		// ───────────── 三阶段 · 领域 ─────────────

		/// <summary>Keeps the domain (ink ground, blank sky) and the ending's letterbox alive while needed.</summary>
		private void UpdateDomain()
		{
			bool ending = CurrentState == State.Ending;
			if (phase >= 3 && !ending)
			{
				DomainSystem.TargetDomain = 1f;
				// 玩家用传送之类的办法离开了领域：领域跟着他重新展开
				if (Main.netMode != NetmodeID.MultiplayerClient && Target.Distance(domainCenter) > DomainSystem.Radius + 1000f)
				{
					domainCenter = Target.Center;
					NPC.netUpdate = true;
				}
				DomainSystem.Center = domainCenter;

				// 领域边界：玩家出不去，碰到边就被推回来
				if (!Main.dedServ && DomainSystem.Domain > 0.5f)
				{
					Player me = Main.LocalPlayer;
					Vector2 off = me.Center - domainCenter;
					float d = off.Length();
					if (me.active && !me.dead && d > DomainSystem.Radius)
						me.velocity = -off / d * 9f;
				}
			}
			if (ending)
			{
				DomainSystem.TargetLetterbox = Timer < EndingGone - 40 ? 1f : 0f;
				// 演出期间玩家无敌
				if (!Main.dedServ)
				{
					Player me = Main.LocalPlayer;
					me.immune = true;
					me.immuneNoBlink = true;
					me.immuneTime = Math.Max(me.immuneTime, 2);
				}
			}
		}

		// 万剑悬空：两排墨剑悬在空白的天上，一排从左往右、一排从右往左依次落下
		private const int HangCols = 15;
		private const float HangSpacing = 92f;

		private void Hang(Player player)
		{
			Vector2 spot = player.Center + new Vector2(side * 340f, -320f);
			if (Timer == 0)
			{
				Timer = 1;
				BlinkTo(spot);
				return;
			}
			Drift(spot);

			if (Timer == 10)
			{
				SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f }, player.Center);
				if (Main.netMode != NetmodeID.MultiplayerClient)
				{
					for (int row = 0; row < 2; row++)
					{
						for (int c = 0; c < HangCols; c++)
						{
							float x = player.Center.X + (c - HangCols / 2) * HangSpacing + row * HangSpacing / 2f;
							float y = player.Center.Y - 540f - row * 90f;
							int delay = row == 0 ? 50 + c * 6 : 74 + (HangCols - 1 - c) * 6;
							Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x, y), Vector2.Zero,
								ModContent.ProjectileType<InkSword>(), ProjDamage(75, 55), 0f, Main.myPlayer, delay);
						}
					}
				}
			}

			if (++Timer >= 10 + 74 + (HangCols - 1) * 6 + 40)
				NextInScript();
		}

		// 一笔画地：两笔，各留一道窄缝，第二笔反方向
		private void Brush(Player player)
		{
			Vector2 spot = player.Center + new Vector2(side * 360f, -380f);
			if (Timer == 0)
			{
				Timer = 1;
				BlinkTo(spot);
				return;
			}
			Drift(spot);

			if ((Timer == 10 || Timer == 110) && Main.netMode != NetmodeID.MultiplayerClient)
			{
				int dir = Timer == 10 ? side : -side;
				// 缝开在玩家身边或上方的空中，绝不开进地里
				float gapY = player.Center.Y - Main.rand.NextFloat(0f, 200f);
				for (int i = 0; i < 30 && Collision.SolidCollision(new Vector2(player.Center.X - 20f, gapY - BrushSweep.GapHalf + 4f), 40, (int)(BrushSweep.GapHalf * 2f - 8f)); i++)
					gapY -= 16f;
				float startX = player.Center.X - dir * BrushSweep.Length / 2f;
				Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(startX, gapY), Vector2.Zero,
					ModContent.ProjectileType<BrushSweep>(), ProjDamage(80, 60), 0f, Main.myPlayer, gapY, dir, startX);
			}

			if (++Timer >= 220)
				NextInScript();
		}

		// 永字八法：在玩家所在处写一个巨大的"永"字，八笔按笔顺落下
		private void Eight(Player player)
		{
			if (Timer == 0)
			{
				Timer = 1;
				BlinkTo(player.Center + new Vector2(side * 200f, -560f));
				return;
			}
			NPC.velocity *= 0.9f;
			if (Timer == 6)
				Say("Eternity");
			if (Timer == 10 && Main.netMode != NetmodeID.MultiplayerClient)
			{
				for (int i = 0; i < CalligraphyStroke.StrokeCount; i++)
					Projectile.NewProjectile(NPC.GetSource_FromAI(), player.Center, Vector2.Zero,
						ModContent.ProjectileType<CalligraphyStroke>(), ProjDamage(75, 55), 0f, Main.myPlayer, i, i * CalligraphyStroke.StrokeGap);
			}
			// 写字时也不闲着：每隔一会儿掷一把飞剑追人（三阶段不画红线，只有寒光）
			if (Timer > 30 && (int)Timer % 40 == 0 && Timer < 10 + CalligraphyStroke.EndAt && Main.netMode != NetmodeID.MultiplayerClient)
				Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<FlyingSword>(),
					ProjDamage(70, 52), 0f, Main.myPlayer, NPC.whoAmI, FlyingSword.Count / 2, 24);
			if (++Timer >= 10 + CalligraphyStroke.EndAt + 20)
				NextInScript();
		}

		// 醉剑：仰头一口酒，前倾喷出三片酒雾，酒雾凝成细剑扇形射来
		private const int DrunkSprayAt = 40;
		private const int DrunkWaves = 3;
		private const int DrunkWaveGap = 12;

		private void Drunk(Player player)
		{
			Vector2 spot = player.Center + new Vector2(side * 380f, -140f);
			if (Timer == 0)
			{
				Timer = 1;
				BlinkTo(spot);
				return;
			}
			Drift(spot);
			if (Timer == 8)
			{
				Say("Drink");
				SoundEngine.PlaySound(SoundID.Item3, NPC.Center);
			}
			for (int w = 0; w < DrunkWaves; w++)
			{
				if (Timer != DrunkSprayAt + w * DrunkWaveGap)
					continue;
				SoundEngine.PlaySound(SoundID.Item13 with { Pitch = 0.3f }, NPC.Center);
				if (Main.netMode != NetmodeID.MultiplayerClient)
				{
					Vector2 mouth = NPC.Center + new Vector2(NPC.spriteDirection * 18f, -34f);
					float baseAngle = (player.Center - mouth).ToRotation();
					const int count = 11;
					for (int i = 0; i < count; i++)
					{
						// 扇形 ±35°，每一波错开半格，缝不在同一个地方
						float a = baseAngle + MathHelper.ToRadians(-35f + 70f * (i + (w % 2) * 0.5f) / (count - 1));
						float speed = Main.rand.NextFloat(10f, 15f);
						Projectile.NewProjectile(NPC.GetSource_FromAI(), mouth, a.ToRotationVector2() * speed,
							ModContent.ProjectileType<WineBlade>(), ProjDamage(60, 45), 0f, Main.myPlayer);
					}
				}
			}
			if (++Timer >= DrunkSprayAt + DrunkWaves * DrunkWaveGap + 50)
				NextInScript();
		}

		// ───────────── 最后一剑与结局 ─────────────

		// 最后一剑 · 人剑合一，一剑开天：
		//   说"最后一剑。"，天上的法相睁眼；剑仙化作一道青光升天，没入法相（法相泛起青辉）；
		//   法相这一剑由 FinalSword 自天顶一笔画下。没接住就等一会儿再来一剑。
		private const int MergeStart = 30;
		private const int MergeEnd = 70;
		private const int FinalSwordAt = 85;
		private const int RetryDelay = 100;
		private int retryTimer;

		/// <summary>人剑合一后剑仙本人看不见：化光的那几十帧里渐隐。</summary>
		private float MergeAlpha => CurrentState == State.Final ? 1f - MathHelper.Clamp((Timer - MergeStart) / (MergeEnd - MergeStart), 0f, 1f) : 1f;

		private void Final(Player player)
		{
			if (Timer == 0)
			{
				Timer = 1;
				retryTimer = 0;
				KillOwnProjectiles();
				BlinkTo(player.Center + new Vector2(0f, -380f));
				return;
			}
			if (Timer == 10)
				Say("FinalStroke");
			if (Timer >= 10)
				DomainSystem.TargetColossusEye = 1f; // 法相睁眼

			if (Timer < MergeStart)
				NPC.velocity *= 0.9f;
			else if (Timer < MergeEnd)
			{
				// 化作一道青光，直上云霄
				NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0f, -18f), 0.15f);
				DomainSystem.TargetColossusMerge = 1f;
				if (!Main.dedServ)
				{
					Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(16f, 40f), DustID.Frost, -NPC.velocity * 0.2f, 60, default, 1.6f);
					d.noGravity = true;
				}
				if (Timer == MergeStart)
					SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.2f }, NPC.Center);
			}
			else
			{
				// 已没入法相：本人藏在玩家头顶高处，跟着玩家
				DomainSystem.TargetColossusMerge = 1f;
				NPC.velocity = Vector2.Zero;
				NPC.Center = player.Center + new Vector2(0f, -1400f);
			}

			if (Timer == FinalSwordAt)
			{
				SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.6f }, player.Center);
				if (Main.netMode != NetmodeID.MultiplayerClient)
				{
					// 法相的剑自天顶落笔
					side = Main.rand.NextBool() ? -1 : 1;
					Vector2 at = player.Center + new Vector2(side * 220f, -900f);
					NPC.NewNPC(NPC.GetSource_FromAI(), (int)at.X, (int)at.Y + 75, ModContent.NPCType<FinalSword>(), 0, NPC.whoAmI);
				}
			}

			if (retryTimer > 0 && --retryTimer == 0)
				Timer = FinalSwordAt - 1; // 再来一剑
			if (Timer <= FinalSwordAt)
				Timer++;
		}

		/// <summary>Called by the Final Sword when it lands unbroken: wait a moment, then strike again.</summary>
		public void StrokeMissed()
		{
			if (CurrentState != State.Final)
				return;
			retryTimer = RetryDelay;
			Shake(Vector2.UnitX, 14f, 30); // 天地被劈开，镜头重重一震
		}

		/// <summary>Called by the Final Sword when it is broken: the Immortal steps out of the colossus beside it.</summary>
		public void StrokeReceived(Vector2 at)
		{
			if (CurrentState != State.Final)
				return;
			NPC.Center = FindClearSpot(at + new Vector2(0f, -120f));
			NPC.velocity = Vector2.Zero;
			MistBurst(NPC.Center);
			hitstop = 12;
			Shake(Vector2.UnitY, 10f, 24);
			SwitchTo(State.Ending);
		}

		// 结局时间轴
		private const int EndingApproach = 40;
		private const int EndingGift = 130;
		private const int EndingDepart = 190;
		private const int EndingGone = 460;
		private const int EndingFade = 90;     // 最后这么多帧里化作云雾散去

		private float DepartAlpha => (CurrentState == State.Ending ? 1f - MathHelper.Clamp((Timer - (EndingGone - EndingFade)) / EndingFade, 0f, 1f) : 1f)
			* (1f - defeatTimer / (float)DefeatLeaveTime) * MergeAlpha;

		private void Ending(Player player)
		{
			if (Timer == 0)
				KillOwnProjectiles();

			if (Timer == EndingApproach)
			{
				// 落到玩家身旁
				side = NPC.Center.X < player.Center.X ? -1 : 1;
				BlinkTo(player.Center + new Vector2(side * 260f, -10f));
				Timer++;
				return;
			}
			if (Timer < EndingApproach)
				NPC.velocity *= 0.9f;
			else if (Timer < EndingDepart)
				Drift(player.Center + new Vector2(side * 110f, -10f)); // 缓步走近
			else
			{
				// 乘云而去：转身，斜斜升上天，越飞越高
				NPC.direction = NPC.spriteDirection = side;
				float rise = MathHelper.Clamp((Timer - EndingDepart) / 120f, 0f, 1f);
				NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(side * 2.2f, MathHelper.Lerp(-1.6f, -4.2f, rise)), 0.04f);
				// 身后拖出云气；临走时整个人化作一团云雾，向四周散开
				if (!Main.dedServ)
				{
					bool dissolving = Timer >= EndingGone - EndingFade;
					int count = dissolving ? 4 : 1;
					for (int i = 0; i < count; i++)
					{
						Vector2 at = dissolving ? NPC.Center + Main.rand.NextVector2Circular(30f, 60f) : Feet + new Vector2(Main.rand.NextFloat(-36f, 36f), 4f);
						Vector2 vel = dissolving ? Main.rand.NextVector2Circular(2.2f, 2.2f) : new Vector2(-side * 0.8f, 0.6f);
						Dust d = Dust.NewDustPerfect(at, DustID.Cloud, vel, 120, default, Main.rand.NextFloat(1.2f, 2.2f));
						d.noGravity = true;
					}
				}
			}

			if (Timer == EndingApproach + 30)
				Say("Received");
			if (Timer == EndingGift)
				GiveGifts(player);
			if (Timer == EndingGift + 15)
				LocalRecognition();
			if (Timer == EndingGift + 45)
				Say("Farewell");

			if (++Timer >= EndingGone)
			{
				WorldFlags.DownedSwordImmortal = true;
				if (Main.netMode == NetmodeID.Server)
					NetMessage.SendData(MessageID.WorldData);
				NPC.active = false;
				if (Main.netMode == NetmodeID.Server)
					NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
			}
		}

		/// <summary>
		/// 本机玩家的认可之礼（每位玩家在自己的客户端上判定、自己拿）：
		///   夺过剑的：剑还你，外加剑穗和几块云纹绡；
		///   以剑问剑（对他的伤害里剑类占六成以上）：认可一句，没有清月剑的给一把，外加云纹绡和药酒。
		/// </summary>
		private void LocalRecognition()
		{
			if (Main.dedServ)
				return;
			Player me = Main.LocalPlayer;
			var source = NPC.GetSource_Loot();
			if (reclaimedFromMe)
			{
				Say("ReturnBlade");
				me.ClearBuff(ModContent.BuffType<Buffs.Disarmed>());
				me.QuickSpawnItem(source, ModContent.ItemType<Items.InkwakeTassel>());
				me.QuickSpawnItem(source, ModContent.ItemType<Items.CloudweaveSilk>(), 4);
				return;
			}
			bool fought = localDamage > NPC.lifeMax * 0.05f;
			if (fought && localSwordDamage >= localDamage * BladeAnswersShare)
			{
				Say("BladeAnswers");
				if (!me.HasItem(ModContent.ItemType<Items.Clearmoon>()))
					me.QuickSpawnItem(source, ModContent.ItemType<Items.Clearmoon>());
				me.QuickSpawnItem(source, ModContent.ItemType<Items.CloudweaveSilk>(), 4);
				me.QuickSpawnItem(source, ModContent.ItemType<Items.AzureCloudDraught>(), 3);
			}
		}

		/// <summary>赠剑：装备从剑仙手里抛出，划一道弧线落在玩家脚边。</summary>
		private void GiveGifts(Player player)
		{
			SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f }, NPC.Center);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			// 每次必给：云纹绡（织青衫，约两次凑齐）和青云药酒。
			// 第一次问剑：清月剑和一壶酒必给（赠剑是剧情）；之后清月剑 1/3。
			// 剑穗 1/2，御剑诀（坐骑）1/4，是再来打的动力
			bool first = !WorldFlags.DownedSwordImmortal;
			var gifts = new System.Collections.Generic.List<(int type, int stack)>
			{
				(ModContent.ItemType<Items.CloudweaveSilk>(), Main.rand.Next(10, 13)),
				(ModContent.ItemType<Items.AzureCloudDraught>(), Main.rand.Next(5, 9)),
			};
			if (first || Main.rand.NextBool(3))
				gifts.Add((ModContent.ItemType<Items.Clearmoon>(), 1));
			if (first)
				gifts.Add((ModContent.ItemType<Items.CloudbrewGourd>(), 1));
			if (Main.rand.NextBool(2))
				gifts.Add((ModContent.ItemType<Items.InkwakeTassel>(), 1));
			if (Main.rand.NextBool(4))
				gifts.Add((ModContent.ItemType<Items.LitanyOfSwordflight>(), 1));
			float toPlayer = Math.Sign(player.Center.X - NPC.Center.X);
			for (int i = 0; i < gifts.Count; i++)
			{
				int index = Item.NewItem(NPC.GetSource_Loot(), NPC.Center + swordHand, 8, 8, gifts[i].type, gifts[i].stack);
				// 一件件抛出，落点散开在玩家脚边
				Main.item[index].velocity = new Vector2(toPlayer * (1.8f + i * 0.9f), -6.5f - (i % 2) * 1.5f);
				if (Main.netMode == NetmodeID.Server)
					NetMessage.SendData(MessageID.SyncItem, -1, -1, null, index, 1f);
			}
		}

		private void KillOwnProjectiles()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			foreach (Projectile proj in Main.ActiveProjectiles)
			{
				if (proj.hostile && proj.ModProjectile?.Mod == Mod)
					proj.Kill();
			}
		}

		private void Say(string key)
		{
			if (Main.netMode != NetmodeID.Server)
				CombatText.NewText(NPC.Hitbox, phase >= 3 ? Fx.Teal : Fx.PaleTeal, this.GetLocalization(key).Value, dramatic: true);
		}

		private void DrawDomainBoundary(SpriteBatch sb)
		{
			float d = DomainSystem.Domain;
			if (d <= 0.01f || phase < 3)
				return;
			// 领域边界：几笔带飞白的墨圈，笔与笔之间留一点白，整圈缓缓转动。
			// 外面再晕一层很淡的墨，像纸上洇开
			const int arcs = 7, pointsPerArc = 22;
			float spin = Main.GlobalTimeWrappedHourly * 0.08f;
			float span = MathHelper.TwoPi / arcs;
			var pts = new Vector2[pointsPerArc];
			for (int k = 0; k < arcs; k++)
			{
				float start = spin + k * span;
				float length = span * (0.82f + 0.1f * MathF.Sin(k * 2.1f));     // 每笔长短不一
				float radius = DomainSystem.Radius + 14f * MathF.Sin(k * 1.7f); // 每笔略有出入
				for (int i = 0; i < pointsPerArc; i++)
				{
					// 笔锋在前：从这一笔的末端往回取点
					float ang = start + length * (1f - i / (float)(pointsPerArc - 1));
					pts[i] = domainCenter + ang.ToRotationVector2() * radius;
				}
				float width = 38f + 10f * MathF.Sin(k * 1.3f);
				Trail.Draw(pts, p => width * (0.55f + 0.45f * MathF.Sin(MathF.PI * (0.15f + 0.85f * (1f - p)))),
					p => Fx.Ink * 0.75f * d, Trail.Brush.Value);
				Trail.Draw(pts, p => width * 4f, p => Fx.Ink * 0.05f * d, Trail.Glow.Value);
			}
		}

		private void NextInScript()
		{
			var (state, strokes) = Script[(int)ScriptIndex % Script.Length];
			ScriptIndex++;
			strokeCount = Math.Max(strokes, 1);
			SwitchTo(state);
		}

		/// <summary>避战者，剑必至：玩家跑远了，瞬身到身边直接出剑。</summary>
		private void Pursue()
		{
			pursueCooldown = 300;
			strokeCount = 1;
			SwitchTo(State.Stroke);
			if (Main.netMode != NetmodeID.Server)
				CombatText.NewText(NPC.Hitbox, Fx.PaleTeal, this.GetLocalization("Pursue").Value, dramatic: true);
		}

		private void SwitchTo(State state)
		{
			CurrentState = state;
			Timer = 0;
			Repeats = 0;
			NPC.netUpdate = true;
		}

		// ───────────── 移动 ─────────────

		/// <summary>Slow, eased drift. Stops instead of entering tiles; if stuck, blinks.</summary>
		private void Drift(Vector2 target)
		{
			target = FindClearSpot(target);
			Vector2 toTarget = target - NPC.Center;
			float dist = toTarget.Length();
			Vector2 desired = dist > 1f ? toTarget / dist * MathF.Min(DriftSpeed, dist / 30f) : Vector2.Zero;
			NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.05f);

			if (Collision.SolidCollision(NPC.position + NPC.velocity, NPC.width, NPC.height))
			{
				NPC.velocity = Vector2.Zero;
				if (dist > 120f)
					BlinkTo(target);
			}
		}

		/// <summary>Dissolve into mist here, reform at the destination.</summary>
		private void BlinkTo(Vector2 dest, int half = BlinkHalf)
		{
			blinkDest = FindClearSpot(dest);
			blinkHalf = half;
			blinkTimer = half * 2;
			NPC.velocity = Vector2.Zero;
			MistBurst(NPC.Center);
			SoundEngine.PlaySound(SoundID.Item24 with { Pitch = 0.6f, Volume = 0.6f }, NPC.Center);
			NPC.netUpdate = true;
		}

		private void UpdateBlink()
		{
			NPC.velocity = Vector2.Zero;
			blinkTimer--;
			if (blinkTimer == blinkHalf)
			{
				NPC.Center = blinkDest;
				MistBurst(NPC.Center);
				AddRipple(Feet);
			}
		}

		/// <summary>Moves a point upward until the boss's hitbox there is free of solid tiles.</summary>
		private Vector2 FindClearSpot(Vector2 center)
		{
			for (int i = 0; i < 40; i++)
			{
				Vector2 topLeft = center - NPC.Size / 2f;
				// 多留一格余量，脚下的云也别陷进地里
				if (!Collision.SolidCollision(topLeft - new Vector2(4f), NPC.width + 8, NPC.height + 24))
					return center;
				center.Y -= 16f;
			}
			return center;
		}

		// 原版的折算方式：敌方弹幕伤害在专家/大师模式会被放大，所以专家值给得更低
		private int ProjDamage(int normal, int expert) => NPC.GetAttackDamage_ForProjectiles(normal, expert);

		// ───────────── 表现 ─────────────

		private static float Approach(float value, float target, float step)
			=> value < target ? MathF.Min(value + step, target) : MathF.Max(value - step, target);

		private void Shake(Vector2 dir, float strength, int frames)
		{
			if (Main.netMode == NetmodeID.Server)
				return;
			Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, dir, strength, 6f, frames, 1600f, "QingyiJianxian/Shake"));
		}

		private void MistBurst(Vector2 at)
		{
			if (Main.netMode == NetmodeID.Server)
				return;
			for (int i = 0; i < 24; i++)
			{
				Dust d = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(26f, 54f), DustID.Cloud,
					Main.rand.NextVector2Circular(2.5f, 2.5f), 120, default, Main.rand.NextFloat(1.1f, 1.8f));
				d.noGravity = true;
			}
		}

		private void AddRipple(Vector2 at)
		{
			int slot = 0;
			for (int i = 1; i < rippleAge.Length; i++)
				if (rippleAge[i] > rippleAge[slot])
					slot = i;
			ripplePos[slot] = at;
			rippleAge[slot] = 1;
		}

		private void UpdateVisuals(Player player)
		{
			for (int i = trail.Length - 1; i > 0; i--)
				trail[i] = trail[i - 1];
			trail[0] = NPC.Center;

			for (int i = 0; i < rippleAge.Length; i++)
				if (rippleAge[i] > 0 && ++rippleAge[i] > 40)
					rippleAge[i] = 0;

			if (Dashing)
			{
				if (Timer == T.DashStart)
				{
					Array.Clear(inkStroke);
					inkStroke[0] = NPC.Center - NPC.velocity; // 从起点落笔
				}
				for (int i = inkStroke.Length - 1; i > 0; i--)
					inkStroke[i] = inkStroke[i - 1];
				inkStroke[0] = NPC.Center;
				inkFade = 1f;
			}
			else if (inkFade > 0f && (inkFade -= 1f / 30f) <= 0f)
			{
				Array.Clear(inkStroke);
			}

			if (hitFlash > 0)
				hitFlash--;

			// 冲刺和瞬身时脚下的云散开，停下后重新聚拢
			bool cloudGone = Dashing || Blinking || (Striking && Timer >= T.DashEnd);
			cloudAlpha = MathHelper.Lerp(cloudAlpha, cloudGone ? 0f : 1f, cloudGone ? 0.35f : 0.06f);

			// 姿势：御剑、落叶时捏剑指施法；一剑横江锁定方向后前倾，冲完再缓缓收势
			bool casting = (CurrentState == State.Swordflight && Timer < 150) || (CurrentState == State.Leaves && Timer < 140)
				|| CurrentState is State.River or State.Ring or State.Hang or State.Brush or State.Eight
				|| (CurrentState == State.Drunk && Timer < DrunkSprayAt)
				|| (CurrentState == State.Transition && Timer > 10) || (CurrentState == State.Final && Timer > 10);
			bool lunging = (Striking && !Blinking && Timer >= T.AimEnd && Timer < T.DashEnd + 10)
				|| (CurrentState == State.Drunk && Timer >= DrunkSprayAt && Timer < DrunkSprayAt + DrunkWaves * DrunkWaveGap + 10); // 前倾喷酒
			castPose = Approach(castPose, casting ? 1f : 0f, casting ? 1f / 30f : 1f / 18f);
			lungePose = Approach(lungePose, lunging ? 1f : 0f, lunging ? 1f / 8f : 1f / 14f);

			int dir = NPC.spriteDirection;
			Vector2 hand = Vector2.Lerp(IdleHand, CastHand, castPose);
			hand = Vector2.Lerp(hand, LungeHand, lungePose);
			hand.X *= dir;

			float target;
			if (Dashing)
				target = dashDir.ToRotation();
			else if (Striking && Timer >= T.WarnStart && Timer < T.DashStart)
				target = (-dashDir).ToRotation() - 0.5f * dir; // 收剑蓄势
			else if (castPose > 0.05f)
				target = MathHelper.PiOver2 + 0.98f * dir;     // 剑垂身后，斜指地面
			else
				target = MathHelper.PiOver2 + 0.5f * dir;      // 垂剑于身侧
			swordAngle = swordAngle.AngleLerp(target, Dashing ? 0.5f : 0.18f);
			swordHand = Vector2.Lerp(swordHand, hand, 0.3f);

			if (Dashing && Main.netMode != NetmodeID.Server)
			{
				for (int i = 0; i < 3; i++)
				{
					Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20f, 40f), DustID.Smoke,
						-NPC.velocity * 0.05f, 120, Fx.Ink, 1.6f);
					d.noGravity = true;
				}
			}
			// 云边偶尔飘出一缕雾
			else if (cloudAlpha > 0.5f && Main.netMode != NetmodeID.Server && Main.rand.NextBool(8))
			{
				Dust d = Dust.NewDustPerfect(Feet + new Vector2(Main.rand.NextFloat(-36f, 36f), 6f), DustID.Cloud,
					new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), 0.3f), 160, default, 0.9f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Texture2D body = TextureAssets.Npc[Type].Value;
			int dir = NPC.spriteDirection;
			SpriteEffects fx = dir == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
			Vector2 origin = dir == 1 ? FigureCenter : new Vector2(NPC.frame.Width - FigureCenter.X, FigureCenter.Y);
			float bob = Dashing ? 0f : MathF.Sin(Main.GlobalTimeWrappedHourly * 2.0f) * 3f;
			Vector2 pos = NPC.Center + new Vector2(0f, bob);
			float alpha = BodyAlpha * DepartAlpha;

			DrawRipples(spriteBatch);
			DrawTelegraph(spriteBatch);
			DrawDomainBoundary(spriteBatch);
			DrawInkStroke();

			// 残影：只在出剑时出现
			float speed = NPC.velocity.Length();
			if (speed > 10f)
			{
				for (int i = trail.Length - 1; i >= 1; i--)
				{
					if (trail[i] == Vector2.Zero)
						continue;
					float a = (1f - i / (float)trail.Length) * MathHelper.Clamp(speed / 40f, 0f, 1f) * 0.55f;
					spriteBatch.Draw(body, trail[i] - screenPos, NPC.frame, Fx.Teal * a, 0f, origin, NPC.scale, fx, 0f);
				}
			}

			// 仙人自带一点光：夜里和洞里也不会黑成剪影
			Color lit = NPC.GetAlpha(Color.Lerp(drawColor, Color.White, 0.45f)) * alpha;
			spriteBatch.Draw(body, pos - screenPos, NPC.frame, lit, 0f, origin, NPC.scale, fx, 0f);
			DrawSword(spriteBatch, pos + swordHand, lit);
			if (twin)
			{
				// 夺来的清月剑握在另一只手，和青锋错开一个角度
				Texture2D moon = ModContent.Request<Texture2D>("QingyiJianxian/Content/Items/Clearmoon").Value;
				var hilt2 = new Vector2(10f, moon.Height - 10f);
				Vector2 hand2 = pos + new Vector2(14f * dir, 4f);
				spriteBatch.Draw(moon, hand2 - screenPos, null, lit, swordAngle - 0.8f * dir + MathHelper.PiOver4, hilt2, 0.85f, SpriteEffects.None, 0f);
			}
			if (CurrentState == State.Drunk && Timer >= 4 && Timer < DrunkSprayAt)
			{
				// 仰头饮酒：葫芦举到嘴边
				Texture2D gourd = ModContent.Request<Texture2D>("QingyiJianxian/Content/Items/CloudbrewGourd").Value;
				Vector2 mouth = pos + new Vector2(dir * 14f, -40f);
				spriteBatch.Draw(gourd, mouth - screenPos, null, lit, -0.9f * dir, gourd.Size() / 2f, 0.6f, dir == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
			}
			DrawCloud(spriteBatch, pos, drawColor);

			if (hitFlash > 0)
			{
				// 受击闪白：叠一层加色
				BeginBlend(spriteBatch, BlendState.Additive);
				spriteBatch.Draw(body, pos - screenPos, NPC.frame, Color.White * (hitFlash / 6f) * 0.35f * alpha, 0f, origin, NPC.scale, fx, 0f);
				BeginBlend(spriteBatch, BlendState.AlphaBlend);
			}
			return false;
		}

		private void DrawInkStroke()
		{
			if (inkFade <= 0f)
				return;
			// 一笔浓墨：头粗尾细，尾部飞白；再压一道淡青的锋芒
			float fade = inkFade * inkFade;
			Trail.Draw(inkStroke, p => MathHelper.Lerp(72f, 30f, p), p => Fx.Ink * 0.92f * fade, Trail.Brush.Value);
			Trail.Draw(inkStroke, p => 12f * (1f - p), p => Fx.PaleTeal * 0.6f * fade * (1f - p), Trail.Glow.Value, additive: true);
		}

		private void DrawCloud(SpriteBatch sb, Vector2 pos, Color drawColor)
		{
			if (cloudAlpha < 0.02f)
				return;
			Texture2D cloud = ModContent.Request<Texture2D>("QingyiJianxian/Content/NPCs/SwordImmortal/FootCloud").Value;
			// 云不随人上下浮动太多，只跟一半，脚像是踩进云里
			Vector2 at = new Vector2(pos.X, NPC.Center.Y + (pos.Y - NPC.Center.Y) * 0.5f + FeetY - 4f);
			float breathe = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.3f) * 0.03f;
			Color c = Color.Lerp(drawColor, Color.White, 0.6f) * cloudAlpha * DepartAlpha * 0.95f;
			sb.Draw(cloud, at - Main.screenPosition, null, c, 0f, cloud.Size() / 2f, new Vector2(breathe, 1f), SpriteEffects.None, 0f);
		}

		private void DrawRipples(SpriteBatch sb)
		{
			Texture2D ring = Fx.Ring.Value;
			for (int i = 0; i < rippleAge.Length; i++)
			{
				if (rippleAge[i] <= 0)
					continue;
				float t = rippleAge[i] / 40f;
				float w = MathHelper.Lerp(30f, 220f, 1f - (1f - t) * (1f - t));
				Color c = Fx.PaleTeal * (1f - t) * 0.8f;
				// 压扁的圆环，像踩在水面上荡开
				sb.Draw(ring, ripplePos[i] - Main.screenPosition, null, c, 0f, ring.Size() / 2f, new Vector2(w / ring.Width, w * 0.28f / ring.Height), SpriteEffects.None, 0f);
			}
		}

		private void DrawSword(SpriteBatch sb, Vector2 hand, Color color)
		{
			Texture2D sword = ModContent.Request<Texture2D>("QingyiJianxian/Content/NPCs/SwordImmortal/Azurebrand").Value;
			// 贴图里剑尖指向右上（-45°），剑柄在左下
			var hilt = new Vector2(12f, sword.Height - 18f);
			sb.Draw(sword, hand - Main.screenPosition, null, color, swordAngle + MathHelper.PiOver4, hilt, 1f, SpriteEffects.None, 0f);
		}

		private void DrawTelegraph(SpriteBatch sb)
		{
			if (RiverWarning())
			{
				DrawRiverTelegraph(sb);
				return;
			}
			if (!Striking || Blinking || Timer < T.WarnStart || Timer >= T.DashStart)
				return;

			bool locked = Timer >= T.AimEnd;
			int tier = TelegraphTier(NPC);
			// 锁定那一下，剑尖寒光一闪——这是所有难度都保留的提示
			if (locked && Timer < T.AimEnd + 8)
				Glint(sb, NPC.Center + swordHand + swordAngle.ToRotationVector2() * 60f, 1f - (Timer - T.AimEnd) / 8f);
			// 一阶段画完整红线；二阶段红线只在瞄准时出现，锁定即消失；三阶段不画
			if (tier >= 2 || (tier == 1 && locked))
				return;
			float progress = (Timer - T.WarnStart) / (T.AimEnd - T.WarnStart);
			float alpha = locked ? (Timer % 4 < 2 ? 1f : 0.6f) : MathHelper.Clamp(progress, 0.15f, 0.8f);
			float thickness = locked ? 4f : 2f;
			Vector2 end = NPC.Center + dashDir * (T.Speed * (T.DashEnd - T.DashStart) + 200f);
			Fx.Line(sb, NPC.Center, end, Fx.Cinnabar * alpha, thickness);
		}

		/// <summary>
		/// 预警线的强度，按阶段递减：0 = 完整红线（一阶段，教学），1 = 只在瞄准时出现、锁定即消失（二阶段），
		/// 2 = 不画红线，只靠剑尖寒光、姿势和声音（三阶段）。大师模式从一阶段起就是 1。
		/// 指出"往哪躲"的安全区提示（剑河的缝、剑阵的缺口、一笔画地的缝）不受影响。
		/// </summary>
		public static int TelegraphTier(NPC boss)
		{
			if (boss.ModNPC is not SwordImmortal immortal)
				return 0;
			int tier = immortal.phase - 1;
			if (Main.masterMode)
				tier = Math.Max(tier, 1);
			return Math.Min(tier, 2);
		}

		/// <summary>剑尖寒光：一颗短暂的四芒星。</summary>
		public static void Glint(SpriteBatch sb, Vector2 at, float strength)
		{
			if (strength <= 0f)
				return;
			Fx.Circle(sb, Fx.SoftGlow.Value, at, 70f * strength, new Color(170, 240, 235) * 0.7f * strength);
			Fx.Line(sb, at - new Vector2(34f * strength, 0f), at + new Vector2(34f * strength, 0f), Color.White * strength, 2f);
			Fx.Line(sb, at - new Vector2(0f, 34f * strength), at + new Vector2(0f, 34f * strength), Color.White * strength, 2f);
		}

		private void DrawRiverTelegraph(SpriteBatch sb)
		{
			// 每一行画出它将要流过的正弦河道；缺口画成一条淡青的宽带，告诉玩家往哪钻
			const int segments = 24;
			float length = RiverSword.StartOffset * 2f;
			float pulse = 0.55f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f);
			for (int row = -RiverSword.RowsHalf; row <= RiverSword.RowsHalf; row++)
			{
				bool gap = Math.Abs(row - riverGap) <= RiverSword.GapHalf;
				if (gap && row != riverGap)
					continue;
				float baseY = riverBaseY + row * RiverSword.Spacing;
				// 缺口画得淡一些，像一道墨晕留白，提示但不抢眼
				Color c = gap ? Fx.PaleTeal * 0.16f : Fx.Cinnabar * 0.22f * pulse;
				float thick = gap ? RiverSword.Spacing * (RiverSword.GapHalf * 2 + 1) * 0.6f : 1.5f;
				for (int i = 0; i < segments; i++)
				{
					float d0 = length * i / segments, d1 = length * (i + 1) / segments;
					var a = new Vector2(riverStartX + riverSide * d0, RiverSword.RowY(baseY, d0));
					var b = new Vector2(riverStartX + riverSide * d1, RiverSword.RowY(baseY, d1));
					Fx.Line(sb, a, b, c, thick);
				}
			}
		}

		private static void BeginBlend(SpriteBatch sb, BlendState blend)
		{
			sb.End();
			sb.Begin(SpriteSortMode.Deferred, blend, Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
		}
	}
}
