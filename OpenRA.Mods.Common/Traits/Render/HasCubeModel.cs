using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
    public struct SimpleVertex
    {
        // 3d position
        public readonly float X, Y, Z;

        // Texture coordinates
        public readonly float U, V;

        // Color
        public readonly float R, G, B, A;

        public SimpleVertex(float x, float y, float z, float u, float v, float r, float g, float b, float a)
        {
            X = x;
            Y = y;
            Z = z;
            U = u;
            V = v;
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }

    public class CubeRenderable<T> : IRenderable, IFinalizedRenderable where T : struct
    {
        public WPos Pos { get; private set; }
        public PaletteReference Palette { get; private set; }
        public int ZOffset { get; private set; }
        public bool IsDecoration { get; private set; }

        readonly IShader shader;
        readonly IVertexBuffer<T> vertexBuffer;

        public CubeRenderable(IShader shader, IVertexBuffer<T> vertexBuffer)
        {
            this.shader = shader;
            this.vertexBuffer = vertexBuffer;
        }

        public IRenderable WithPalette(PaletteReference newPalette)
        {
            return this;
        }

        public IRenderable WithZOffset(int newOffset)
        {
            return this;
        }

        public IRenderable OffsetBy(WVec offset)
        {
            return this;
        }

        public IRenderable AsDecoration()
        {
            return this;
        }

        public IFinalizedRenderable PrepareRender(WorldRenderer wr)
        {
            return this;
        }

        public void Render(WorldRenderer wr)
        {
            var model = OpenRA.Graphics.Util.IdentityMatrix();
            var view = OpenRA.Graphics.Util.IdentityMatrix();
            var projection = OpenRA.Graphics.Util.IdentityMatrix();

            shader.PrepareRender();
            shader.SetMatrix("uModel", model);
            shader.SetMatrix("uView", view);
            shader.SetMatrix("uProjection", projection);
            Game.Renderer.DrawBatch(vertexBuffer, 0, vertexBuffer.Length, PrimitiveType.TriangleList);
        }

        public void RenderDebugGeometry(WorldRenderer wr)
        {
        }

        public Rectangle ScreenBounds(WorldRenderer wr)
        {
            return wr.Viewport.Rectangle;
        }
    }

    public class WithCubeModelInfo : TraitInfo
    {
        public override object Create(ActorInitializer init)
        {
            return new WithCubeModel();
        }
    }

    public class WithCubeModel : IRender
    {
        static IShader shader;

        readonly SimpleVertex[] vertices;
        readonly IVertexBuffer<SimpleVertex> vertexBuffer;

        public WithCubeModel()
        {
            if (shader == null)
                shader = Game.Renderer.CreateShader("glsl/cube");

            vertices = new[]
            {
	            new SimpleVertex(0, 0, 0, 0, 0, 1, 0, 0, 1),
	            new SimpleVertex(0, 1, 0, 0, 1, 1, 0, 0, 1),
	            new SimpleVertex(1, 1, 0, 1, 1, 1, 0, 0, 1),
	            new SimpleVertex(1, 1, 0, 1, 1, 1, 0, 0, 1),
	            new SimpleVertex(1, 0, 0, 1, 0, 1, 0, 0, 1),
	            new SimpleVertex(0, 0, 0, 0, 0, 1, 0, 0, 1),

	            new SimpleVertex(0, 0, 1, 0, 0, 0, 1, 0, 1),
	            new SimpleVertex(0, 1, 1, 0, 1, 0, 1, 0, 1),
	            new SimpleVertex(1, 1, 1, 1, 1, 0, 1, 0, 1),
	            new SimpleVertex(1, 1, 1, 1, 1, 0, 1, 0, 1),
	            new SimpleVertex(1, 0, 1, 1, 0, 0, 1, 0, 1),
	            new SimpleVertex(0, 0, 1, 0, 0, 0, 1, 0, 1),

	            new SimpleVertex(0, 0, 0, 0, 0, 0, 0, 1, 1),
	            new SimpleVertex(0, 0, 1, 0, 1, 0, 0, 1, 1),
	            new SimpleVertex(0, 1, 1, 1, 1, 0, 0, 1, 1),
	            new SimpleVertex(0, 1, 1, 1, 1, 0, 0, 1, 1),
	            new SimpleVertex(0, 1, 0, 1, 0, 0, 0, 1, 1),
	            new SimpleVertex(0, 0, 0, 0, 0, 0, 0, 1, 1),

	            new SimpleVertex(1, 0, 0, 0, 0, 0, 1, 1, 1),
	            new SimpleVertex(1, 0, 1, 0, 1, 0, 1, 1, 1),
	            new SimpleVertex(1, 1, 1, 1, 1, 0, 1, 1, 1),
	            new SimpleVertex(1, 1, 1, 1, 1, 0, 1, 1, 1),
	            new SimpleVertex(1, 1, 0, 1, 0, 0, 1, 1, 1),
	            new SimpleVertex(1, 0, 0, 0, 0, 0, 1, 1, 1),

	            new SimpleVertex(0, 0, 0, 0, 0, 1, 0, 1, 1),
	            new SimpleVertex(0, 0, 1, 0, 1, 1, 0, 1, 1),
	            new SimpleVertex(1, 0, 1, 1, 1, 1, 0, 1, 1),
	            new SimpleVertex(1, 0, 1, 1, 1, 1, 0, 1, 1),
	            new SimpleVertex(1, 0, 0, 1, 0, 1, 0, 1, 1),
	            new SimpleVertex(0, 0, 0, 0, 0, 1, 0, 1, 1),

	            new SimpleVertex(0, 1, 0, 0, 0, 1, 1, 0, 1),
	            new SimpleVertex(0, 1, 1, 0, 1, 1, 1, 0, 1),
	            new SimpleVertex(1, 1, 1, 1, 1, 1, 1, 0, 1),
	            new SimpleVertex(1, 1, 1, 1, 1, 1, 1, 0, 1),
	            new SimpleVertex(1, 1, 0, 1, 0, 1, 1, 0, 1),
	            new SimpleVertex(0, 1, 0, 0, 0, 1, 1, 0, 1),
            };

            vertexBuffer = Game.Renderer.CreateVertexBuffer<SimpleVertex>(vertices.Length);
            vertexBuffer.SetData(vertices, vertices.Length);
        }

        public IEnumerable<IRenderable> Render(Actor self, WorldRenderer wr)
        {
            yield return new CubeRenderable<SimpleVertex>(shader, vertexBuffer);
        }

        public IEnumerable<Rectangle> ScreenBounds(Actor self, WorldRenderer wr)
        {
            yield return new Rectangle(0, 0, 100, 100);
        }
    }
}
