using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DevTools.Testing;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace DevTools;

public class ScreenshotCamera : GameComponent
{
    private Camera _camera;
    private RenderTexture _texture;
    private Texture2D _baked;
    private RenderTexture _active;

    private IReadOnlyList<Thing> _culledList = [];
    private CellRect? _culledCells = [];

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

        try
        {
            var map = Find.CurrentMap;
            _culledCells ??= new CellRect(0, 0, map.Size.x, map.Size.z).ClipInsideMap(Find.CurrentMap);

            _camera.aspect = 1f;
            _camera.orthographicSize =
                (float)Math.Ceiling(Math.Max(_culledCells.Value.Height / 2f + 2, _culledCells.Value.Width / 2f + 2));
            _camera.fieldOfView = Current.Camera.fieldOfView;
            _camera.transform.position = _culledCells.Value.CenterCell.ToVector3() +
                                         new Vector3(0, Current.Camera.transform.position.y, 0);
            _camera.transform.rotation = Current.Camera.transform.rotation;
            _camera.cullingMask = Current.Camera.cullingMask;

            var maxCell = _culledCells == null
                ? Math.Max(map.Size.z, map.Size.x)
                : Math.Max(_culledCells.Value.Size.z, _culledCells.Value.Size.x);

            int size = maxCell;
            int maxSize = size * 264;
            while (size * 2 <= 16384 && size * 2 <= maxSize)
                size *= 2;

            _texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { name = "ScreenshotTexture" };
            _texture.Create();

            _camera.targetTexture = _texture;
            RenderTexture.active = _texture;
            foreach (Thing t in _culledList ?? Find.CurrentMap.listerThings.AllThings)
            {
                t.DrawNowAt(t.DrawPos);
            }

            _camera.Render();
            _baked = _texture.CreateTexture2D(TextureFormat.RGBA32, mipChain: false);
        }
        finally
        {
            plzTakeScreenshot = false;
        }

    }


    internal static IEnumerator _takeGameScreenshot(IReadOnlyList<Thing> things = null, CellRect? cells = null)
    {
        plzTakeScreenshot = true;
        instance._culledList = things;
        instance._culledCells = cells;
        yield return new WaitUntil(() => !plzTakeScreenshot);
    }

    public static void TakeGameScreenshot(string savePath, IReadOnlyList<Thing> things = null,
        CellRect? cells = null)
    {
        CoroutineObject.Instance.StartCoroutine(_takeGameScreenshot(things, cells));
        var screenshot = instance._baked;
        instance._baked = null;
        var screenshotPng = screenshot.EncodeToPNG();
        File.WriteAllBytes(savePath, screenshotPng);
    }

    public static Texture2D TakeGameScreenshot(IReadOnlyList<Thing> things = null, CellRect? cells = null)
    {
        CoroutineObject.Instance.StartCoroutine(_takeGameScreenshot(things, cells));
        var screenshot = instance._baked;
        instance._baked = null;
        return screenshot;
    }

    public static IEnumerator TakeUIScreenshot(Window window, [CanBeNull] MethodInfo methodToCapture = null)
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
        File.WriteAllBytes("/home/lana/RimWorldDev/Dumps/testUI.png", otherPng);

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