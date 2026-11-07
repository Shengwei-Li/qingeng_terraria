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

		// 法相贴图里的位置（原图像素，见 tools/make_colossus.py）
		private const float BladeX = 954f;      // 竖在脸前的剑身中线
		private const float BladeTop = 0f, BladeBottom = 600f;

		/// <summary>
		/// 法天相地：空白天幕上剑仙本人的法相，剑竖在脸前只露一只眼。
		/// 整幅一层、不透明，像画在这张纸上的——不切条带错动（会把长发撕成锯齿、在脸旁留折痕），也不半透明。
		/// 动静靠这些：极慢的呼吸（缩放）和浮沉；每隔几秒眨一次眼；剑光沿剑身扫下；身后身前几层流云横飘。
		/// 剑仙化光没入时整幅泛起青辉。
		/// </summary>
		private void DrawColossus(SpriteBatch sb, float domain)
		{
			if (domain <= 0.01f)
				return;
			float t = Main.GlobalTimeWrappedHourly;
			float eyeFinal = DomainSystem.ColossusEye;
			bool blinking = eyeFinal < 0.5f && t % 4.7f < 0.16f;
			Texture2D tex = ModContent.Request<Texture2D>(blinking ? "QingyiJianxian/Assets/Sky/ColossusClosed" : "QingyiJianxian/Assets/Sky/Colossus").Value;

			// 呼吸与浮沉：都很慢、很小——巨物的动是沉的
			float breathe = 1f + 0.004f * MathF.Sin(t * 0.9f);
			float bob = MathF.Sin(t * 0.45f) * 6f;
			float scale = Main.screenWidth * 1.02f / tex.Width * breathe;
			// 巨大而遥远：几乎不随镜头动，只有一点点视差
			float drift = MathHelper.Clamp((Main.screenPosition.X + Main.screenWidth / 2f - DomainSystem.Center.X) * -0.02f, -50f, 50f);
			var topLeft = new Vector2((Main.screenWidth - tex.Width * scale) / 2f + drift, -Main.screenHeight * 0.02f + bob);

			float emerge = MathHelper.Clamp((domain - 0.4f) / 0.6f, 0f, 1f);
			float merge = DomainSystem.ColossusMerge;
			float fade = emerge * intensity;
			// 浮现时由淡转实；平时几乎不透明，化光时泛青
			Color tint = Color.Lerp(Color.White, new Color(170, 238, 228), merge * 0.6f) * (0.92f * fade);

			DrawMist(sb, topLeft, tex, scale, fade, t, behind: true);
			sb.Draw(tex, topLeft, null, tint, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

			// 剑光：每 3.3 秒一道寒光从剑尖扫到剑柄
			float sweep = t % 3.3f / 0.7f;
			if (sweep < 1f)
			{
				Vector2 at = topLeft + new Vector2(BladeX, MathHelper.Lerp(BladeTop, BladeBottom, sweep)) * scale;
				float s2 = MathF.Sin(MathF.PI * sweep);
				ScreenLine(sb, at - new Vector2(0f, 50f * scale), at + new Vector2(0f, 50f * scale), Color.White * 0.8f * s2 * fade, 4f * scale);
			}

			DrawMist(sb, topLeft, tex, scale, fade, t, behind: false);
		}

		/// <summary>
		/// 流云：法相原图里裁下来的祥云（Cloud0、Cloud1，同一种画风），在法相身后、身前缓缓横飘，循环。
		/// 身后的大而慢、略淡；身前的从下半身漫过，稍快。每朵大小、朝向、高低、速度各不相同。
		/// </summary>
		private static void DrawMist(SpriteBatch sb, Vector2 topLeft, Texture2D flow, float scale, float fade, float t, bool behind)
		{
			float w = flow.Width * scale, h = flow.Height * scale;
			int count = behind ? 3 : 3;
			for (int i = 0; i < count; i++)
			{
				int k = i + (behind ? 0 : 3);
				Texture2D cloud = ModContent.Request<Texture2D>(k % 2 == 0 ? "QingyiJianxian/Assets/Sky/Cloud0" : "QingyiJianxian/Assets/Sky/Cloud1").Value;
				float size = scale * (behind ? 1.5f + 0.35f * i : 1.1f + 0.3f * i);
				float cw = cloud.Width * size;
				float speed = (behind ? 6f : 11f) * (1f + 0.25f * i) * scale;   // 像素/秒
				float span = w + cw * 2f;
				float x = ((t * speed + k * 0.37f * span) % span) - cw;
				float y = h * (behind ? 0.18f + 0.22f * i : 0.58f + 0.12f * i) + MathF.Sin(t * 0.4f + k) * 8f * scale;
				float a = behind ? 0.8f : 0.92f;
				var flip = k % 3 == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
				sb.Draw(cloud, topLeft + new Vector2(x, y), null, Color.White * (a * fade), 0f, Vector2.Zero, size, flip, 0f);
			}
		}

		private void DrawSunOrMoon(SpriteBatch sb, float night, float domain)
		{
			// 一轮淡淡的日/月，不抢戏：白天暖白，夜里冷白，领域里是淡青的
			Vector2 pos = new(Main.screenWidth * 0.78f, Main.screenHeight * 0.18f);
			Color disc = Color.Lerp(Color.Lerp(new Color(250, 236, 214), new Color(232, 240, 250), night), new Color(178, 228, 222), domain);
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
