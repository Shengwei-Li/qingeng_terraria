using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Common;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>
	/// 御剑 / Swordborne：翅膀栏饰品，脚踏飞剑。剑仙打完后有概率掉落。
	/// 比猪鲨翅膀略弱：飞行 2.5 秒（猪鲨 3 秒），水平速度 7.5（猪鲨 8）。像星盘一样按住下 + 跳可以悬停，悬停时飞行时间耗得快一倍。
	/// 没有翅膀图：平时剑斜背在背后，起飞时剑从背后飞到脚下，人站在剑上飞，落地后剑回到背上（见 SwordborneLayer）。
	/// </summary>
	[AutoloadEquip(EquipType.Wings)]
	public class Swordborne : ModItem
	{
		public const int FlyTime = 150;

		public override void SetStaticDefaults()
		{
			ArmorIDs.Wing.Sets.Stats[Item.wingSlot] = new WingStats(FlyTime, 7.5f, 1.8f, true, 7.5f, 1.8f);
		}

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(gold: 8);
		}

		public override void VerticalWingSpeeds(Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
			ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend)
		{
			ascentWhenFalling = 0.85f;
			ascentWhenRising = 0.15f;
			maxCanAscendMultiplier = 1f;
			maxAscentMultiplier = 2.6f; // 猪鲨翅膀约 3
			constantAscend = 0.135f;
		}

		// 不扇翅膀：没有翅膀帧动画，也没有扇翅声
		public override bool WingUpdate(Player player, bool inUse) => true;
	}

	public class SwordbornePlayer : ModPlayer
	{
		/// <summary>0 = 剑在背上，1 = 剑在脚下；中间是剑从背后飞到脚下（或飞回）的过程。</summary>
		public float Ride;

		private bool riding;

		public bool Equipped => Player.wings == EquipLoader.GetEquipSlot(Mod, nameof(Swordborne), EquipType.Wings);

		private bool Airborne => Player.velocity.Y != 0f && Player.grappling[0] < 0 && !Player.mount.Active;

		public override void PostUpdate()
		{
			bool ownWings = Player.wingsLogic == EquipLoader.GetEquipSlot(Mod, nameof(Swordborne), EquipType.Wings);
			if (!Airborne || !ownWings)
				riding = false;
			else if (Player.controlJump && Player.wingTime > 0 && Player.jump == 0)
				riding = true; // 跳到顶、开始用飞行时间的那一刻踏上剑

			// 悬停（按住下 + 跳）：飞行时间耗得快一倍
			if (riding && Player.controlDown && Player.controlJump && Player.wingTime > 0)
				Player.wingTime = Math.Max(0, Player.wingTime - 1);

			Ride = MathHelper.Clamp(Ride + (riding ? 0.125f : -0.125f), 0f, 1f); // 8 帧飞出 / 飞回

			// 剑尾洒下的光屑
			if (riding && Ride >= 1f && !Main.dedServ && Player.velocity.LengthSquared() > 4f && Main.rand.NextBool(2))
			{
				Vector2 tail = Player.Bottom + new Vector2(-Player.direction * 26f, 2f);
				Fx.Mote(tail, -Player.velocity * 0.15f + Main.rand.NextVector2Circular(0.6f, 0.6f), 0.7f);
			}
		}
	}

	/// <summary>画剑：在背上（身体后面）或脚下（脚前面），起飞 / 落地时在两处之间插值。</summary>
	public abstract class SwordborneLayerBase : PlayerDrawLayer
	{
		private static Asset<Texture2D> sword;

		protected abstract bool Front { get; }

		public override void Load()
		{
			if (!Main.dedServ)
				sword = ModContent.Request<Texture2D>("QingyiJianxian/Content/Items/Clearmoon");
		}

		public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
		{
			Player p = drawInfo.drawPlayer;
			if (p.dead || p.invis || drawInfo.shadow != 0f || p.mount.Active)
				return false;
			var sp = p.GetModPlayer<SwordbornePlayer>();
			return sp.Equipped && (sp.Ride >= 0.5f) == Front;
		}

		protected override void Draw(ref PlayerDrawSet drawInfo)
		{
			Player p = drawInfo.drawPlayer;
			float t = p.GetModPlayer<SwordbornePlayer>().Ride;
			float e = t * t * (3f - 2f * t);
			int dir = p.direction;

			// 面朝右时的两种姿态（面朝左时镜像）：贴图里剑尖朝右上（-45°）
			// 背上：剑柄在右肩后，剑尖斜向左下；脚下：剑身水平，剑尖朝前
			Vector2 center = drawInfo.Position - Main.screenPosition + new Vector2(p.width / 2f, p.height / 2f);
			Vector2 backPos = center + new Vector2(-3f, -4f);
			Vector2 footPos = center + new Vector2(0f, p.height / 2f + 3f);
			float backRot = MathHelper.ToRadians(118f) + MathHelper.PiOver4;
			float footRot = MathHelper.PiOver4;
			float backScale = 0.62f, footScale = 0.85f;

			// 中途抬高一点，像剑从背后翻出来落到脚下
			Vector2 pos = Vector2.Lerp(backPos, footPos, e) + new Vector2(0f, -14f * MathF.Sin(e * MathHelper.Pi));
			float rot = MathHelper.Lerp(backRot, footRot + MathHelper.TwoPi, e);
			float scale = MathHelper.Lerp(backScale, footScale, e);
			if (t >= 1f)
				pos.Y += MathF.Sin(Main.GameUpdateCount * 0.1f) * 1.2f; // 踏剑时轻轻浮动

			pos.X = center.X + (pos.X - center.X) * dir;
			SpriteEffects fx = SpriteEffects.None;
			if (dir < 0)
			{
				rot = -rot;
				fx = SpriteEffects.FlipHorizontally;
			}

			Texture2D tex = sword.Value;
			Color light = Lighting.GetColor((int)((pos.X + Main.screenPosition.X) / 16f), (int)((pos.Y + Main.screenPosition.Y) / 16f));
			var data = new DrawData(tex, pos.Floor(), null, light, rot, tex.Size() / 2f, scale, fx, 0) { shader = drawInfo.cWings };
			drawInfo.DrawDataCache.Add(data);

			// 踏剑时剑身一层淡淡的青光
			if (t > 0.5f)
			{
				var glow = new DrawData(tex, pos.Floor(), null, Fx.PaleTeal * 0.35f * (t - 0.5f) * 2f, rot, tex.Size() / 2f, scale * 1.06f, fx, 0) { shader = drawInfo.cWings };
				drawInfo.DrawDataCache.Add(glow);
			}
		}
	}

	public class SwordborneBackLayer : SwordborneLayerBase
	{
		protected override bool Front => false;

		public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.Torso);
	}

	public class SwordborneFootLayer : SwordborneLayerBase
	{
		protected override bool Front => true;

		public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Shoes);
	}
}
