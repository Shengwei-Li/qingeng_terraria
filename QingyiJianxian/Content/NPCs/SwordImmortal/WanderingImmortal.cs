using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.NPCs.SwordImmortal
{
	/// <summary>
	/// 初遇 · 云上饮酒：剑仙倚在一朵云上喝酒，慢慢飘。不理人，也伤不到他。
	/// 第一下打他：「哦？」。再打——
	///   肉后：他起身拔剑，正式开战（换成 <see cref="SwordImmortal"/>）。
	///   肉前：他懒得起身，挥手一道剑气把你赶走；不知进退，一次比一次重，可能真的会死。
	///
	/// 他像世界里的一件事，不是跟着玩家的东西：
	///   出现后就停在那一带天空，以出现点为中心左右慢慢飘；附近没人了、或者天黑了，他就散成云雾走掉。
	///   自然出现每天最多一次。
	/// ai[0] = 状态，ai[1] = 被惹了几次，ai[2] = 状态计时，ai[3] = 出现点 X（0 表示还没安顿好）。
	/// </summary>
	public class WanderingImmortal : ModNPC
	{
		private const int DrinkFrames = 8, WaveFirst = 8, WaveFrames = 8;
		private const int RiseFirst = 16, RiseFrames = 8, RiseFrameTicks = 6; // 起身：云散人立
		private const int WaveFrameTicks = 4;
		private const int QiFrame = 4;           // 挥到第几帧时放出剑气
		private const float HoverHeight = 320f;  // 离地约 20 格：抬头就看得见，又不至于飘出屏幕
		private const float AppearOffset = 560f; // 出现在玩家左右这么远的空中，屏幕内就能看到
		private const float WanderRange = 240f;  // 以出现点为中心，左右各飘这么远
		private const float LeaveDistance = 3500f; // 附近约两屏内没有玩家……
		private const int LeaveDelay = 60 * 30;    // ……持续半分钟，他就走（跑开拿个东西回来，人还在）
		private const float FaceDeadzone = 48f;    // 玩家要偏出这么远他才转身，免得在正下方来回抽搐

		private enum Mode { Lounging, Waving, Leaving, Rising }

		private Mode CurrentMode
		{
			get => (Mode)NPC.ai[0];
			set => NPC.ai[0] = (float)value;
		}

		private ref float Provocations => ref NPC.ai[1];
		private ref float ModeTimer => ref NPC.ai[2];
		private ref float AnchorX => ref NPC.ai[3];
		private int talkCooldown;
		private int aloneTimer;
		private int drinkFrame, drinkCounter;
		private float fade = 1f;

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = DrinkFrames + WaveFrames + RiseFrames;
			NPCID.Sets.MPAllowedEnemies[Type] = true;
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
		}

		public override void SetDefaults()
		{
			NPC.width = 110;
			NPC.height = 110;
			NPC.lifeMax = 1000;
			NPC.immortal = true;          // 伤不到他
			NPC.HideStrikeDamage = true;
			NPC.damage = 0;
			NPC.defense = 999;
			NPC.knockBackResist = 0f;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.chaseable = false;        // 召唤物和追踪弹不会自己去打他，免得误伤
			NPC.aiStyle = -1;
			NPC.HitSound = SoundID.Tink;
			NPC.npcSlots = 0f;
		}

		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
		{
			// 肉前中期（打过骷髅王）以后，白天的地表，偶尔会遇到他
			if (!NPC.downedBoss3 || !Main.dayTime || !spawnInfo.Player.ZoneOverworldHeight || spawnInfo.Invasion
				|| WorldFlags.EncounteredToday || NPC.AnyNPCs(Type) || NPC.AnyNPCs(ModContent.NPCType<SwordImmortal>()) || NPC.AnyDanger())
				return 0f;
			return 0.004f;
		}

		// 什么时候走由他自己决定（见 AI），不用原版"离玩家远了就消失"的规则
		public override bool CheckActive() => false;

		public override void OnSpawn(IEntitySource source)
		{
			WorldFlags.EncounteredToday = true;
			Announce("Arrive"); // 全服提示：一缕酒香随风飘来……
		}

		private void Announce(string key)
		{
			Color color = new(170, 236, 228);
			if (Main.netMode == NetmodeID.Server)
				ChatHelper.BroadcastChatMessage(NetworkText.FromKey(this.GetLocalizationKey(key)), color);
			else if (Main.netMode == NetmodeID.SinglePlayer)
				Main.NewText(this.GetLocalization(key).Value, color);
		}

		public override void AI()
		{
			NPC.TargetClosest(false);
			Player player = Main.player[NPC.target];

			if (AnchorX == 0f)
			{
				// 原版刷怪把他放在屏幕外的地面上；挪到玩家身侧的半空，让人一抬头就看到。
				// 只由服务器（或单人）决定位置，再同步给大家
				if (Main.netMode == NetmodeID.MultiplayerClient)
					return;
				int side = NPC.Center.X >= player.Center.X ? 1 : -1;
				NPC.Center = HoverSpot(new Vector2(player.Center.X + side * AppearOffset, player.Center.Y));
				AnchorX = NPC.Center.X;
				NPC.direction = NPC.spriteDirection = -side; // 面朝玩家这边
				NPC.netUpdate = true;
			}

			if (talkCooldown > 0)
				talkCooldown--;

			if (CurrentMode is Mode.Lounging or Mode.Waving && Main.netMode != NetmodeID.MultiplayerClient)
			{
				// 天黑了就走，说一句；附近没人了也走，悄悄的
				bool anyoneNear = false;
				foreach (Player p in Main.ActivePlayers)
					if (!p.dead && p.Distance(NPC.Center) < LeaveDistance)
						anyoneNear = true;
				aloneTimer = anyoneNear ? 0 : aloneTimer + 1;

				if (!Main.dayTime || aloneTimer > LeaveDelay)
				{
					if (!Main.dayTime)
						Say("Leave");
					else
						Announce("Gone"); // 玩家不在跟前，看不到头顶的字，用聊天栏告诉他：人是自己走的
					CurrentMode = Mode.Leaving;
					ModeTimer = 0;
					NPC.netUpdate = true;
				}
			}

			switch (CurrentMode)
			{
				case Mode.Lounging:
					// 以出现点为中心左右慢慢飘（各客户端用同一个时钟算，不需要额外同步），高度跟着地形起伏
					float phase = (Main.GameUpdateCount + NPC.whoAmI * 97) * 0.004f;
					float targetX = AnchorX + MathF.Sin(phase) * WanderRange;
					Vector2 spot = HoverSpot(new Vector2(targetX, NPC.Center.Y));
					Vector2 want = new(MathHelper.Clamp((targetX - NPC.Center.X) * 0.02f, -0.6f, 0.6f), (spot.Y - NPC.Center.Y) * 0.02f);
					NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.05f);
					// 玩家靠近时提一句
					if (NPC.Distance(player.Center) < 240f && talkCooldown == 0 && Provocations == 0)
					{
						Say("Disturb");
						talkCooldown = 900;
					}
					break;

				case Mode.Waving:
					NPC.velocity *= 0.9f;
					if (ModeTimer == QiFrame * WaveFrameTicks)
						ReleaseQi(player);
					if (++ModeTimer >= WaveFrames * WaveFrameTicks)
					{
						CurrentMode = Mode.Lounging;
						ModeTimer = 0;
					}
					break;

				case Mode.Rising:
					// 起身：动画放完，站定的那一刻原地换成剑仙本体开打
					NPC.velocity *= 0.85f;
					if (++ModeTimer >= RiseFrames * RiseFrameTicks && Main.netMode != NetmodeID.MultiplayerClient)
					{
						// 末帧里站立的人物比画面中心偏右下一点（见 tools/make_textures.py 的拼接方式）
						Vector2 at = NPC.Center + new Vector2(6f * NPC.spriteDirection, 3f);
						int boss = NPC.NewNPC(NPC.GetSource_FromAI(), (int)at.X, (int)at.Y + 55, ModContent.NPCType<SwordImmortal>(), 0, 0, 0, 0, 0, NPC.target);
						if (boss < Main.maxNPCs)
						{
							Main.npc[boss].direction = Main.npc[boss].spriteDirection = NPC.spriteDirection;
							if (Main.netMode == NetmodeID.Server)
								NetMessage.SendData(MessageID.SyncNPC, number: boss);
						}
						NPC.active = false;
						if (Main.netMode == NetmodeID.Server)
							NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
					}
					break;

				case Mode.Leaving:
					NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(1.6f * NPC.direction, -2.4f), 0.03f);
					fade = 1f - MathHelper.Clamp((ModeTimer - 60f) / 90f, 0f, 1f);
					if (!Main.dedServ && Main.rand.NextBool(2))
					{
						Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(50f, 40f), DustID.Cloud, Main.rand.NextVector2Circular(1.5f, 1.5f), 120, default, 1.6f);
						d.noGravity = true;
					}
					if (++ModeTimer >= 150)
						NPC.active = false;
					break;
			}

			// 云边飘出几缕雾
			if (!Main.dedServ && Main.rand.NextBool(10))
			{
				Dust d = Dust.NewDustPerfect(NPC.Bottom + new Vector2(Main.rand.NextFloat(-40f, 40f), -8f), DustID.Cloud, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), 0.25f), 160, default, 1f);
				d.noGravity = true;
			}

			// 朝向：平时跟着自己飘的方向；被惹了才转头看玩家。都留一段死区，不会来回抽搐
			int face = NPC.direction == 0 ? 1 : NPC.direction;
			if (Provocations > 0 && CurrentMode != Mode.Leaving)
			{
				float dx = player.Center.X - NPC.Center.X;
				if (dx > FaceDeadzone)
					face = 1;
				else if (dx < -FaceDeadzone)
					face = -1;
			}
			else if (NPC.velocity.X > 0.08f)
				face = 1;
			else if (NPC.velocity.X < -0.08f)
				face = -1;
			NPC.direction = NPC.spriteDirection = face;
		}

		/// <summary>
		/// The point <see cref="HoverHeight"/> above the ground under x, and clear of solid tiles.
		/// Searches downward from well above <paramref name="from"/>, so a hill or a cave ceiling does not trap him.
		/// </summary>
		private Vector2 HoverSpot(Vector2 from)
		{
			int tx = (int)(from.X / 16f);
			int startY = (int)(from.Y / 16f) - 40;
			float y = from.Y - HoverHeight;
			for (int i = 0; i < 100; i++)
			{
				int ty = startY + i;
				if (!WorldGen.InWorld(tx, ty))
					continue;
				Tile t = Main.tile[tx, ty];
				Tile above = Main.tile[tx, ty - 1];
				// 第一块上方是空气的实心砖，就是地面
				if (t.HasTile && Main.tileSolid[t.TileType] && !t.IsActuated && !(above.HasTile && Main.tileSolid[above.TileType]))
				{
					y = ty * 16f - HoverHeight;
					break;
				}
			}
			var spot = new Vector2(from.X, y);
			for (int i = 0; i < 30 && Collision.SolidCollision(spot - NPC.Size / 2f, NPC.width, NPC.height); i++)
				spot.Y -= 16f;
			return spot;
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			if (CurrentMode is Mode.Leaving or Mode.Rising)
				return;
			Provocations++;

			if (!Main.dedServ)
			{
				// 随手挡开：几点青色火星
				for (int i = 0; i < 6; i++)
				{
					Dust d = Dust.NewDustPerfect(NPC.Center, DustID.Frost, Main.rand.NextVector2Circular(3f, 3f), 80, default, 1f);
					d.noGravity = true;
				}
			}

			if (Provocations == 1)
			{
				Say("Oh");
				return;
			}

			if (Main.hardMode)
			{
				// 肉后：好，陪你走几招——起身，云散，站定拔剑
				if (CurrentMode != Mode.Rising)
				{
					Say("Accept");
					CurrentMode = Mode.Rising;
					ModeTimer = 0;
					NPC.netUpdate = true;
					SoundEngine.PlaySound(SoundID.Item24 with { Pitch = -0.2f }, NPC.Center);
				}
				return;
			}

			// 肉前：懒得起身，挥手赶人
			if (CurrentMode == Mode.Lounging)
			{
				CurrentMode = Mode.Waving;
				ModeTimer = 0;
				NPC.netUpdate = true;
				int n = (int)Provocations - 1;
				Say(n == 1 ? "Dismiss1" : n == 2 ? "Dismiss2" : "Dismiss3");
			}
		}

		private void ReleaseQi(Player player)
		{
			SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f, Volume = 0.8f }, NPC.Center);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			// 一次比一次重：100 → 160 → 256 → 410 …
			int n = Math.Max((int)Provocations - 2, 0);
			float scale = MathF.Pow(1.6f, n);
			int damage = NPC.GetAttackDamage_ForProjectiles(100f * scale, 75f * scale);
			Vector2 hand = NPC.Center + new Vector2(NPC.spriteDirection * 14f, -12f);
			Vector2 vel = (player.Center - hand).SafeNormalize(Vector2.UnitX) * 24f;
			Projectile.NewProjectile(NPC.GetSource_FromAI(), hand, vel, ModContent.ProjectileType<DismissQi>(), damage, 14f, Main.myPlayer, NPC.target);
		}

		private void Say(string key)
		{
			if (Main.netMode != NetmodeID.Server)
				CombatText.NewText(NPC.Hitbox, Fx.PaleTeal, this.GetLocalization(key).Value, dramatic: key != "Disturb");
		}

		public override void FindFrame(int frameHeight)
		{
			int frame;
			if (CurrentMode == Mode.Waving)
				frame = WaveFirst + Math.Min((int)ModeTimer / WaveFrameTicks, WaveFrames - 1);
			else if (CurrentMode == Mode.Rising)
				frame = RiseFirst + Math.Min((int)ModeTimer / RiseFrameTicks, RiseFrames - 1);
			else
			{
				if (++drinkCounter >= 9)
				{
					drinkCounter = 0;
					drinkFrame = (drinkFrame + 1) % DrinkFrames;
				}
				frame = drinkFrame;
			}
			NPC.frame.Y = frame * frameHeight;
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Texture2D tex = TextureAssets.Npc[Type].Value;
			SpriteEffects fx = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
			float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f) * 3f;
			Color c = Color.Lerp(drawColor, Color.White, 0.45f) * fade;
			spriteBatch.Draw(tex, NPC.Center + new Vector2(0f, bob) - screenPos, NPC.frame, c, 0f, NPC.frame.Size() / 2f, 1f, fx, 0f);
			return false;
		}
	}
}
