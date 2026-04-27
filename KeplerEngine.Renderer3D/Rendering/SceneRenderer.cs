using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using KeplerEngine.Renderer3D.Core;
using KeplerEngine.Physics;
using KeplerEngine.Orbital;
using KeplerEngine.Simulation;

namespace KeplerEngine.Renderer3D.Rendering;

public class SceneRenderer : IDisposable
{
    private readonly GL     _gl;
    private readonly Shader _shader;
    

    // Scale: number of OpenGL units per meter
    // Kerbin radius = 600 km → in OpenGL = 1.0
    private float _scale;

    private Mesh? _predictedOrbitMesh;

    public float Scale => _scale;

    // Geometry
    private readonly List<(Mesh mesh, float[] color, float[] transform)> _drawList = new();
    private Mesh? _gridMesh;
    private Mesh? _axesMesh;

    // GLSL source
    private const string VertSrc = @"
#version 330 core
layout(location = 0) in vec3 aPos;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProj;
void main() {
    gl_Position = uProj * uView * uModel * vec4(aPos, 1.0);
}";

    private const string FragSrc = @"
#version 330 core
uniform vec4 uColor;
out vec4 FragColor;
void main() {
    FragColor = uColor;
}";

    public SceneRenderer(GL gl, float referenceRadius)
    {
        _gl     = gl;
        _shader = new Shader(gl, VertSrc, FragSrc);
        _scale  = 1f / (float)referenceRadius;

        gl.Enable(GLEnum.DepthTest);
        gl.Enable(GLEnum.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.LineWidth(1.5f);
    }

    public void BuildScene(OrbitSimulation sim)
    {
        // Cleaning old meshes
        foreach (var (m, _, _) in _drawList) m.Dispose();
        _drawList.Clear();
        _gridMesh?.Dispose();
        _axesMesh?.Dispose();

        // Mesh and axes
        _gridMesh = new Mesh(_gl, MeshFactory.GridLines(5f, 20));
        _axesMesh = new Mesh(_gl, MeshFactory.AxesLines(2f));

        // Planets
        foreach (var body in sim.Celestials)
        {
            float r = (float)body.Radius * _scale;
            var sphere = new Mesh(_gl, MeshFactory.WireframeSphere(r));
            var color  = new[]{ 0.29f, 0.56f, 0.85f, 1f };
            var model  = MathHelper.Translation(
                (float)body.Position.X * _scale,
                (float)body.Position.Z * _scale,
                (float)body.Position.Y * _scale);
            _drawList.Add((sphere, color, model));
        }

        // Orbits and satellites
        foreach (var body in sim.Orbitals)
        {
            // Orbital path
            var orbit = new Mesh(_gl, MeshFactory.OrbitLine(body, _scale));
            _drawList.Add((orbit, new[]{ 0.18f, 0.80f, 0.44f, 0.8f }, MathHelper.Identity()));

            // Satellite
            float sr = (float)body.Primary.Radius * _scale * 0.015f;
            sr = Math.Max(sr, 0.02f);
            var sat = new Mesh(_gl, MeshFactory.WireframeSphere(sr, 16), GLEnum.Lines);
            _drawList.Add((sat, new[]{ 0.91f, 0.30f, 0.24f, 1f },
                MathHelper.Translation(
                    (float)body.Position.X * _scale,
                    (float)body.Position.Z * _scale,
                    (float)body.Position.Y * _scale)));
        }
    }

    public void UpdateDynamic(OrbitSimulation sim)
    {
        // Recalculate only the satellite positions
        // A simple approach is to rebuild the entire scene every frame
        // (this is fast for a few objects; we'll optimize it later)
        BuildScene(sim);
    }

    public void Render(Camera camera, int width, int height)
    {
        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.ClearColor(0.05f, 0.05f, 0.10f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _shader.Use();

        float aspect = width / (float)height;
        _shader.SetMat4("uView", camera.GetView());
        _shader.SetMat4("uProj", camera.GetProjection(aspect));

        // Mesh
        if (_gridMesh != null)
        {
            _shader.SetMat4("uModel", MathHelper.Identity());
            _shader.SetVec4("uColor", 0.1f, 0.1f, 0.2f, 1f);
            _gridMesh.Draw();
        }

        // Axes
        if (_axesMesh != null)
        {
            _shader.SetMat4("uModel", MathHelper.Identity());
            // X красный
            _shader.SetVec4("uColor", 1f, 0.2f, 0.2f, 1f);
            _gl.BindVertexArray(0);
            _shader.SetMat4("uModel", MathHelper.Identity());
            _axesMesh.Draw();
        }

        // All other locations
        foreach (var (mesh, color, model) in _drawList)
        {
            _shader.SetMat4("uModel", model);
            _shader.SetVec4("uColor", color[0], color[1], color[2], color[3]);
            mesh.Draw();
        }

        if (_predictedOrbitMesh != null)
        {
            _shader.SetMat4("uModel", MathHelper.Identity());
            _shader.SetVec4("uColor", 0.95f, 0.50f, 0.10f, 0.9f);
            _predictedOrbitMesh.Draw();
        }
    }

    public void Dispose()
    {
        foreach (var (m, _, _) in _drawList) m.Dispose();
        _gridMesh?.Dispose();
        _axesMesh?.Dispose();
        _shader.Dispose();
        _predictedOrbitMesh?.Dispose();
    }

    public void SetPredictedOrbit(KeplerianElements? el, OrbitalBody referenceBody)
    {
        _predictedOrbitMesh?.Dispose();
        _predictedOrbitMesh = null;
    
        if (el == null || referenceBody == null) return;
    
        // Reset true anomaly so orbit draws from 0, not from maneuver node
        var drawEl = el.Clone();
        drawEl.TrueAnomaly = 0;
    
        var snap  = new OrbitalBody("__pred", referenceBody.Primary, drawEl);
        var verts = MeshFactory.OrbitLine(snap, _scale);
        _predictedOrbitMesh = new Mesh(_gl, verts);
    }
}