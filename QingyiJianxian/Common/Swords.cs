using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Common
{
	/// <summary>
	/// 剑类判定：近战、挥砍式（阔剑、刀刃）或刺击式（短剑），且不是镐斧锤。
	/// 原版和大多数模组的剑都能认出来；自己用特殊方式出招的剑（如清月剑的手持连斩）登记在 <see cref="Extra"/> 里。
	/// </summary>
	public static class Swords
	{
		public static readonly HashSet<int> Extra = new();

		public static bool IsSword(Item item)
		{
			if (item == null || item.IsAir || item.damage <= 0)
				return false;
			if (Extra.Contains(item.type))
				return true;
			if (!item.DamageType.CountsAsClass(DamageClass.Melee))
				return false;
			if (item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.consumable || item.channel)
				return false;
			return item.useStyle == ItemUseStyleID.Swing || item.useStyle == ItemUseStyleID.Rapier;
		}

		/// <summary>A melee projectile fired while holding a sword counts as the sword (Terra Blade beams, Clearmoon's swings).</summary>
		public static bool IsSwordProjectile(Projectile proj)
		{
			if (!proj.friendly || proj.owner < 0 || proj.owner >= Main.maxPlayers)
				return false;
			return proj.DamageType.CountsAsClass(DamageClass.Melee) && IsSword(Main.player[proj.owner].HeldItem);
		}
	}

	/// <summary>
	/// Per-player sword state: the tassel, Clearmoon's combo counter, and the Azure Wayfarer set bonus
	/// (身法 Cloudstep dash + 剑意 Sword-Will stacks).
	/// </summary>
	public class SwordPlayer : ModPlayer
	{
		public bool Tassel;
		public bool WayfarerSet;
		public bool Crown;

		// 罡气护体（青衫两件 + 发冠）：被击中后小青剑绕身 3 秒，期间受伤 -25%，冷却 20 秒
		private const int GuardSwords = 4;
		private const int GuardCooldown = 60 * 20;
		private int guardTimer;
		private int guardCooldown;
		private bool GuardActive => WayfarerSet && Crown;
		public int ComboStep;
		public int ComboTimer;

		// 剑意：剑类命中叠层，每层剑类伤害 +4%、攻速 +3%，最多 5 层；三秒不出剑就散
		public const int MaxWill = 5;
		private const int WillDuration = 180;
		public int WillStacks;
		private int willTimer;

		// 身法：双击左右方向键，踏云冲刺；起步一瞬无敌
		private const int DashLeft = 3, DashRight = 2; // 与原版 doubleTapCardinalTimer 的下标一致
		private const float DashSpeed = 15f;
		private const int DashCooldown = 45;
		private const int DashDuration = 14;
		private int dashDir;
		private int dashDelay;
		private int dashTimer;

		public override void ResetEffects()
		{
			Tassel = false;
			WayfarerSet = false;
			Crown = false;

			dashDir = 0;
			if (Player.controlRight && Player.releaseRight && Player.doubleTapCardinalTimer[DashRight] < 15)
				dashDir = 1;
			else if (Player.controlLeft && Player.releaseLeft && Player.doubleTapCardinalTimer[DashLeft] < 15)
				dashDir = -1;
		}

		public override void PreUpdateMovement()
		{
			if (dashDelay > 0)
				dashDelay--;
			if (dashTimer > 0)
			{
				dashTimer--;
				// 冲刺途中身后留下云雾
				if (!Main.dedServ)
				{
					Dust d = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(10f, 20f), DustID.Cloud, -Player.velocity * 0.15f, 120, default, 1.4f);
					d.noGravity = true;
				}
			}

			bool canDash = WayfarerSet && Player.dashType == 0 && !Player.setSolar && !Player.mount.Active && !Player.CCed;
			if (!canDash || dashDir == 0 || dashDelay > 0)
				return;

			Player.velocity.X = dashDir * DashSpeed;
			Player.immune = true;
			Player.immuneNoBlink = false;
			Player.immuneTime = System.Math.Max(Player.immuneTime, 12);
			dashDelay = DashCooldown;
			dashTimer = DashDuration;
			if (!Main.dedServ)
			{
				Terraria.Audio.SoundEngine.PlaySound(SoundID.Item24 with { Pitch = 0.6f, Volume = 0.6f }, Player.Center);
				for (int i = 0; i < 16; i++)
				{
					Dust d = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(14f, 24f), DustID.Cloud, Main.rand.NextVector2Circular(2f, 2f), 120, default, 1.6f);
					d.noGravity = true;
				}
			}
		}

		public override void PostUpdate()
		{
			// 停手一秒，连招从头开始
			if (ComboTimer > 0 && --ComboTimer == 0)
				ComboStep = 0;

			if (guardTimer > 0)
				guardTimer--;
			if (guardCooldown > 0 && --guardCooldown == 0 && GuardActive && Player.whoAmI == Main.myPlayer)
			{
				// 冷却好了：剑光一闪，心里有数
				Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.8f, Volume = 0.35f }, Player.Center);
				for (int i = 0; i < 12; i++)
				{
					Dust d = Dust.NewDustPerfect(Player.Center + (MathHelper.TwoPi * i / 12f).ToRotationVector2() * 30f, DustID.Frost, Vector2.Zero, 100, default, 1f);
					d.noGravity = true;
				}
			}

			// 剑被夺只在这一战里：剑仙不在了就解除
			int disarmed = ModContent.BuffType<Content.Buffs.Disarmed>();
			if (Player.HasBuff(disarmed) && !NPC.AnyNPCs(ModContent.NPCType<Content.NPCs.SwordImmortal.SwordImmortal>()))
				Player.ClearBuff(disarmed);

			if (willTimer > 0 && --willTimer == 0)
				WillStacks = 0;
			if (!WayfarerSet)
				WillStacks = 0;
			if (WillStacks > 0)
				Player.AddBuff(ModContent.BuffType<Content.Buffs.SwordWill>(), 2);
		}

		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			if (guardTimer > 0)
				modifiers.FinalDamage *= 0.75f;
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			if (!GuardActive || guardCooldown > 0 || Player.whoAmI != Main.myPlayer)
				return;
			guardTimer = Content.Projectiles.GuardSword.Duration;
			guardCooldown = GuardCooldown;
			// 护体剑的伤害随手上的剑走；没拿剑就按一个基础值
			int damage = Swords.IsSword(Player.HeldItem) ? (int)(Player.GetWeaponDamage(Player.HeldItem) * 0.6f) : 50;
			for (int i = 0; i < GuardSwords; i++)
				Projectile.NewProjectile(Player.GetSource_FromThis("GuardingQi"), Player.Center, Vector2.Zero,
					ModContent.ProjectileType<Content.Projectiles.GuardSword>(), damage, 4f, Player.whoAmI, i, GuardSwords);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f }, Player.Center);
		}

		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Swords.IsSword(item))
				OnSwordHit(target, hit, damageDone);
		}

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Swords.IsSwordProjectile(proj))
				OnSwordHit(target, hit, damageDone);
		}

		private void OnSwordHit(NPC target, NPC.HitInfo hit, int damageDone)
		{
			TryInkMark(target, hit, damageDone);
			if (WayfarerSet && !target.immortal)
			{
				WillStacks = System.Math.Min(WillStacks + 1, MaxWill);
				willTimer = WillDuration;
			}
		}

		/// <summary>剑穗：剑类暴击时留下墨痕，一秒后炸开，追加这一下伤害的一半。</summary>
		private void TryInkMark(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!Tassel || !hit.Crit || target.immortal || target.dontTakeDamage)
				return;
			target.GetGlobalNPC<InkMarkNPC>().Mark(damageDone / 2, Player.whoAmI);
		}
	}

	/// <summary>剑类加成：剑穗（伤害 +15%，暴击 +10%）和剑意（每层伤害 +4%、攻速 +3%）。只对剑类。</summary>
	public class SwordBonusItem : GlobalItem
	{
		public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
		{
			if (!Swords.IsSword(item))
				return;
			var sp = player.GetModPlayer<SwordPlayer>();
			if (sp.Tassel)
				damage += 0.15f;
			damage += 0.04f * sp.WillStacks;
		}

		public override float UseSpeedMultiplier(Item item, Player player)
			=> Swords.IsSword(item) ? 1f + 0.03f * player.GetModPlayer<SwordPlayer>().WillStacks : 1f;

		public override void ModifyWeaponCrit(Item item, Player player, ref float crit)
		{
			if (player.GetModPlayer<SwordPlayer>().Tassel && Swords.IsSword(item))
				crit += 10f;
		}
	}

	/// <summary>墨痕：挂在敌人身上，一秒后炸开。只在打出墨痕的那位玩家的客户端上计时和结算。</summary>
	public class InkMarkNPC : GlobalNPC
	{
		private const int Delay = 60;

		public override bool InstancePerEntity => true;

		private int timer;
		private int stored;
		private int owner = -1;

		public void Mark(int damage, int player)
		{
			// 墨痕叠加：再暴击一次就再多存一些，时间不重置，免得永远炸不了
			stored += damage;
			owner = player;
			if (timer <= 0)
				timer = Delay;
		}

		public override void PostAI(NPC npc)
		{
			if (timer <= 0)
				return;
			if (!Main.dedServ && Main.rand.NextBool(3))
			{
				Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(npc.width / 2f, npc.height / 2f), DustID.Smoke, Vector2.Zero, 60, Fx.Ink, 1.2f);
				d.noGravity = true;
			}
			if (--timer > 0)
				return;
			if (owner == Main.myPlayer && npc.active && stored > 0)
				npc.SimpleStrikeNPC(stored, 0, false, 0f, DamageClass.Melee);
			if (!Main.dedServ)
			{
				for (int i = 0; i < 16; i++)
				{
					Dust d = Dust.NewDustPerfect(npc.Center, DustID.Smoke, Main.rand.NextVector2Circular(5f, 5f), 40, Fx.Ink, 2f);
					d.noGravity = true;
				}
			}
			stored = 0;
			owner = -1;
		}
	}
}
