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
	/// 从身边起势，朝鼠标那一侧水平碾出一道白玉色的剑河。鼠标的高度决定河道走哪一层（离自己上下最多 10 格）。
	/// 范围有限：高 7 格、长 50 格，贴地流、撞墙停；每个敌人只斩一次，最多斩 12 个，对 Boss 打七折。见 TalismanRiver。
	/// 不分职业——打完剑仙，法师、射手、召唤师也有东西拿。
	/// </summary>
	public class LingeringStreamTalisman : ModItem
	{
		public const float LaneRange = 160f;   // 河道中线离自己上下最多多远（10 格）

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
			Item.damage = 260;
			Item.knockBack = 9f;
			Item.shoot = ModContent.ProjectileType<TalismanRiver>();
			Item.shootSpeed = 1f;
			Item.UseSound = SoundID.Item8 with { Pitch = 0.3f };
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(silver: 40);
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			int dir = Main.MouseWorld.X >= player.Center.X ? 1 : -1;
			float lane = MathHelper.Clamp(Main.MouseWorld.Y, player.Center.Y - LaneRange, player.Center.Y + LaneRange);
			Projectile.NewProjectile(source, new Vector2(player.Center.X + dir * 12f, lane), Vector2.Zero, type, damage, knockback, player.whoAmI, dir, lane);
			player.ChangeDir(dir);
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
