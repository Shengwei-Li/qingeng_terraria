using System.IO;
using QingyiJianxian.Common;
using QingyiJianxian.Content.NPCs.SwordImmortal;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian
{
	public class QingyiJianxian : Mod
	{
		/// <summary>联机消息类型</summary>
		public enum Message : byte
		{
			/// <summary>客户端 → 服务器：某位玩家用清月剑打中了剑仙，剑被夺，剑仙进入双剑形态。</summary>
			Reclaim,
		}

		public override void Load()
		{
			if (!Main.dedServ)
				SkyManager.Instance[InkWashSky.Key] = new InkWashSky();
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			var type = (Message)reader.ReadByte();
			switch (type)
			{
				case Message.Reclaim:
					int npc = reader.ReadByte();
					if (Main.netMode == NetmodeID.Server && Main.npc[npc].active && Main.npc[npc].ModNPC is SwordImmortal boss)
						boss.BecomeTwin();
					break;
			}
		}
	}
}
