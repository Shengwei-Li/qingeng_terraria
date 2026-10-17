using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace QingyiJianxian.Common
{
	/// <summary>
	/// Draws a textured ribbon through a list of world points (head first), like a brush stroke.
	/// The texture's x axis runs tail (0) to head (1); its y axis runs across the ribbon.
	/// Uses a BasicEffect, so no compiled shader is needed.
	/// </summary>
	public static class Trail
	{
		public static Asset<Texture2D> Brush => ModContent.Request<Texture2D>("QingyiJianxian/Assets/Effects/BrushStroke");
		public static Asset<Texture2D> Glow => ModContent.Request<Texture2D>("QingyiJianxian/Assets/Effects/GlowStrip");

		/// <summary>A projectile's current center plus its cached old centers, head first (needs TrailCacheLength).</summary>
		public static Vector2[] Centers(Projectile proj, Vector2 offset = default)
		{
			var pts = new Vector2[proj.oldPos.Length + 1];
			pts[0] = proj.Center + offset;
			for (int i = 0; i < proj.oldPos.Length; i++)
				pts[i + 1] = proj.oldPos[i] == Vector2.Zero ? Vector2.Zero : proj.oldPos[i] + proj.Size / 2f + offset;
			return pts;
		}

		private static BasicEffect effect;
		private static VertexPositionColorTexture[] verts = new VertexPositionColorTexture[64];

		/// <param name="points">World positions, head first. Zero vectors end the list early.</param>
		/// <param name="width">Full width at a point, given progress 0 (head) .. 1 (tail).</param>
		/// <param name="color">Color at a point, given progress 0 (head) .. 1 (tail). Premultiplied (use Color * alpha).</param>
		/// <param name="additive">Additive blending, for glowing trails.</param>
		/// <remarks>Call from a draw hook while the sprite batch is running; it is ended and restarted around the draw.</remarks>
		public static void Draw(Vector2[] points, Func<float, float> width, Func<float, Color> color, Texture2D texture, bool additive = false)
		{
			int count = 0;
			while (count < points.Length && points[count] != Vector2.Zero)
				count++;
			if (count < 2 || Main.dedServ)
				return;

			int needed = count * 2;
			if (verts.Length < needed)
				verts = new VertexPositionColorTexture[needed * 2];

			for (int i = 0; i < count; i++)
			{
				float progress = i / (float)(count - 1);
				// 法线：相邻两点方向的垂线
				Vector2 a = points[Math.Max(i - 1, 0)];
				Vector2 b = points[Math.Min(i + 1, count - 1)];
				Vector2 dir = (a - b).SafeNormalize(Vector2.UnitX);
				Vector2 normal = new(-dir.Y, dir.X);
				float half = width(progress) / 2f;
				Color c = color(progress);
				float u = 1f - progress;
				Vector3 p = new(points[i] - Main.screenPosition, 0f);
				verts[i * 2] = new VertexPositionColorTexture(p + new Vector3(normal * half, 0f), c, new Vector2(u, 0f));
				verts[i * 2 + 1] = new VertexPositionColorTexture(p - new Vector3(normal * half, 0f), c, new Vector2(u, 1f));
			}

			GraphicsDevice device = Main.graphics.GraphicsDevice;
			effect ??= new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true };
			effect.Texture = texture;
			effect.World = Matrix.Identity;
			effect.View = Main.GameViewMatrix.TransformationMatrix;
			effect.Projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

			SpriteBatch sb = Main.spriteBatch;
			sb.End();

			device.BlendState = additive ? BlendState.Additive : BlendState.AlphaBlend;
			device.RasterizerState = RasterizerState.CullNone;
			device.SamplerStates[0] = SamplerState.LinearClamp;
			device.DepthStencilState = DepthStencilState.None;
			foreach (EffectPass pass in effect.CurrentTechnique.Passes)
			{
				pass.Apply();
				device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, needed - 2);
			}

			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
		}
	}
}
