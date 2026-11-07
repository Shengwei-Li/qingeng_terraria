using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using QingyiJianxian.Content.NPCs.SwordImmortal;
using Terraria;
using Terraria.ModLoader;

namespace QingyiJianxian.Common
{
	/// <summary>
	/// 开发工具开关：只有本机 ModSources 里有这个模组的源码时才加载录像、演示命令（DevCapture、DevRecorder 里的全部），
	/// 从创意工坊下载的玩家那里这些都不存在，发布前不用删文件。
	/// </summary>
	public static class DevTools
	{
		private static bool? enabled;

		public static bool Enabled => enabled ??= Directory.Exists(Path.Combine(Main.SavePath, "ModSources", "QingyiJianxian"));

		/// <summary>演示录像时替代鼠标位置（武器朝这里出招）；平时为 null，就是鼠标。</summary>
		public static Vector2? Aim;

		public static Vector2 MouseWorld => Aim ?? Main.MouseWorld;
	}

	/// <summary>
	/// 开发用录像（只在开发机上加载，见 DevTools）。
	/// 用 ffmpeg 的桌面复制（ddagrab，和 OBS 的显示器捕获同一个接口）直接从屏幕抓游戏窗口，显卡编码，
	/// 不从游戏里回读画面——回读整屏每帧都卡，还会让显卡驱动重置。所以录制时游戏照常跑，几乎不掉帧。
	/// 没有声音，配乐后期加。
	/// </summary>
	public static class ScreenRecorder
	{
		private static Process ffmpeg;
		private static readonly Stopwatch clock = new();

		/// <summary>视频第一帧对应的 clock 时间（秒）；还没出第一帧时为负。</summary>
		private static double firstFrame = -1;

		public static bool Recording => ffmpeg != null;
		public static bool HasFrames => firstFrame >= 0;
		public static string File { get; private set; }

		/// <summary>现在是视频里的第几秒。</summary>
		public static double VideoTime => clock.Elapsed.TotalSeconds - firstFrame;

		public static string Start(string name)
		{
			if (Recording)
				return "已经在录了";
			string dir = Path.Combine(DevCapture.Folder, "videos");
			Directory.CreateDirectory(dir);
			File = Path.Combine(dir, $"{name}_{DateTime.Now:MMdd_HHmmss}.mkv"); // mkv：万一没正常收尾，文件也能用
			firstFrame = -1;
			clock.Restart();
			// 先试只录游戏窗口 + N 卡编码；不行就录整个屏幕、换 CPU 编码
			foreach (string region in new[] { Region(), "" })
			{
				foreach (string enc in new[] { "h264_nvenc -preset p4 -rc vbr -cq 19 -b:v 0", "libx264 -preset veryfast -crf 18" })
				{
					string args = $"-y -loglevel error -nostats -progress pipe:1 -f lavfi -i \"ddagrab=framerate=60:draw_mouse=0{region}\" "
						+ $"-vf hwdownload,format=bgra -fps_mode cfr -r 60 -c:v {enc} -pix_fmt yuv420p \"{File}\"";
					ffmpeg = Launch(args);
					if (ffmpeg != null && !ffmpeg.WaitForExit(1500))
						break;
					ffmpeg = null;
				}
				if (ffmpeg != null)
					break;
			}
			if (ffmpeg == null)
				return "ffmpeg 启动失败（没装的话：winget install Gyan.FFmpeg，然后重启 tModLoader）";
			Process p = ffmpeg;
			p.OutputDataReceived += (_, e) =>
			{
				// -progress 每半秒报一次 frame=N；第一次 N>0 时反推出第一帧的时间
				if (firstFrame < 0 && e.Data != null && e.Data.StartsWith("frame=")
					&& int.TryParse(e.Data.AsSpan(6), out int n) && n > 0)
					firstFrame = clock.Elapsed.TotalSeconds - n / 60.0;
			};
			p.BeginOutputReadLine();
			return null;
		}

		/// <summary>只录游戏窗口那一块（窗口在主显示器上、尺寸对得上时），否则录整个主显示器。</summary>
		private static string Region()
		{
			try
			{
				Rectangle r = Main.instance.Window.ClientBounds;
				int w = Main.screenWidth & ~1, h = Main.screenHeight & ~1;
				if (r.Width == Main.screenWidth && r.Height == Main.screenHeight && r.X >= 0 && r.Y >= 0)
					return $":offset_x={r.X}:offset_y={r.Y}:video_size={w}x{h}";
			}
			catch (Exception)
			{
				// 拿不到窗口位置就录整个屏幕
			}
			return "";
		}

