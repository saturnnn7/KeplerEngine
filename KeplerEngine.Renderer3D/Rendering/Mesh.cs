using System;
using Silk.NET.OpenGL;

namespace KeplerEngine.Renderer3D.Rendering;

/// <summary>A wrapper for VAO + VBO. Renders via DrawArrays.</summary>
public class Mesh : IDisposable
{
    private readonly GL   _gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly int  _count;
    private readonly GLEnum _primitive;

    public Mesh(GL gl, float[] vertices, GLEnum primitive = GLEnum.Lines)
    {
        _gl        = gl;
        _primitive = primitive;
        _count     = vertices.Length / 3;   // Every 3 floats = one vertex (x, y, z)

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();

        gl.BindVertexArray(_vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, _vbo);

        unsafe
        {
            fixed (float* p = vertices)
                gl.BufferData(GLEnum.ArrayBuffer,
                    (nuint)(vertices.Length * sizeof(float)),
                    p, GLEnum.StaticDraw);
        }

        // layout location=0 → vec3 position
        gl.EnableVertexAttribArray(0);
        unsafe { gl.VertexAttribPointer(0, 3, GLEnum.Float, false, 3 * sizeof(float), (void*)0); }

        gl.BindVertexArray(0);
    }

    public void Draw()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(_primitive, 0, (uint)_count);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
    }
}