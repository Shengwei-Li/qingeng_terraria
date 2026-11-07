using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QingyiJianxian.Content.NPCs.SwordImmortal;
using Terraria;
using Terraria.ModLoader;

namespace QingyiJianxian.Common
{
	/// <summary>
	/// 开发用：把游戏画面逐帧直接喂给 ffmpeg，录成 60 帧 mp4（和 DevCapture 一起，发布前删掉）。
	/// 录的时候关掉跳帧：电脑跟不上时游戏放慢，而不是丢帧，所以成片始终是满 60 帧、不卡。
	/// 代价是录制时游戏可能慢一点，并且没有声音（配乐后期加）。
	/// 每画一帧，按"这一帧游戏更新了几次"写入：没更新就不写，更新了两次就写两遍，成片的时间始终准确。
	/// </summary>
	public class DevRecorder : ModSystem
	{
		private static Process ffmpeg;
		private static Stream pipe;
		private static Color[] pixels;
		private static int width, height, framesLeft, updates, written, doubled;
		private static string file;
		private static Terraria.Enums.FrameSkipMode oldSkip;
		private static bool oldHideUI;

		public static bool Recording => ffmpeg != null;

		public override void Load()
		{
			if (!Main.dedServ)
				Main.OnPostDraw += Frame;
		}

		public override void Unload()
		{
			Main.OnPostDraw -= Frame;
			Stop();
		}

		public override void OnWorldUnload() => Stop();

		public override void PostUpdateEverything()
		{
			if (Recording)
				updates++;
		}

		public static string Start(int seconds, bool keepUI)
		{
			if (Recording)
				return "已经在录了（/qyrec stop 结束）";
			GraphicsDevice gd = Main.graphics.GraphicsDevice;
			width = gd.PresentationParameters.BackBufferWidth & ~1; // H.264 要求偶数尺寸
			height = gd.PresentationParameters.BackBufferHeight & ~1;
			string dir = Path.Combine(DevCapture.Folder, "videos");
			Directory.CreateDirectory(dir);
			file = Path.Combine(dir, $"rec_{DateTime.Now:MMdd_HHmmss}.mp4");
			string args = $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {width}x{height} -r 60 -i - "
				+ $"-c:v libx264 -preset veryfast -crf 16 -pix_fmt yuv420p -movflags +faststart \"{file}\"";
			ffmpeg = Launch(args);
			if (ffmpeg == null)
				return "找不到 ffmpeg。安装：winget install Gyan.FFmpeg，然后重启 tModLoader";
			pipe = ffmpeg.StandardInput.BaseStream;
			pixels = new Color[width * height];
			framesLeft = seconds * 60;
			updates = written = doubled = 0;
			oldSkip = Main.FrameSkipMode;
			Main.FrameSkipMode = Terraria.Enums.FrameSkipMode.Off;
			oldHideUI = Main.hideUI;
			if (!keepUI)
				Main.hideUI = true;
			return null;
		}

		private static Process Launch(string args)
		{
			string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var candidates = new List<string> { "ffmpeg", Path.Combine(local, "Microsoft", "WinGet", "Links", "ffmpeg.exe") };
			string pkgs = Path.Combine(local, "Microsoft", "WinGet", "Packages");
			if (Directory.Exists(pkgs))
				foreach (string d in Directory.GetDirectories(pkgs, "*FFmpeg*"))
					candidates.AddRange(Directory.GetFiles(d, "ffmpeg.exe", SearchOption.AllDirectories));
			foreach (string exe in candidates)
			{
				try
				{
					return Process.Start(new ProcessStartInfo(exe, args)
					{
						UseShellExecute = false,
						RedirectStandardInput = true,
						CreateNoWindow = true,
					});
				}
				catch (Exception)
				{
					// 这个位置没有，试下一个
				}
			}
			return null;
		}

		private static void Frame(GameTime _)
		{
			if (!Recording)
				return;
			int n = updates;
			updates = 0;
			if (n == 0)
				return; // 画了但游戏没走：重复的画面，不写
			try
			{
				Main.graphics.GraphicsDevice.GetBackBufferData(new Rectangle(0, 0, width, height), pixels, 0, pixels.Length);
				ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(pixels.AsSpan());
				for (int i = 0; i < n && framesLeft > 0; i++, framesLeft--, written++)
					pipe.Write(bytes);
				if (n > 1)
					doubled += n - 1;
			}
			catch (Exception e)
			{
				Main.NewText("[录像] 中断：" + e.Message, Color.OrangeRed);
				framesLeft = 0;
			}
			if (framesLeft <= 0)
				Stop();
		}