		internal static Process Launch(string args)
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
						RedirectStandardOutput = true,
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

		/// <summary>结束录制（让 ffmpeg 自己收尾，不卡游戏）；done 在文件写完后在后台线程调用。</summary>
		public static void Stop(Action<string> done = null)
		{
			if (!Recording)
				return;
			Process p = ffmpeg;
			string file = File;
			ffmpeg = null;
			Task.Run(() =>
			{
				try
				{
					p.StandardInput.Write("q");
					p.StandardInput.Flush();
					if (!p.WaitForExit(20000))
						p.Kill();
				}
				catch (Exception)
				{
					// 已经退出
				}
				p.Dispose();
				done?.Invoke(file);
			});
		}
	}

	/// <summary>
	/// /qyshow：一条命令录完剑仙所有招式。在头顶召出剑仙（玩家无敌、隐藏界面），按阶段顺序每招只出一次、录几秒，
	/// 最后是最后一剑。录完自动切成一招一个短片，放在 videos/&lt;名字&gt;_clips/ 里。
	/// 录制中输入 /qyshow stop 中止。
	/// </summary>
	public class BossShow : ModSystem
	{
		// (片名, 招式, 血量, 录几秒)：按阶段往后排，阶段只能往后走
		private static readonly (string clip, string state, float life, float seconds)[] Steps =
		{
			("01_一剑横江", "Stroke", 0.9f, 3.5f),
			("02_御剑术", "Swordflight", 0.9f, 4.5f),
			("03_落叶成剑", "Leaves", 0.9f, 4.5f),
			("04_转二阶段", "Transition", 0.5f, 3f),
			("05_剑河", "River", 0.5f, 5f),
			("06_瞬身连斩", "Flurry", 0.5f, 3.5f),
			("07_剑阵", "Ring", 0.5f, 4.5f),
			("08_展开领域", "Transition", 0.2f, 4f),
			("09_万剑悬空", "Hang", 0.2f, 5f),
			("10_一笔画地", "Brush", 0.2f, 6.5f),
			("11_剑气纵横", "Unbound", 0.2f, 4.5f),
			("12_醉剑", "Drunk", 0.2f, 4.5f),
			("13_最后一剑", "Final", 0.04f, 12f),
		};

		private const float LeadIn = 0.8f;   // 每段从招式开始前 0.8 秒切
		private const int WaitLimit = 60 * 25; // 25 秒等不到这一招就跳过

		private static int step = -1;
		private static int wait;
		private static bool started;
		private static double stepEnd;
		private static bool oldHideUI;
		private static float oldZoom;
		private const float ShowZoom = 1.5f; // 放大：剑仙和招式在 2K 画面里不会太小
		private const float LiftScreens = 0.2f; // 镜头往上抬 0.2 屏：少拍地下的黑，多拍天上的招式
		private static readonly List<(string clip, double at, float seconds)> marks = new();

		public static bool Running => step >= 0;

		public override bool IsLoadingEnabled(Mod mod) => DevTools.Enabled;

		/// <summary>录像时放大、镜头上抬（剑仙和武器演示共用）。</summary>
		/// <param name="zoom">放大倍数（1-2）</param>
		/// <param name="lift">镜头上抬几屏</param>
		/// <param name="shift">镜头往前（朝 shift 的正负方向）挪几屏：武器朝前打，人物站在左三分之一</param>
		public static void BeginFraming(float zoom = ShowZoom, float lift = LiftScreens, float shift = 0f)
		{
			oldHideUI = Main.hideUI;
			Main.hideUI = true;
			oldZoom = Main.GameZoomTarget;
			Main.GameZoomTarget = frameZoom = zoom;
			frameLift = lift;
			frameShift = shift;
			framing = true;
			QybossCommand.God = true;
		}

		public static void EndFraming()
		{
			framing = false;
			Main.hideUI = oldHideUI;
			Main.GameZoomTarget = oldZoom;
			QybossCommand.God = false;
		}

		private static bool framing;
		private static float frameZoom, frameLift, frameShift;

