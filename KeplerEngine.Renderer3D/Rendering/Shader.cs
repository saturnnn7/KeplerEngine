using System;
using Silk.NET.OpenGL;

namespace KeplerEngine.Renderer3D.Rendering;

public class Shader : IDisposable
{
    private readonly GL  _gl;
    public  readonly uint Program;

    public Shader(GL gl, string vertSrc, string fragSrc)
    {
        _gl = gl;

        uint vert = Compile(gl, ShaderType.VertexShader,   vertSrc);
        uint frag = Compile(gl, ShaderType.FragmentShader, fragSrc);

        Program = gl.CreateProgram();
        gl.AttachShader(Program, vert);
        gl.AttachShader(Program, frag);
        gl.LinkProgram(Program);

        gl.GetProgram(Program, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
            throw new Exception($"Shader link error: {gl.GetProgramInfoLog(Program)}");

        gl.DeleteShader(vert);
        gl.DeleteShader(frag);
    }

    public void Use() => _gl.UseProgram(Program);

    public void SetMat4(string name, float[] m)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        if (loc < 0) return;
        unsafe { fixed (float* p = m) _gl.UniformMatrix4(loc, 1, false, p); }
    }

    public void SetVec3(string name, float x, float y, float z)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        if (loc >= 0) _gl.Uniform3(loc, x, y, z);
    }

    public void SetVec4(string name, float x, float y, float z, float w)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        if (loc >= 0) _gl.Uniform4(loc, x, y, z, w);
    }

    public void SetFloat(string name, float v)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        if (loc >= 0) _gl.Uniform1(loc, v);
    }

    private static uint Compile(GL gl, ShaderType type, string src)
    {
        uint s = gl.CreateShader(type);
        gl.ShaderSource(s, src);
        gl.CompileShader(s);
        gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
            throw new Exception($"Shader compile error ({type}): {gl.GetShaderInfoLog(s)}");
        return s;
    }

    public void Dispose() => _gl.DeleteProgram(Program);
}