		public static void Stop()
		{
			if (!Recording)
				return;
			try
			{
				pipe.Close();
				ffmpeg.WaitForExit(30000);
			}
			catch (Exception)
			{
				// ffmpeg 已经退出
			}
			ffmpeg.Dispose();
			ffmpeg = null;
			pipe = null;
			pixels = null;
			Main.FrameSkipMode = oldSkip;
			Main.hideUI = oldHideUI;
			if (!Main.gameMenu)
			{
				Main.NewText($"[录像] 已保存 {written / 60f:0.0} 秒：{file}", Color.LightSeaGreen);
				if (doubled > 0)
					Main.NewText($"[录像] 有 {doubled} 帧是重复补上的（电脑没跟上），关掉其他程序再录会更顺", Color.Orange);
			}
		}
	}

	/// <summary>/qyrec [秒数] [ui] | stop：开始录像；默认 15 秒、隐藏界面。</summary>
	public class QyrecCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "qyrec";
		public override string Usage => "/qyrec [秒数，默认 15] [ui = 保留界面] | /qyrec stop";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (args.Length > 0 && args[0] == "stop")
			{
				DevRecorder.Stop();
				return;
			}
			int seconds = 15;
			bool keepUI = false;
			foreach (string a in args)
			{
				if (a == "ui")
					keepUI = true;
				else if (int.TryParse(a, out int s))
					seconds = Math.Clamp(s, 1, 300);
			}
			string error = DevRecorder.Start(seconds, keepUI);
			if (error != null)
				Main.NewText("[录像] " + error, Color.OrangeRed);
		}
	}

	/// <summary>
	/// /qyboss 招式 [god]：召出剑仙（已在场就用在场的），血量直接放到招式所在的阶段，之后只反复出这一招，中间留一次空窗。
	/// /qyboss off：恢复正常出招。/qyboss god：切换玩家无敌。
	/// </summary>
	public class QybossCommand : ModCommand
	{
		/// <summary>只反复出这一招（State 的名字）；null 为正常出招。</summary>
		public static string DemoMove;

		/// <summary>演示时玩家无敌，录长镜头不会被打死。</summary>
		public static bool God;

		private static readonly (string key, string state, float life)[] Moves =
		{
			("stroke", "Stroke", 0.9f), ("flight", "Swordflight", 0.9f), ("leaves", "Leaves", 0.9f),
			("flurry", "Flurry", 0.5f), ("river", "River", 0.5f), ("ring", "Ring", 0.5f),
			("hang", "Hang", 0.2f), ("brush", "Brush", 0.2f), ("unbound", "Unbound", 0.2f), ("drunk", "Drunk", 0.2f),
			("p1", null, 0.9f), ("p2", null, 0.5f), ("p3", null, 0.2f), ("final", null, 0.06f),
		};

		public override CommandType Type => CommandType.Chat;
		public override string Command => "qyboss";
		public override string Usage => "/qyboss stroke|flight|leaves|flurry|river|ring|hang|brush|unbound|drunk|p1|p2|p3|final [god] | off | god";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			Player p = caller.Player;
			string what = args.Length > 0 ? args[0] : "";
			if (what == "god")
			{
				God = !God;
				Main.NewText("[演示] 无敌：" + (God ? "开" : "关"));
				return;
			}
			if (Array.IndexOf(args, "god") > 0)
				God = true;
			if (what == "off")
			{
				DemoMove = null;
				Main.NewText("[演示] 恢复正常出招");
				return;
			}
			int found = Array.FindIndex(Moves, m => m.key == what);
			if (found < 0)
			{
				Main.NewText("用法：" + Usage);
				return;
			}
			var (_, state, life) = Moves[found];
			DemoMove = state;
			int type = ModContent.NPCType<SwordImmortal>();
			int i = NPC.FindFirstNPC(type);
			if (i < 0)
				i = NPC.NewNPC(p.GetSource_Misc("qyboss"), (int)p.Center.X + p.direction * 360, (int)p.Center.Y - 160, type, Target: p.whoAmI);
			NPC npc = Main.npc[i];
			npc.life = Math.Max(1, (int)(npc.lifeMax * life));
			npc.netUpdate = true;
			Main.NewText($"[演示] {what}：血量 {life:P0}" + (state != null ? "，只出这一招" : "") + (God ? "，无敌开" : ""));
		}
	}

	public class DemoGodPlayer : ModPlayer
	{
		public override bool FreeDodge(Player.HurtInfo info) => QybossCommand.God;
	}
}