		public static string Begin()
		{
			if (Running || WeaponShow.Running || ScreenRecorder.Recording)
				return "已经在录了（/qyshow stop 中止）";
			if (Main.netMode != Terraria.ID.NetmodeID.SinglePlayer)
				return "只能在单人模式里录";
			foreach (NPC n in Main.npc)
				if (n.active && n.ModNPC is SwordImmortal or FinalSword)
					n.active = false; // 从头开始，阶段要从一阶段走起
			string error = ScreenRecorder.Start("show");
			if (error != null)
				return error;
			marks.Clear();
			BeginFraming();
			step = 0;
			wait = 0;
			started = false;
			return null;
		}

		public static void Abort()
		{
			if (!Running)
				return;
			Finish(cut: false);
			Main.NewText("[录像] 已中止");
		}

		public override void ModifyScreenPosition()
		{
			if (framing)
			{
				Main.screenPosition.Y -= Main.screenHeight * frameLift / frameZoom;
				Main.screenPosition.X += Main.screenWidth * frameShift / frameZoom;
			}
		}

		public override void OnWorldUnload()
		{
			if (Running)
				Finish(cut: false);
		}

		public override void PostUpdateEverything()
		{
			if (!Running || Main.gameMenu)
				return;
			// ffmpeg 刚启动的几秒还没有画面：等第一帧出来再召剑仙
			if (!ScreenRecorder.HasFrames)
				return;

			Player player = Main.LocalPlayer;
			int type = ModContent.NPCType<SwordImmortal>();
			int i = NPC.FindFirstNPC(type);
			var (clip, state, life, seconds) = Steps[step];

			if (!started)
			{
				// 进入这一步：只出这一招，血量放到这一招的阶段
				QybossCommand.DemoMove = state is "Transition" or "Final" ? null : state;
				if (i < 0)
					i = NPC.NewNPC(player.GetSource_Misc("qyshow"), (int)player.Center.X + player.direction * 360, (int)player.Center.Y - 160, type, Target: player.whoAmI);
				NPC boss = Main.npc[i];
				if (boss.life > boss.lifeMax * life)
					boss.life = Math.Max(1, (int)(boss.lifeMax * life));
				started = true;
				wait = 0;
				stepEnd = -1;
				return;
			}

			if (i < 0)
			{
				Finish(cut: true);
				return;
			}
			var immortal = (SwordImmortal)Main.npc[i].ModNPC;
			if (stepEnd < 0)
			{
				if (immortal.StateName == state)
				{
					double at = ScreenRecorder.VideoTime;
					marks.Add((clip, at, seconds));
					stepEnd = at + seconds;
				}
				else if (++wait > WaitLimit)
					Next();
				return;
			}
			if (ScreenRecorder.VideoTime >= stepEnd)
				Next();
		}

		private static void Next()
		{
			started = false;
			if (++step >= Steps.Length)
				Finish(cut: true);
		}

		private static void Finish(bool cut)
		{
			step = -1;
			QybossCommand.DemoMove = null;
			EndFraming();
			foreach (NPC n in Main.npc)
				if (n.active && n.ModNPC is SwordImmortal or FinalSword)
					n.active = false; // 不走结局，不给礼物，不记战胜
			var clips = new List<(string clip, double at, float seconds)>(marks);
			if (!cut)
			{
				ScreenRecorder.Stop();
				return;
			}
			Main.NewText("[录像] 录完了，正在切片，请稍等……", Color.LightSeaGreen);
			ScreenRecorder.Stop(file => Cut(file, clips));
		}

		/// <summary>后台把整段视频切成一招一个短片，再拼一个合集。</summary>
		internal static void Cut(string file, List<(string clip, double at, float seconds)> clips)
		{
			string dir = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "_clips");
			Directory.CreateDirectory(dir);
			var list = new StringBuilder();
			foreach (var (clip, at, seconds) in clips)
			{
				string start = Math.Max(0, at - LeadIn).ToString("0.000", CultureInfo.InvariantCulture);
				string len = (seconds + LeadIn).ToString("0.000", CultureInfo.InvariantCulture);
				string output = Path.Combine(dir, clip + ".mp4");
				Run($"-y -loglevel error -ss {start} -i \"{file}\" -t {len} -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -an \"{output}\"");
				list.AppendLine($"file '{output.Replace("\\", "/").Replace("'", "'\\''")}'");
			}
			string listFile = Path.Combine(dir, "list.txt");
			System.IO.File.WriteAllText(listFile, list.ToString());
			Run($"-y -loglevel error -f concat -safe 0 -i \"{listFile}\" -c copy \"{Path.Combine(dir, "00_合集.mp4")}\"");
			System.IO.File.Delete(listFile);
			Main.QueueMainThreadAction(() => Main.NewText($"[录像] 完成：{dir}", Color.LightSeaGreen));
		}

