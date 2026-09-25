using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DevTools.Testing;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Verse;

namespace DevTools;

public class ScreenshotCamera : GameComponent
{
    private Camera _camera;
    private RenderTexture _texture;
    private Texture2D _baked;

    private IReadOnlyList<Thing> _culledList = [];
    private CellRect? _culledCells = [];
    private RenderTexture _active;

    private static bool plzTakeScreenshot = false;
    private static ScreenshotCamera instance;

    public ScreenshotCamera(Game game) : base()
    {

    }

    public override void FinalizeInit()
    {
        instance = this;


        _camera = new GameObject("ScreenshotCamera")
            .AddComponent<Camera>();

        _camera.orthographic = true;
        _camera.enabled = false;
        _camera.targetTexture = _texture;
    }

    public override void GameComponentTick()
    {
        if (!plzTakeScreenshot)
            return;

        if (Find.TickManager.TicksGame < 10)
            return;

        var map = Find.CurrentMap;

        _camera.aspect = 1f;
        _camera.orthographicSize = (float)Math.Ceiling(Math.Max(_culledCells.Value.Height / 2f + 2, _culledCells.Value.Width / 2f + 2));
        _camera.fieldOfView = Current.Camera.fieldOfView;
        _camera.transform.position = _culledCells.Value.CenterCell.ToVector3() + new Vector3(0, Current.Camera.transform.position.y, 0);
        _camera.transform.rotation = Current.Camera.transform.rotation;
        _camera.cullingMask = Current.Camera.cullingMask;

        var maxCell = _culledCells == null ? Math.Max(map.Size.z,  map.Size.x) : Math.Max(_culledCells.Value.Size.z, _culledCells.Value.Size.x);

        var width = maxCell * 256;
        var height = maxCell * 256;

        _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "ScreenshotTexture"
        };
        _texture.Create();

        _camera.targetTexture = _texture;
        RenderTexture.active = _texture;
        foreach (Thing t in Find.CurrentMap.listerThings.AllThings)
        {
            t.DrawNowAt(t.DrawPos);
        } _camera.Render();
        _baked = _texture.CreateTexture2D(TextureFormat.RGBA32, mipChain: false);

        plzTakeScreenshot = false;
    }


    public static IEnumerator TakeScreenshot(string path, IReadOnlyList<Thing> things = null, CellRect? cells = null)
    {
        plzTakeScreenshot = true;
        instance._culledList = things;
        instance._culledCells = cells;
        yield return new WaitUntil(() => !plzTakeScreenshot);
        RenderTexture.active = instance._texture;

        var texture = new Texture2D(
            instance._texture.width,
            instance._texture.height,
            TextureFormat.RGBA32,
            false
        );

        texture.ReadPixels(
            new Rect(0, 0, instance._texture.width, instance._texture.height),
            0,
            0
        );

        texture.Apply();

        var png = texture.EncodeToPNG();
        File.WriteAllBytes(path, png);
    }

    public static IEnumerator TakeUIScreenshot(string path, Window window, [CanBeNull] MethodInfo methodToCapture = null)
    {
        instance._texture =
            new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        instance._texture.Create();
        using var capture = new ScopedMethodHook(methodToCapture ?? AccessTools.Method(window.GetType(), nameof(Window.DoWindowContents)),
            prefix: new HarmonyMethod(CapturePrefix), postfix: new HarmonyMethod(CapturePostfix));

        yield return new WaitForEndOfFrame();

        var rect = window.windowRect;
        var baked = instance._texture.CreateTexture2D(TextureFormat.RGBA32, mipChain: false);

        var cropped = new Texture2D((int)window.windowRect.width, (int)window.windowRect.height, TextureFormat.RGBA32, mipChain: false);
        cropped.SetPixels(baked.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
        cropped.Apply();

        var otherPng = cropped.EncodeToPNG();
        File.WriteAllBytes(path, otherPng);

    }

    internal static void CapturePrefix()
    {
        instance._active = RenderTexture.active;
        RenderTexture.active = instance._texture;
    }

    internal static void CapturePostfix()
    {
        RenderTexture.active = instance._active;
    }
}