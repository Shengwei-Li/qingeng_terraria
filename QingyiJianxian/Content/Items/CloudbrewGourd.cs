using QingyiJianxian.Common;
using QingyiJianxian.Content.NPCs.SwordImmortal;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>
	/// 一壶酒 / Cloudbrew Gourd: pour a cup to heaven and the Sword Immortal comes.
	/// 第一次问剑后剑仙亲手抛给你；丢了或给队友，可以在酒桶旁用麦芽酒和云纹绡酿（问过剑才解锁）。
	/// </summary>
	public class CloudbrewGourd : ModItem
	{
		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 1;
			ItemID.Sets.SortingPriorityBossSpawns[Type] = 12;
		}

		public override void SetDefaults()
		{
			Item.width = 52;
			Item.height = 60;
			Item.maxStack = 1;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = 30;
			Item.useAnimation = 30;
			Item.consumable = false;
			Item.rare = ItemRarityID.Lime;
			Item.value = Item.buyPrice(gold: 5);
		}

		public override bool CanUseItem(Player player)
			=> !NPC.AnyNPCs(ModContent.NPCType<SwordImmortal>()) && !NPC.AnyNPCs(ModContent.NPCType<WanderingImmortal>());

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
			{
				SoundEngine.PlaySound(SoundID.Item3, player.position); // 倒酒
				// 还没和他问过剑：请来的是云上饮酒的他（初遇）；问过剑之后，他直接乘云而来开战
				int type = WorldFlags.DownedSwordImmortal ? ModContent.NPCType<SwordImmortal>() : ModContent.NPCType<WanderingImmortal>();
				if (Main.netMode != NetmodeID.MultiplayerClient)
					NPC.SpawnOnPlayer(player.whoAmI, type);
				else
					NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: type);
			}
			return true;
		}

		public override void AddRecipes()
		{
			var asked = new Condition(this.GetLocalization("RecipeCondition"), () => WorldFlags.DownedSwordImmortal);
			CreateRecipe()
				.AddIngredient(ItemID.Ale, 5)
				.AddIngredient<CloudweaveSilk>(3)
				.AddTile(TileID.Kegs)
				.AddCondition(asked)
				.Register();
		}
	}
}
