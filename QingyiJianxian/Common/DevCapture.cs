#if DEBUG
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
	/// 开发用：把游戏画面一帧帧存成 PNG，供离线检查特效（只在从源码构建的调试版里存在，发布版没有）。
	/// 截的是最终画面（含光照、背景、地形），画面中央 1280×720 的一块，每 2 帧存一张。
	/// 存到 tModLoader 存档目录下的 QingyiCaptures/&lt;名字&gt;/。
	/// </summary>
	public class DevCapture : ModSystem
	{
		private static string name;
		private static int framesLeft;
		private static int every;
		private static int tick;
		private static int index;
		private static readonly List<(Color[] data, int w, int h, int i)> pending = new();

		public static string Folder => Path.Combine(Main.SavePath, "QingyiCaptures");

		public static void Start(string captureName, int frames, int stride = 2)
		{
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

		private static void Capture(GameTime _)
		{
			if (framesLeft <= 0 || name == null)
				return;
			if (tick++ % every != 0)
				return;
			GraphicsDevice gd = Main.graphics.GraphicsDevice;
			int bw = gd.PresentationParameters.BackBufferWidth, bh = gd.PresentationParameters.BackBufferHeight;
			int w = Math.Min(1280, bw), h = Math.Min(720, bh);
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
		public override string Usage => "/qyfx qi|lance|swing|river|all";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			Player p = caller.Player;
			string what = args.Length > 0 ? args[0] : "all";
			int dir = p.direction;
			var src = p.GetSource_Misc("qyfx");
			int dmg = 60;

			// 靶子：身前 200、420、640 像素各一只
			for (int k = 0; k < 3; k++)
			{
				int n = NPC.NewNPC(src, (int)(p.Center.X + dir * (200 + 220 * k)), (int)p.Bottom.Y - 4, NPCID.BlueSlime);
				NPC npc = Main.npc[n];
				npc.lifeMax = npc.life = 30000;
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
					Projectile.NewProjectile(src, p.Center + aim * 40f, aim * 28f, ModContent.ProjectileType<ClearmoonQi>(), dmg, 2f, p.whoAmI, 1f, 1f);
					DevCapture.Start("lance", 30);
					break;
				case "swing":
					Projectile.NewProjectile(src, p.MountedCenter, aim, ModContent.ProjectileType<ClearmoonSwing>(), dmg, 2f, p.whoAmI, 0f, dir);
					DevCapture.Start("swing", 14, 1);
					break;
				case "river":
					Projectile.NewProjectile(src, new Vector2(p.Center.X + dir * 12f, p.Center.Y), Vector2.Zero, ModContent.ProjectileType<TalismanRiver>(), dmg, 9f, p.whoAmI, dir, p.Center.Y);
					DevCapture.Start("river", 40);
					break;
				default:
					Main.NewText("用法：/qyfx qi | lance | swing | river（站在平地上，面朝空旷处）");
					break;
			}
		}
	}
}
#endif
