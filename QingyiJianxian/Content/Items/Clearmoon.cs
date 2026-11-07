using Microsoft.Xna.Framework;
using QingyiJianxian.Common;
using QingyiJianxian.Content.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>
	/// 清月剑 / Clearmoon. The Sword Immortal's parting gift.
	/// 左键三段：横扫、回撩、前刺。前两剑甩出一线剑气，前刺刺出一道长剑气，刺中的敌人片刻后再绽开一道斩痕。
	/// 右键御剑：剑脱手飞到鼠标处连斩三剑再飞回来。剑不在手里时左右键都用不了。停手一秒，连招从头开始。
	/// </summary>
	public class Clearmoon : ModItem
	{
		public const int FlightCooldown = 150;   // 剑回手后 2.5 秒才能再御剑
		public const float FlightRange = 520f;   // 御剑最远飞多远

		public override void SetStaticDefaults()
		{
			Swords.Extra.Add(Type); // 手持弹幕出招，靠挥砍式判定认不出，单独登记为剑
		}

		public override void SetDefaults()
		{
			Item.width = 56;
			Item.height = 56;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 16;
			Item.useAnimation = 16;
			Item.autoReuse = true;
			Item.noMelee = true;        // 伤害由挥砍弹幕结算
			Item.noUseGraphic = true;   // 剑由弹幕来画
			Item.DamageType = DamageClass.Melee;
			Item.damage = 72;
			Item.crit = 10;
			Item.knockBack = 5f;
			Item.shoot = ModContent.ProjectileType<ClearmoonSwing>();
			Item.shootSpeed = 1f;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(gold: 6);
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool CanUseItem(Player player)
		{
			// 剑被夺：这一战用不了
			if (player.HasBuff<Buffs.Disarmed>())
				return false;
			// 剑飞出去了，手里是空的
			if (player.ownedProjectileCounts[ModContent.ProjectileType<ClearmoonFlight>()] > 0)
				return false;
			if (player.altFunctionUse == 2)
				return player.GetModPlayer<SwordPlayer>().FlightCooldown <= 0;
			return true;
		}

		// 前刺蓄势更长，收势也慢一点
		public override float UseSpeedMultiplier(Player player)
		{
			if (player.altFunctionUse == 2)
				return 1f;
			return player.GetModPlayer<SwordPlayer>().ComboStep == 2 ? 0.75f : 1f;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			var sp = player.GetModPlayer<SwordPlayer>();
			if (player.altFunctionUse == 2)
			{
				Vector2 offset = DevTools.MouseWorld - player.MountedCenter;
				if (offset.Length() > FlightRange)
					offset = offset.SafeNormalize(Vector2.UnitX) * FlightRange;
				Vector2 point = player.MountedCenter + offset;
				Projectile.NewProjectile(source, player.MountedCenter, offset.SafeNormalize(Vector2.UnitX) * 6f,
					ModContent.ProjectileType<ClearmoonFlight>(), (int)(damage * 0.75f), knockback, player.whoAmI, point.X, point.Y);
				return false;
			}
			Vector2 aim = (DevTools.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
			int dir = aim.X >= 0 ? 1 : -1;
			Projectile.NewProjectile(source, player.MountedCenter, aim, type, damage, knockback, player.whoAmI, sp.ComboStep, dir);
			sp.ComboStep = (sp.ComboStep + 1) % 3;
			sp.ComboTimer = 60;
			return false;
		}
	}
}
