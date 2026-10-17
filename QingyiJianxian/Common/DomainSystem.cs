using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace QingyiJianxian.Common
{
	/// <summary>
	/// 三阶段 · 领域的展开程度（天空据此褪成空白）、领域中心，以及结局时的电影黑边。
	/// The boss writes the targets every tick; if it disappears, everything fades back on its own.
	/// </summary>
	public class DomainSystem : ModSystem
	{
		public const float Radius = 1400f;     // 领域边界半径

		public static Vector2 Center;
		public static float Domain;            // 0..1，领域展开程度
		public static float Letterbox;         // 0..1，黑边
		public static float TargetDomain, TargetLetterbox;

		// 法天相地：天幕上那尊法相。Eye = 睁眼程度，Merge = 剑仙化光没入法相（法相发亮）
		public static float ColossusEye, ColossusMerge;
		public static float TargetColossusEye, TargetColossusMerge;

		// 一剑开天没接住：天地沿这一剑被劈开一瞬
		public static Vector2 CutPoint, CutDir;
		public static float Cut;

		public static void Cleave(Vector2 at, Vector2 dir)
		{
			CutPoint = at;
			CutDir = dir.SafeNormalize(Vector2.UnitY);
			Cut = 1f;
		}

		public override void PostUpdateEverything()
		{
			Domain = Approach(Domain, TargetDomain, 1f / 70f);
			Letterbox = Approach(Letterbox, TargetLetterbox, 1f / 30f);
			ColossusEye = Approach(ColossusEye, TargetColossusEye, 1f / 40f);
			ColossusMerge = Approach(ColossusMerge, TargetColossusMerge, 1f / 50f);
			TargetDomain = TargetLetterbox = TargetColossusEye = TargetColossusMerge = 0f;
			Cut = MathF.Max(0f, Cut - 1f / 45f);
		}

		public override void OnWorldUnload()
		{
			Domain = Letterbox = TargetDomain = TargetLetterbox = 0f;
			ColossusEye = ColossusMerge = TargetColossusEye = TargetColossusMerge = 0f;
			Cut = 0f;
		}

		private static float Approach(float v, float t, float step) => v < t ? MathF.Min(v + step, t) : MathF.Max(v - step, t);

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			// 劈开：一道贯穿全屏的白光，两侧裂口的墨边向外错开，然后合拢
			layers.Insert(0, new LegacyGameInterfaceLayer("QingyiJianxian: Cleave", () =>
			{
				if (Cut > 0f)
				{
					SpriteBatch sb = Main.spriteBatch;
					Vector2 a = CutPoint - CutDir * 3000f, b = CutPoint + CutDir * 3000f;
					Vector2 n = new(-CutDir.Y, CutDir.X);
					float open = MathF.Sin(MathF.PI * MathF.Min(1f, (1f - Cut) * 2.2f)) * 26f; // 先裂开再合上
					Fx.Line(sb, a + n * open, b + n * open, Fx.Ink * Cut, 6f);
					Fx.Line(sb, a - n * open, b - n * open, Fx.Ink * Cut, 6f);
					Fx.Line(sb, a, b, new Color(120, 230, 220) * Cut * 0.6f, open * 2f + 10f);
					Fx.Line(sb, a, b, Color.White * Cut, MathF.Max(open * 1.2f, 4f));
				}
				return true;
			}, InterfaceScaleType.Game));

			layers.Insert(0, new LegacyGameInterfaceLayer("QingyiJianxian: Letterbox", () =>
			{
				if (Letterbox > 0f)
				{
					int bar = (int)(Main.screenHeight * 0.11f * Letterbox);
					Texture2D px = TextureAssets.MagicPixel.Value;
					Main.spriteBatch.Draw(px, new Rectangle(0, 0, Main.screenWidth, bar), new Rectangle(0, 0, 1, 1), Color.Black);
					Main.spriteBatch.Draw(px, new Rectangle(0, Main.screenHeight - bar, Main.screenWidth, bar), new Rectangle(0, 0, 1, 1), Color.Black);
				}
				return true;
			}, InterfaceScaleType.None));
		}
	}

	/// <summary>World flags: whether the Sword Immortal has been met with blade and let go.</summary>
	public class WorldFlags : ModSystem
	{
		public static bool DownedSwordImmortal;

		/// <summary>今天已经遇到过他了：自然出现每天最多一次。天亮时清零。</summary>
		public static bool EncounteredToday;
		private static bool wasDay;

		public override void ClearWorld()
		{
			DownedSwordImmortal = false;
			EncounteredToday = false;
			wasDay = Main.dayTime;
		}

		public override void PostUpdateWorld()
		{
			if (Main.dayTime && !wasDay)
				EncounteredToday = false;
			wasDay = Main.dayTime;
		}

		public override void SaveWorldData(TagCompound tag)
		{
			if (DownedSwordImmortal)
				tag["downedSwordImmortal"] = true;
		}

		public override void LoadWorldData(TagCompound tag) => DownedSwordImmortal = tag.ContainsKey("downedSwordImmortal");

		public override void NetSend(BinaryWriter writer) => writer.Write(DownedSwordImmortal);

		public override void NetReceive(BinaryReader reader) => DownedSwordImmortal = reader.ReadBoolean();
	}
}
