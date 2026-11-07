using Microsoft.Xna.Framework;
using QingyiJianxian.Content.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>
	/// 清流剑符 / Talisman of the Lingering Stream：剑仙留在符纸上的一道剑意，用一张少一张。
	/// 从身边起势，朝点下的方向（任意角度）碾出一道白玉色的剑河。
	/// 范围有限：宽 7 格、长 50 格，贴地流、撞墙停；每个敌人只斩一次，最多斩 12 个，对 Boss 打七折。见 TalismanRiver。
	/// 不分职业——打完剑仙，法师、射手、召唤师也有东西拿。
	/// </summary>
	public class LingeringStreamTalisman : ModItem
	{

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 25;
		}

		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 30;
			Item.maxStack = Item.CommonMaxStack;
			Item.consumable = true;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = 60;          // 一秒一张，不能连甩
			Item.useAnimation = 20;
			Item.reuseDelay = 40;
			Item.autoReuse = false;
			Item.noMelee = true;
			Item.DamageType = DamageClass.Generic;
			Item.damage = 420; // 剑仙留下的保命剑气：次数有限，一张就要能清场
			Item.knockBack = 9f;
			Item.shoot = ModContent.ProjectileType<TalismanRiver>();
			Item.shootSpeed = 1f;
			Item.UseSound = SoundID.Item8 with { Pitch = 0.3f };
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(silver: 40);
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			// 朝点下的方向（任意角度）碾出去
			Vector2 aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
			int dir = aim.X >= 0f ? 1 : -1;
			Projectile.NewProjectile(source, player.Center + aim * 12f, Vector2.Zero, type, damage, knockback, player.whoAmI, aim.ToRotation());
			player.ChangeDir(dir);
			// 保命：放出的一瞬无敌 1 秒（6 秒内只一次），身周泛起一圈白玉光
			var sp = player.GetModPlayer<Common.SwordPlayer>();
			if (sp.TalismanGuardCooldown <= 0)
			{
				sp.TalismanGuardCooldown = 60 * 6;
				player.immune = true;
				player.immuneNoBlink = false;
				player.immuneTime = System.Math.Max(player.immuneTime, 60);
				for (int i = 0; i < player.hurtCooldowns.Length; i++)
					player.hurtCooldowns[i] = System.Math.Max(player.hurtCooldowns[i], 60);
				for (int i = 0; i < 16; i++)
					Common.Fx.Mote(player.Center + (MathHelper.TwoPi * i / 16f).ToRotationVector2() * 26f, (MathHelper.TwoPi * i / 16f).ToRotationVector2() * 1.5f, 0.9f);
			}
			// 符纸化灰
			for (int i = 0; i < 10; i++)
			{
				Dust d = Dust.NewDustPerfect(player.Top + new Vector2(player.direction * 6f, -8f), DustID.Ash, Main.rand.NextVector2Circular(1.5f, 1.5f) - Vector2.UnitY, 100, default, 1f);
				d.noGravity = true;
			}
			return false;
		}
	}
}
