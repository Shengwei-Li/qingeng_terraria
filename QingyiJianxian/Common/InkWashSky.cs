using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Content.NPCs.SwordImmortal;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace QingyiJianxian.Common
{
	/// <summary>
	/// 二阶段 · 剑意：天空褪成宣纸色，远山（峰顶有塔）、近山（山头有松），一行大雁，一轮淡日或淡月。
	/// 夜里换成月下的冷灰色调，不让画面发黑。
	/// </summary>
	public class InkWashSky : CustomSky
	{
		public const string Key = "QingyiJianxian:InkWash";

		private bool active;
		private float intensity;
		private float geeseX = -0.2f;   // 雁阵在屏幕上的横向进度，0..1 之外表示不在画面里

		// 夜里的月下水墨：宣纸偏冷，远山变浅，近山不至于黑成一片
		private static readonly Color DayPaper = Color.White, NightPaper = new(150, 160, 182);
		private static readonly Color DayFar = new(130, 138, 140), NightFar = new(96, 108, 126);
		private static readonly Color DayNear = new(36, 42, 50), NightNear = new(44, 52, 68);
		private static readonly Color Blank = new(242, 238, 227); // 宣纸白 #F2EEE3

		private static float Night => Main.dayTime ? 0f : 1f;

		public override void OnLoad() { }

		public override void Update(GameTime gameTime)
		{
			intensity = MathHelper.Clamp(intensity + (active ? 0.02f : -0.02f), 0f, 1f);
			geeseX += 0.0006f;
			if (geeseX > 1.3f)
				geeseX = -0.3f;
		}

		public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
		{
			if (intensity <= 0f)
				return;
			// 画两遍：最远的一层先铺底；最近的一层再画一遍，盖住原版的远景山和树（夜里它们是黑的）
			bool farthest = maxDepth >= float.MaxValue && minDepth < float.MaxValue;
			bool nearest = maxDepth >= 0f && minDepth < 0f;
			if (!farthest && !nearest)
				return;

			int w = Main.screenWidth, h = Main.screenHeight;
			float night = Night;
			Texture2D paper = ModContent.Request<Texture2D>("QingyiJianxian/Assets/Sky/PaperGradient").Value;
			spriteBatch.Draw(paper, new Rectangle(0, 0, w, h), Color.Lerp(DayPaper, NightPaper, night) * intensity);

			// 三阶段领域：天地空白。整张纸铺成宣纸白，只留几笔极淡的远山和一轮淡青的月
			float domain = DomainSystem.Domain;
			if (domain > 0f)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, w, h), new Rectangle(0, 0, 1, 1), Blank * (domain * intensity));

			DrawSunOrMoon(spriteBatch, night, domain);
			DrawColossus(spriteBatch, domain);
			DrawGeese(spriteBatch, night, 1f - domain);
			DrawMountains(spriteBatch, "MountainsFar", Color.Lerp(DayFar, NightFar, night) * (1f - 0.75f * domain), 0.06f, 0.30f, 1.6f);
			DrawMountains(spriteBatch, "MountainsNear", Color.Lerp(DayNear, NightNear, night) * (1f - domain), 0.12f, 0.46f, 2.2f);
		}

		// 法相贴图里的位置（像素，见 tools/make_colossus.py 的输出）
		private static readonly Vector2 ColossusEye = new(436f, 178f);   // 露出的那只眼
		private const float BladeX = 477f;                                // 竖在脸前的剑身中线
		private const int WindStrip = 3;                                  // 风吹波动的条带高度

		/// <summary>
		/// 法天相地：空白天幕上剑仙本人的法相，剑竖在脸前只露一只眼。
		///   风：外层（长发、发带、衣袖、云）按水平条带随风摆动，越往上摆得越大；中间的脸、剑、手不动；
		///   眨眼：每隔几秒眨一次（换成闭眼的核心层）；
		///   眼光：领域里那只眼一直有淡淡青光在呼吸，最后一剑时大亮；
		///   剑光：每隔几秒一道寒光沿剑身从上往下扫过；
		///   云雾：身前身后几缕流云横飘；整体随呼吸极轻地起伏。
		/// 剑仙化光没入时整幅泛起青辉。
		/// </summary>
		private void DrawColossus(SpriteBatch sb, float domain)
		{
			if (domain <= 0.01f)
				return;
			Texture2D flow = ModContent.Request<Texture2D>("QingyiJianxian/Assets/Sky/Colossus").Value;
			float t = Main.GlobalTimeWrappedHourly;
			float eyeFinal = DomainSystem.ColossusEye;
			bool blinking = eyeFinal < 0.5f && t % 4.7f < 0.16f;
			Texture2D core = ModContent.Request<Texture2D>(blinking ? "QingyiJianxian/Assets/Sky/ColossusCoreClosed" : "QingyiJianxian/Assets/Sky/ColossusCore").Value;

			float breathe = 1f + 0.006f * MathF.Sin(t * 1.2f);
			float scale = Main.screenWidth * 1.02f / flow.Width * breathe;
			// 巨大而遥远：几乎不随镜头动，只有一点点视差
			float drift = MathHelper.Clamp((Main.screenPosition.X + Main.screenWidth / 2f - DomainSystem.Center.X) * -0.02f, -50f, 50f);
			var topLeft = new Vector2((Main.screenWidth - flow.Width * scale) / 2f + drift, -Main.screenHeight * 0.02f);

			float emerge = MathHelper.Clamp((domain - 0.4f) / 0.6f, 0f, 1f);
			float merge = DomainSystem.ColossusMerge;
			float fade = emerge * intensity;
			Color tint = Color.Lerp(Color.White, new Color(150, 235, 225), merge) * (0.55f + 0.25f * merge) * fade;

			DrawMist(sb, topLeft, flow, scale, fade, t, behind: true);

			// 风：条带左右错动。一道主波从上往下走，再叠一道慢的阵风；越往上（发梢、发带）摆得越大
			for (int y = 0; y < flow.Height; y += WindStrip)
			{
				float up = 1f - y / (float)flow.Height;
				float wave = MathF.Sin(t * 2.2f - y * 0.06f) * (3f + 10f * up);
				float gust = MathF.Sin(t * 0.7f + y * 0.02f) * (2f + 4f * up);
				var src = new Rectangle(0, y, flow.Width, Math.Min(WindStrip, flow.Height - y));
				sb.Draw(flow, topLeft + new Vector2((wave + gust) * scale, y * scale), src, tint, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			}
			sb.Draw(core, topLeft, null, tint, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

			// 剑光：每 3.3 秒一道寒光从剑尖扫到剑柄
			float sweep = t % 3.3f / 0.7f;
			if (sweep < 1f)
			{
				Vector2 at = topLeft + new Vector2(BladeX, MathHelper.Lerp(0f, 300f, sweep)) * scale;
				float s = MathF.Sin(MathF.PI * sweep);
				ScreenCircle(sb, Fx.SoftGlow.Value, at, 40f * scale, Color.White * 0.55f * s * fade);
				ScreenLine(sb, at - new Vector2(0f, 26f * scale), at + new Vector2(0f, 26f * scale), Color.White * 0.8f * s * fade, 2.5f * scale);
			}

			// 眼光：领域里一直有淡淡的青光在呼吸；最后一剑时大亮。闭眼那几帧不亮
			float eye = MathHelper.Clamp(0.25f + 0.15f * MathF.Sin(t * 2f) + eyeFinal, 0f, 1.2f) * fade;
			if (!blinking && eye > 0.01f)
			{
				Vector2 at = topLeft + ColossusEye * scale;
				ScreenCircle(sb, Fx.SoftGlow.Value, at, (40f + 50f * eyeFinal) * scale, new Color(120, 230, 220) * 0.55f * eye);
				ScreenCircle(sb, Fx.SoftGlow.Value, at, (8f + 12f * eyeFinal) * scale, Color.White * 0.9f * eye);
			}

			DrawMist(sb, topLeft, flow, scale, fade, t, behind: false);
		}

		/// <summary>几缕流云：拉长的柔光，缓缓横飘、循环。身后一层淡、身前一层更淡。</summary>
		private static void DrawMist(SpriteBatch sb, Vector2 topLeft, Texture2D flow, float scale, float fade, float t, bool behind)
		{
			Texture2D glow = Fx.SoftGlow.Value;
			float w = flow.Width * scale, h = flow.Height * scale;
			int count = behind ? 4 : 3;
			for (int i = 0; i < count; i++)
			{
				float seed = i * 1.37f + (behind ? 0f : 0.5f);
				float speed = (behind ? 0.010f : 0.016f) * (1f + 0.3f * i);
				float x = ((t * speed + seed * 0.31f) % 1f) * (w * 1.4f) - w * 0.2f;
				float y = h * (behind ? 0.55f + 0.1f * i : 0.7f + 0.08f * i) + MathF.Sin(t * 0.5f + seed) * 10f * scale;
				var size = new Vector2(w * (behind ? 0.45f : 0.35f), h * 0.07f);
				Color c = new Color(250, 252, 250) * (behind ? 0.45f : 0.3f) * fade;
				sb.Draw(glow, topLeft + new Vector2(x, y), null, c, 0f, glow.Size() / 2f, size / glow.Size(), SpriteEffects.None, 0f);
			}
		}

		private void DrawSunOrMoon(SpriteBatch sb, float night, float domain)
		{
			// 一轮淡淡的日/月，不抢戏：白天暖白，夜里冷白，领域里是淡青的；外圈一层晕
			Vector2 pos = new(Main.screenWidth * 0.78f, Main.screenHeight * 0.18f);
			Color disc = Color.Lerp(Color.Lerp(new Color(250, 236, 214), new Color(232, 240, 250), night), new Color(178, 228, 222), domain);
			ScreenCircle(sb, Fx.SoftGlow.Value, pos, 260f, disc * 0.35f * intensity);
			ScreenCircle(sb, Fx.Disc.Value, pos, 76f, disc * 0.85f * intensity);
		}

		private void DrawGeese(SpriteBatch sb, float night, float visibility)
		{
			if (geeseX < -0.25f || geeseX > 1.25f || visibility <= 0f)
				return;
			Texture2D goose = ModContent.Request<Texture2D>("QingyiJianxian/Assets/Sky/Goose").Value;
			int frameW = goose.Width / 2;
			Color ink = Color.Lerp(new Color(50, 56, 64), new Color(70, 80, 100), night) * 0.8f * intensity * visibility;
			Vector2 lead = new(Main.screenWidth * geeseX, Main.screenHeight * 0.22f);
			// 人字形雁阵，头雁在前往右飞；每只扇翅的节奏错开
			for (int i = 0; i < 7; i++)
			{
				int rank = (i + 1) / 2;
				int wing = i % 2 == 0 ? 1 : -1;
				Vector2 pos = lead + new Vector2(-rank * 26f, rank * 14f * wing);
				int frame = (int)(Main.GlobalTimeWrappedHourly * 3f + i * 0.7f) % 2;
				sb.Draw(goose, pos, new Rectangle(frame * frameW, 0, frameW, goose.Height), ink, 0f,
					new Vector2(frameW / 2f, goose.Height / 2f), 2f, SpriteEffects.None, 0f);
			}
		}

		/// <param name="parallax">How much the layer follows the camera horizontally.</param>
		/// <param name="top">Layer top as a fraction of screen height.</param>
		private void DrawMountains(SpriteBatch sb, string name, Color tint, float parallax, float top, float scale)
		{
			Texture2D tex = ModContent.Request<Texture2D>("QingyiJianxian/Assets/Sky/" + name).Value;
			float tileW = tex.Width * scale;
			float offset = -(Main.screenPosition.X * parallax % tileW);
			if (offset > 0f)
				offset -= tileW;
			// 上下也跟镜头轻微移动，站高了山就低一点
			float surface = (float)Main.worldSurface * 16f;
			float y = Main.screenHeight * top + MathHelper.Clamp((surface - Main.screenPosition.Y) * parallax * 0.5f, -120f, 120f);
			for (float x = offset; x < Main.screenWidth; x += tileW)
				sb.Draw(tex, new Vector2(x, y), null, tint * intensity, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		// 天空是按屏幕坐标画的；Fx 里的工具按世界坐标算（会减掉镜头位置），这里不能用
		private static void ScreenCircle(SpriteBatch sb, Texture2D tex, Vector2 center, float diameter, Color color)
			=> sb.Draw(tex, center, null, color, 0f, tex.Size() / 2f, diameter / tex.Width, SpriteEffects.None, 0f);

		private static void ScreenLine(SpriteBatch sb, Vector2 from, Vector2 to, Color color, float thickness)
		{
			Vector2 d = to - from;
			sb.Draw(TextureAssets.MagicPixel.Value, from, new Rectangle(0, 0, 1, 1), color, d.ToRotation(), new Vector2(0f, 0.5f), new Vector2(d.Length(), thickness), SpriteEffects.None, 0f);
		}

		public override Color OnTileColor(Color inColor)
		{
			// 白天四周略压暗；夜里反而提亮成月光，保证打得清楚
			Color day = Color.Lerp(inColor, inColor * 0.85f, intensity);
			Color night = Color.Lerp(inColor, new Color(150, 162, 186), 0.55f * intensity);
			// 领域里天地一片白，四下通明
			return Color.Lerp(Main.dayTime ? day : night, Color.White, 0.8f * DomainSystem.Domain);
		}

		public override float GetCloudAlpha() => 1f - intensity;

		public override bool IsActive() => active || intensity > 0f;

		public override void Reset() => active = false;

		public override void Activate(Vector2 position, params object[] args) => active = true;

		public override void Deactivate(params object[] args) => active = false;
	}

	/// <summary>Turns the ink-wash sky on while the boss is in phase two or later.</summary>
	public class InkWashScene : ModSceneEffect
	{
		public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

		public override bool IsSceneEffectActive(Player player) => SwordImmortal.InkWashActive();

		public override void SpecialVisuals(Player player, bool isActive)
		{
			CustomSky sky = SkyManager.Instance[InkWashSky.Key];
			if (sky == null)
				return;
			if (isActive && !sky.IsActive())
				SkyManager.Instance.Activate(InkWashSky.Key);
			else if (!isActive && sky.IsActive())
				SkyManager.Instance.Deactivate(InkWashSky.Key);
		}
	}
}
