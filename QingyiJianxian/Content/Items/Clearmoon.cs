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
	/// 三段连斩：上挑、下劈、旋身一斩；每一剑甩出月牙剑气，旋斩那道最大。停手一秒，连招从头开始。
	/// </summary>
	public class Clearmoon : ModItem
	{
		public override void SetStaticDefaults()
		{
			Swords.Extra.Add(Type); // 手持弹幕出招，靠挥砍式判定认不出，单独登记为剑
		}

		public override void SetDefaults()
		{
			Item.width = 56;
			Item.height = 56;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 15;
			Item.useAnimation = 15;
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

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			var sp = player.GetModPlayer<SwordPlayer>();
			Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
			int dir = aim.X >= 0 ? 1 : -1;
			Projectile.NewProjectile(source, player.MountedCenter, aim, type, damage, knockback, player.whoAmI, sp.ComboStep, dir);
			sp.ComboStep = (sp.ComboStep + 1) % 3;
			sp.ComboTimer = 60;
			return false;
		}
	}
}
