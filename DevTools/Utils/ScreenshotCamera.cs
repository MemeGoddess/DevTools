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
using Object = UnityEngine.Object;

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


    public static IEnumerator TakeScreenshot(string path, IReadOnlyList<Thing> things = null, CellRect? cells = null, Action<Texture2D, Texture2D> validator = null)
    {
        validator ??= ValidateTextures;
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

    public static IEnumerator TakeUIScreenshot(string path, Window window, [CanBeNull] MethodInfo methodToCapture = null, Action<Texture2D, Texture2D> validator = null)
    {
        validator ??= ValidateTextures;
        instance._texture =
            new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        instance._texture.Create();
        using var capture = new ScopedMethodHook(methodToCapture ?? AccessTools.Method(window.GetType(), nameof(Window.DoWindowContents)),
            prefix: new HarmonyMethod(CapturePrefix), postfix: new HarmonyMethod(CapturePostfix));

        yield return new WaitForEndOfFrame();

        var rect = window.windowRect;
        var baked = instance._texture.CreateTexture2D(TextureFormat.RGBA32, mipChain: false);

        var currentTex = new Texture2D((int)window.windowRect.width, (int)window.windowRect.height, TextureFormat.RGBA32, mipChain: false);
        currentTex.SetPixels(baked.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
        currentTex.Apply();

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, currentTex.EncodeToPNG());
            if(TestRunner.Active)
                Test.Fail("Comparison image didn't exist");
            yield break;
        }

        var originalBytes = File.ReadAllBytes(path);
        var originalTex = new Texture2D(1, 1);
        originalTex.LoadImage(originalBytes);

        validator(originalTex, currentTex);
        ITestGroup current = Test.Current;
        if (current.Status == Status.Failed)
        {
            var newPath = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-fail" + Path.GetExtension(path));
            File.WriteAllBytes(newPath, currentTex.EncodeToPNG());
        }
        Object.Destroy(originalTex);
        Object.Destroy(currentTex);
    }

    public static void ValidateTextures(Texture2D original, Texture2D current)
    {
        Expect.AreEqual(original.texelSize, current.texelSize, "Sizes are different");

        float difference = 0;
        float usedPixels = 0;

        int smallestX = original.width, smallestY = original.height, largestX = -1, largestY = -1;

        var originalPixels = original.GetPixels();
        var currentPixels = current.GetPixels();
        var comparisonPixels = new Color[originalPixels.Length];

        var width = original.width;
        for(int i = 0; i < originalPixels.Length; i++)
        {
            var originalPixel = originalPixels[i];
            var currentPixel =  currentPixels[i];

            if (originalPixel.maxColorComponent > 0 || currentPixel.maxColorComponent > 0)
            {
                usedPixels++;
                int x = i % width;
                int y = i / width;
                smallestX = Math.Min(smallestX, x );
                smallestY = Math.Min(smallestY, y);
                largestX = Math.Max(largestX, x);
                largestY = Math.Max(largestY, y);
            }

            if (originalPixel != currentPixel)
                difference++;

            comparisonPixels[i] = ComparePixel(originalPixel, currentPixel);
        }
        Expect.AreEqual(difference, 0, $"Image is {Math.Round(difference / usedPixels * 100, 2)}% different ({difference}/{usedPixels})");

        var comparison = new Texture2D(original.width, original.height, TextureFormat.RGBA32, mipChain: false);
        comparison.SetPixels(comparisonPixels);
        comparison.Apply();

        var rect =  new Rect(
            Math.Max(smallestX - 20, 0),
            Math.Max(smallestY - 20, 0),
            Math.Min(largestX + 40 - smallestX, current.width - smallestX),
            Math.Min(largestY + 40 - smallestY, current.height - smallestY));

        var artefact = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, mipChain: false);
        artefact.SetPixels(comparison.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
        artefact.Apply();
        Object.Destroy(comparison);
        Test.Artefact(artefact);
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

    private static Color ComparePixel(Color c1, Color c2)
    {
        // Missing in Current
        if (c1.maxColorComponent > 0 && c2.maxColorComponent == 0)
            return Color.Lerp(c1, Color.red, 0.2f);

        // Added in Current
        if (c2.maxColorComponent > 0 && c1.maxColorComponent == 0)
            return Color.Lerp(c2, Color.green, 0.2f);

        // Different in both
        if (c1 != c2)
            return Color.Lerp(c2, Color.yellow, 0.2f);

        // Equal
        return c1;
    }

}