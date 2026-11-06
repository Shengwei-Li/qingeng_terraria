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
		public static readonly Color Ink = new(18, 22, 30);
		public static readonly Color Teal = new(74, 167, 166);
		public static readonly Color PaleTeal = new(170, 236, 228);
		// 白玉：偏暖的白，比纯白温润。剑气的芯、剑河的浪尖
		public static readonly Color Jade = new(236, 250, 238);

		/// <summary>这一点是不是在实心方块里（平台不算）。剑气、剑河用它贴地、撞墙。</summary>
		public static bool Solid(Vector2 world)
		{
			int i = (int)(world.X / 16f), j = (int)(world.Y / 16f);
			return WorldGen.InWorld(i, j, 1) && WorldGen.SolidTile(i, j);
		}

		/// <summary>白玉色的光屑：小、亮、慢慢往上飘散。</summary>
		public static void Mote(Vector2 at, Vector2 velocity, float scale = 0.8f)
		{
			if (Main.dedServ)
				return;
			Dust d = Dust.NewDustPerfect(at, Terraria.ID.DustID.TintableDustLighted, velocity, 0, Jade, scale);
			d.noGravity = true;
			d.fadeIn = 0.4f;
		}

		/// <summary>水雾：贴地翻起、撞墙溅开时用。</summary>
		public static void Mist(Vector2 at, Vector2 velocity, float scale = 1.2f)
		{
			if (Main.dedServ)
				return;
			Dust d = Dust.NewDustPerfect(at, Terraria.ID.DustID.Cloud, velocity, 170, new Color(220, 240, 236), scale);
			d.noGravity = true;
		}

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

		/// <summary>
		/// 剑意：出剑之前，剑锋所指的方向上先洇开一笔干笔淡墨。
		/// 不是一条指路的线——它从剑尖出发，越远越细越淡，中途断续飞白，末端化进空气里，
		/// 读得出"剑要往那边去"，读不出"终点在哪、边界在哪"。墨里一缕淡青寒光，保证在暗处也看得见。
		/// </summary>
		/// <param name="strength">0..1，整体浓淡（随蓄势渐浓）</param>
		/// <param name="seed">同一招保持不变，飞白的断口才不会闪</param>
		public static void SwordIntent(SpriteBatch sb, Vector2 from, Vector2 dir, float length, float width, float strength, int seed)
		{
			const float Seg = 16f;
			int n = (int)(length / Seg);
			if (n <= 0 || strength <= 0f)
				return;
			Vector2 normal = new(-dir.Y, dir.X);
			for (int i = 0; i < n; i++)
			{
				float p = i / (float)n;
				// 飞白：越往笔尾断得越多
				if (Hash(seed, i) < 0.15f + 0.45f * p)
					continue;
				float fade = (1f - p) * (1f - p);
				float w = width * (0.3f + 0.7f * (1f - p));
				Vector2 a = from + dir * (i * Seg);
				Vector2 b = a + dir * (Seg * (0.55f + 0.45f * Hash(seed, i + 101)));
				Vector2 wobble = normal * ((Hash(seed, i + 57) - 0.5f) * width * 0.5f);
				Line(sb, a + wobble, b + wobble, Ink * (0.5f * strength * fade), w);
				Line(sb, a, b, PaleTeal * (0.4f * strength * fade), MathHelper.Max(1f, w * 0.22f));
			}
		}

		/// <summary>剑意锁定：一道寒光顺着剑意极快地划过去，划完就没了。t 为 0..1。</summary>
		public static void SwordIntentFlash(SpriteBatch sb, Vector2 from, Vector2 dir, float length, float t)
		{
			if (t < 0f || t > 1f)
				return;
			float head = length * t;
			float tail = MathHelper.Max(0f, head - 160f);
			float a = 1f - t * 0.7f;
			Line(sb, from + dir * tail, from + dir * head, PaleTeal * (0.5f * a), 4f);
			Line(sb, from + dir * tail, from + dir * head, Color.White * (0.85f * a), 1.5f);
		}

		private static float Hash(int seed, int i)
		{
			uint x = (uint)(seed * 73856093) ^ (uint)(i * 19349663);
			x ^= x >> 13;
			x *= 0x5bd1e995;
			x ^= x >> 15;
			return (x & 0xFFFF) / 65535f;
		}

		/// <summary>Draws a centered texture scaled to the given world-space diameter.</summary>
		public static void Circle(SpriteBatch sb, Texture2D tex, Vector2 center, float diameter, Color color)
		{
			sb.Draw(tex, center - Main.screenPosition, null, color, 0f, tex.Size() / 2f, diameter / tex.Width, SpriteEffects.None, 0f);
		}
	}
}
