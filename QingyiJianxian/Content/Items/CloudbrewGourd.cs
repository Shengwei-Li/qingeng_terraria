using QingyiJianxian.Common;
using QingyiJianxian.Content.NPCs.SwordImmortal;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Content.Items
{
	/// <summary>一壶酒 / Cloudbrew Gourd: pour a cup to heaven and the Sword Immortal comes.</summary>
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

		// 开发测试用：右键总是请来云上饮酒的他，方便反复测试初遇
		public override bool AltFunctionUse(Player player) => true;

		public override bool CanUseItem(Player player)
			=> !NPC.AnyNPCs(ModContent.NPCType<SwordImmortal>()) && !NPC.AnyNPCs(ModContent.NPCType<WanderingImmortal>());

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
			{
				SoundEngine.PlaySound(SoundID.Item3, player.position); // 倒酒
				// 还没和他问过剑：请来的是云上饮酒的他（初遇）；问过剑之后，他直接乘云而来开战
				bool encounter = !WorldFlags.DownedSwordImmortal || player.altFunctionUse == 2;
				int type = encounter ? ModContent.NPCType<WanderingImmortal>() : ModContent.NPCType<SwordImmortal>();
				if (Main.netMode != NetmodeID.MultiplayerClient)
					NPC.SpawnOnPlayer(player.whoAmI, type);
				else
					NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: type);
			}
			return true;
		}

		public override void AddRecipes()
		{
			// 开发测试用：一块土即可合成。正式配方在首次接剑成功后解锁（见设计文档）
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock)
				.Register();
		}
	}
}