		private static void Run(string args)
		{
			using Process p = ScreenRecorder.Launch(args);
			p?.WaitForExit(120000);
		}
	}

	/// <summary>/qyshow：一条命令录完所有招式；/qyshow stop 中止。</summary>
	public class QyshowCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "qyshow";
		public override string Usage => "/qyshow | /qyshow weapons | /qyshow stop";

		public override bool IsLoadingEnabled(Mod mod) => DevTools.Enabled;

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (args.Length > 0 && args[0] == "stop")
			{
				BossShow.Abort();
				WeaponShow.Abort();
				return;
			}
			bool weapons = args.Length > 0 && args[0] == "weapons";
			if (weapons)
			{
				string err = WeaponShow.Begin();
				Main.NewText(err == null ? "[录像] 开始录武器：站着别动，大约 20 秒，录完会提示" : "[录像] " + err, err == null ? Color.LightSeaGreen : Color.OrangeRed);
				return;
			}
			string error = BossShow.Begin();
			Main.NewText(error == null ? "[录像] 开始：站着别动，大约两分钟，录完会提示" : "[录像] " + error, error == null ? Color.LightSeaGreen : Color.OrangeRed);
		}
	}

	/// <summary>
	/// /qyboss 招式 [god]：召出剑仙（已在场就用在场的），血量放到招式所在的阶段，之后只反复出这一招，中间留一次空窗。
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
			("p1", null, 0.9f), ("p2", null, 0.5f), ("p3", null, 0.2f), ("final", null, 0.04f),
		};

		public override CommandType Type => CommandType.Chat;
		public override string Command => "qyboss";
		public override string Usage => "/qyboss stroke|flight|leaves|flurry|river|ring|hang|brush|unbound|drunk|p1|p2|p3|final [god] | off | god";

		public override bool IsLoadingEnabled(Mod mod) => DevTools.Enabled;

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
		public override bool IsLoadingEnabled(Mod mod) => DevTools.Enabled;

		public override bool FreeDodge(Player.HurtInfo info) => QybossCommand.God;

		// 武器演示：替玩家按住左键、不让方向键乱动
		public override void SetControls()
		{
			if (!WeaponShow.Running || Player.whoAmI != Main.myPlayer)
				return;
			Player.controlLeft = Player.controlRight = Player.controlUp = Player.controlDown = false;
			Player.controlJump = Player.controlUseTile = false;
			Player.controlUseItem = WeaponShow.Hold > 0;
		}

		// 出招时面朝演示的方向，而不是真实鼠标
		public override void PostItemCheck()
		{
			if (DevTools.Aim is Vector2 aim && Player.whoAmI == Main.myPlayer)
				Player.ChangeDir(aim.X >= Player.Center.X ? 1 : -1);
		}
	}

	/// <summary>
	/// /qyshow weapons：一条命令录完清月剑三段连击、御剑、清流剑符（平射和斜上）。
	/// 临时把手上那格换成要演示的武器、替玩家按键、朝前方出招，身前摆三只靶子；录完换回原来的物品，自动切片。
	/// </summary>
	public class WeaponShow : ModSystem
	{
		// (片名, 动作, 录几秒)
		private static readonly (string clip, string action, float seconds)[] Steps =
		{
			("01_清月剑_三段连击", "combo", 3.5f),
			("02_清月剑_御剑", "flight", 3.5f),
			("03_清流剑符", "river", 3.5f),
			("04_清流剑符_斜上", "riverup", 3.5f),
		};

		private const float LeadIn = 0.8f;

		private static int step = -1;
		private static bool started;
		private static int settle; // 开录后先等 1.5 秒：界面隐藏、镜头放大都到位了再出招
		private static double stepStart;
		private static int dir;
		private static int slot;
		private static Item original;
		private static readonly List<int> targets = new();
		private static readonly List<(string clip, double at, float seconds)> marks = new();

		/// <summary>还要替玩家按住左键几帧。</summary>
		public static int Hold;

		public static bool Running => step >= 0;

		public override bool IsLoadingEnabled(Mod mod) => DevTools.Enabled;

		public static string Begin()
		{
			if (Running || BossShow.Running || ScreenRecorder.Recording)
				return "已经在录了（/qyshow stop 中止）";
			if (Main.netMode != Terraria.ID.NetmodeID.SinglePlayer)
				return "只能在单人模式里录";
			string error = ScreenRecorder.Start("weapons");
			if (error != null)
				return error;
			Player p = Main.LocalPlayer;
			dir = p.direction;
			slot = p.selectedItem;
			original = p.inventory[slot];
			marks.Clear();
			BossShow.BeginFraming(2f, 0.08f, dir * 0.22f);
			step = 0;
			started = false;
			settle = 90;
			return null;
		}

		public static void Abort()
		{
			if (!Running)
				return;
			Finish(cut: false);
			Main.NewText("[录像] 已中止");
		}

		public override void OnWorldUnload()
		{
			if (Running)
				Finish(cut: false);
		}

		public override void PostUpdateEverything()
		{
			if (!Running || Main.gameMenu || !ScreenRecorder.HasFrames)
				return;
			if (settle > 0)
			{
				settle--;
				return;
			}
			if (Hold > 0)
				Hold--;
			Player p = Main.LocalPlayer;
			var (clip, action, seconds) = Steps[step];
			if (!started)
			{
				started = true;
				ClearTargets();
				// 三只靶子：身前 200、420、640 像素；剑符的靶子用原版血量，看得出斩不斩得死
				for (int k = 0; k < 3; k++)
				{
					int n = NPC.NewNPC(p.GetSource_Misc("qyshow"), (int)(p.Center.X + dir * (200 + 220 * k)), (int)p.Bottom.Y - 4, Terraria.ID.NPCID.BlueSlime);
					NPC npc = Main.npc[n];
					if (!action.StartsWith("river"))
						npc.lifeMax = npc.life = 30000;
					npc.damage = 0;
					targets.Add(n);
				}
				bool talisman = action.StartsWith("river");
				int type = talisman ? ModContent.ItemType<Content.Items.LingeringStreamTalisman>() : ModContent.ItemType<Content.Items.Clearmoon>();
				if (p.inventory[slot].type != type)
					p.inventory[slot] = new Item(type, talisman ? 5 : 1);
				p.selectedItem = slot;
				Vector2 ahead = new(dir * 400f, action == "riverup" ? -230f : 0f);
				DevTools.Aim = p.Center + ahead;
				switch (action)
				{
					case "combo":
						Hold = 56; // 三剑：16 + 16 + 21 帧
						break;
					case "flight":
						// 和右键御剑一样：剑脱手飞到前方偏上，连斩三剑后飞回
						Vector2 offset = new(dir * 380f, -60f);
						Vector2 point = p.MountedCenter + offset;
						int damage = (int)(p.GetWeaponDamage(p.inventory[slot]) * 0.75f);
						Projectile.NewProjectile(p.GetSource_ItemUse(p.inventory[slot]), p.MountedCenter, offset.SafeNormalize(Vector2.UnitX) * 6f,
							ModContent.ProjectileType<Content.Projectiles.ClearmoonFlight>(), damage, 5f, p.whoAmI, point.X, point.Y);
						break;
					default:
						Hold = 2; // 剑符：点一下
						break;
				}
				stepStart = ScreenRecorder.VideoTime;
				marks.Add((clip, stepStart, seconds));
				return;
			}
			if (ScreenRecorder.VideoTime >= stepStart + seconds)
			{
				started = false;
				if (++step >= Steps.Length)
					Finish(cut: true);
			}
		}

		private static void ClearTargets()
		{
			foreach (int n in targets)
				if (Main.npc[n].active && Main.npc[n].type == Terraria.ID.NPCID.BlueSlime)
					Main.npc[n].active = false;
			targets.Clear();
		}

		private static void Finish(bool cut)
		{
			step = -1;
			Hold = 0;
			DevTools.Aim = null;
			ClearTargets();
			Player p = Main.LocalPlayer;
			if (original != null)
				p.inventory[slot] = original;
			original = null;
			BossShow.EndFraming();
			var clips = new List<(string clip, double at, float seconds)>(marks);
			if (!cut)
			{
				ScreenRecorder.Stop();
				return;
			}
			Main.NewText("[录像] 录完了，正在切片，请稍等……", Color.LightSeaGreen);
			ScreenRecorder.Stop(file => BossShow.Cut(file, clips));
		}
	}
}
