using QingyiJianxian.Common;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace QingyiJianxian
{
	public class QingyiJianxian : Mod
	{
		public override void Load()
		{
			if (!Main.dedServ)
				SkyManager.Instance[InkWashSky.Key] = new InkWashSky();
		}
	}
}
