using Microsoft.Xna.Framework;
using QingyiJianxian.Content.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>
	/// 清流剑符 / Talisman of the Lingering Stream：剑仙留在符纸上的一道剑气，用一张少一张。
	/// 点哪里，天上落下一道剑气斩过那一点：先闪一线寒光，再整条斩下，斩到这一笔上的敌人。
	/// 这一笔有长度（约 24 格），不会延伸过整个屏幕；一笔最多斩 8 个敌人；对 Boss 打七折。
	/// 不分职业——打完剑仙，法师、射手、召唤师也有东西拿。
	/// </summary>
	public class LingeringStreamTalisman : ModItem
	{
		public const float Range = 900f;   // 离自己最远多远

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
			Item.damage = 250;
			Item.knockBack = 6f;
			Item.shoot = ModContent.ProjectileType<TalismanStroke>();
			Item.shootSpeed = 1f;
			Item.UseSound = SoundID.Item8 with { Pitch = 0.3f };
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(silver: 40);
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			Vector2 offset = Main.MouseWorld - player.Center;
			if (offset.Length() > Range)
				offset = offset.SafeNormalize(Vector2.UnitX) * Range;
			// 斜着从天上斩下来：与水平成 55°–70°，左斜右斜随机
			float tilt = MathHelper.ToRadians(Main.rand.NextFloat(55f, 70f)) * (Main.rand.NextBool() ? 1f : -1f);
			Projectile.NewProjectile(source, player.Center + offset, Vector2.Zero, type, damage, knockback, player.whoAmI, tilt);
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
