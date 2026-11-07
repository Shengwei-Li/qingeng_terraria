using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QingyiJianxian.Common
{
	/// <summary>
	/// 开发用：把游戏画面一帧帧存成 PNG，供离线检查特效。
	/// 游戏内 Build + Reload 编出来的不带 DEBUG，所以没法用 #if DEBUG 隔开——发布到创意工坊前删掉这个文件。
	/// 截的是最终画面（含光照、背景、地形），整个画面（最大 1920×1080），每 2 帧存一张。
	/// 存到 tModLoader 存档目录下的 QingyiCaptures/&lt;名字&gt;/。
	/// </summary>
	public class DevCapture : ModSystem
	{
		private static string name;
		private static int framesLeft;
		private static int every;
		private static int tick;
		private static int index;
		private static int delay;

		/// <summary>/qyfx domain：不打 Boss，直接把三阶段的空白天地和法相展开这么多帧。</summary>
		public static int DomainPreview;
		private static readonly List<(Color[] data, int w, int h, int i)> pending = new();

		public static string Folder => Path.Combine(Main.SavePath, "QingyiCaptures");

		public static void Start(string captureName, int frames, int stride = 2, int wait = 0)
		{
			delay = wait;
			name = captureName;
			framesLeft = frames;
			every = stride;
			tick = 0;
			index = 0;
			string dir = Path.Combine(Folder, name);
			if (Directory.Exists(dir))
				foreach (string f in Directory.GetFiles(dir, "*.png"))
					File.Delete(f);
			Directory.CreateDirectory(dir);
		}

		public override void Load()
		{
			if (!Main.dedServ)
				Main.OnPostDraw += Capture;
		}

		public override void Unload()
		{
			Main.OnPostDraw -= Capture;
		}

		// 在 NPC 更新之后、DomainSystem 收尾之前写目标值（和 Boss 写的时机一样）
		public override void PostUpdateNPCs()
		{
			if (DomainPreview <= 0)
				return;
			DomainPreview--;
			DomainSystem.Center = Main.LocalPlayer.Center;
			DomainSystem.TargetDomain = 1f;
		}

		private static void Capture(GameTime _)
		{
			if (framesLeft <= 0 || name == null)
				return;
			if (delay > 0)
			{
				delay--;
				return;
			}
			if (tick++ % every != 0)
				return;
			GraphicsDevice gd = Main.graphics.GraphicsDevice;
			int bw = gd.PresentationParameters.BackBufferWidth, bh = gd.PresentationParameters.BackBufferHeight;
			int w = Math.Min(1920, bw), h = Math.Min(1080, bh); // 整屏：剑河 800 像素的尽头也要录进来
			var rect = new Rectangle((bw - w) / 2, (bh - h) / 2, w, h);
			var data = new Color[w * h];
			gd.GetBackBufferData(rect, data, 0, data.Length);
			pending.Add((data, w, h, index++));
			if (--framesLeft == 0)
				Flush();
		}

		private static void Flush()
		{
			string dir = Path.Combine(Folder, name);
			foreach (var (data, w, h, i) in pending)
			{
				using var tex = new Texture2D(Main.graphics.GraphicsDevice, w, h);
				tex.SetData(data);
				using FileStream fs = File.Create(Path.Combine(dir, $"f{i:000}.png"));
				tex.SaveAsPng(fs, w, h);
			}
			pending.Clear();
			Main.NewText($"[青衣剑仙] 已录下 {name}：{dir}", Color.LightSeaGreen);
			name = null;
		}
	}

	/// <summary>
	/// 开发用的特效演示：/qyfx qi | lance | swing | river | all。
	/// 在身前摆三只打不死的史莱姆当靶子，按顺序放出特效，同时自动录帧（见 DevCapture）。
	/// </summary>
	public class QyfxCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "qyfx";
		public override string Usage => "/qyfx qi|lance|swing|river|riverup|domain";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			Player p = caller.Player;
			string what = args.Length > 0 ? args[0] : "all";
			int dir = p.direction;
			var src = p.GetSource_Misc("qyfx");
			// 伤害照物品的真实面板来（剑气按挥剑时的比例），不是随便写个数
			int sword = p.GetWeaponDamage(ContentSamples.ItemsByType[ModContent.ItemType<Content.Items.Clearmoon>()]);
			int river = p.GetWeaponDamage(ContentSamples.ItemsByType[ModContent.ItemType<Content.Items.LingeringStreamTalisman>()]);
			int dmg = (int)(sword * 0.5f);

			// 靶子：身前 200、420、640 像素各一只
			for (int k = 0; k < (what == "domain" ? 0 : 3); k++)
			{
				int n = NPC.NewNPC(src, (int)(p.Center.X + dir * (200 + 220 * k)), (int)p.Bottom.Y - 4, NPCID.BlueSlime);
				NPC npc = Main.npc[n];
				if (what != "river")
					npc.lifeMax = npc.life = 30000; // 剑气靶子打不死；剑河用原版血量，看得出能不能斩死
				npc.damage = 0;
			}

			Vector2 aim = new(dir, 0f);
			switch (what)
			{
				case "qi":
					Projectile.NewProjectile(src, p.Center + aim * 40f, aim * 22f, ModContent.ProjectileType<ClearmoonQi>(), dmg, 2f, p.whoAmI, 0f, 1f);
					DevCapture.Start("qi", 30);
					break;
				case "lance":
					Projectile.NewProjectile(src, p.Center + aim * 40f, aim * 28f, ModContent.ProjectileType<ClearmoonQi>(), (int)(sword * 0.8f), 2f, p.whoAmI, 1f, 1f);
					DevCapture.Start("lance", 30);
					break;
				case "swing":
					Projectile.NewProjectile(src, p.MountedCenter, aim, ModContent.ProjectileType<ClearmoonSwing>(), sword, 2f, p.whoAmI, 0f, dir);
					DevCapture.Start("swing", 14, 1);
					break;
				case "domain":
					// 展开 15 秒；等 3 秒天地褪白、法相浮现后，每 4 帧录一张，录 6 秒
					DevCapture.DomainPreview = 60 * 15;
					DevCapture.Start("domain", 90, 4, 180);
					break;
				case "riverup":
					// 斜着往上 30° 碾出去，看任意角度时贴地和尽头消散
					Projectile.NewProjectile(src, p.Center + aim * 12f, Vector2.Zero, ModContent.ProjectileType<TalismanRiver>(), river, 9f, p.whoAmI, aim.ToRotation() - dir * 0.52f);
					DevCapture.Start("riverup", 40);
					break;
				case "river":
					Projectile.NewProjectile(src, p.Center + aim * 12f, Vector2.Zero, ModContent.ProjectileType<TalismanRiver>(), river, 9f, p.whoAmI, aim.ToRotation());
					DevCapture.Start("river", 40);
					break;
				default:
					Main.NewText("用法：/qyfx qi | lance | swing | river（站在平地上，面朝空旷处）");
					break;
			}
		}
	}
}
