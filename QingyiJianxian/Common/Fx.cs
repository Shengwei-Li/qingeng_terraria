using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace QingyiJianxian.Common
{
	/// <summary>Shared drawing helpers and effect textures.</summary>
	public static class Fx
	{
		// 朱砂红：全场唯一的高饱和预警色
		public static readonly Color Cinnabar = new(214, 64, 48);
		public static readonly Color Ink = new(18, 22, 30);
		public static readonly Color Teal = new(74, 167, 166);
		public static readonly Color PaleTeal = new(170, 236, 228);

		public static Asset<Texture2D> SoftGlow => ModContent.Request<Texture2D>("QingyiJianxian/Assets/Effects/SoftGlow");
		public static Asset<Texture2D> SoftGlowLarge => ModContent.Request<Texture2D>("QingyiJianxian/Assets/Effects/SoftGlowLarge");
		public static Asset<Texture2D> Disc => ModContent.Request<Texture2D>("QingyiJianxian/Assets/Effects/Disc");
		public static Asset<Texture2D> Ring => ModContent.Request<Texture2D>("QingyiJianxian/Assets/Effects/Ring");

		/// <summary>Draws a straight line in world coordinates.</summary>
		public static void Line(SpriteBatch sb, Vector2 from, Vector2 to, Color color, float thickness)
		{
			Vector2 d = to - from;
			sb.Draw(TextureAssets.MagicPixel.Value, from - Main.screenPosition, new Rectangle(0, 0, 1, 1), color,
				d.ToRotation(), new Vector2(0f, 0.5f), new Vector2(d.Length(), thickness), SpriteEffects.None, 0f);
		}

		/// <summary>Draws a centered texture scaled to the given world-space diameter.</summary>
		public static void Circle(SpriteBatch sb, Texture2D tex, Vector2 center, float diameter, Color color)
		{
			sb.Draw(tex, center - Main.screenPosition, null, color, 0f, tex.Size() / 2f, diameter / tex.Width, SpriteEffects.None, 0f);
		}
	}
}
