using System.Collections;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace TempStatus;

// Here are some basic resources on code style and naming conventions to help
// you in your first CSharp plugin!
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
// https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces

// This BepInAutoPlugin attribute comes from the Hamunii.BepInEx.AutoPlugin
// NuGet package, and it will generate the BepInPlugin attribute for you!
// For more info, see https://github.com/Hamunii/BepInEx.AutoPlugin
[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    private GameObject? canvasObj;
    private Text? statusText;

    private void Awake()
    {
        // BepInEx gives us a logger which we can use to log information.
        // See https://lethal.wiki/dev/fundamentals/logging
        Log = Logger;

        // BepInEx also gives us a config file for easy configuration.
        // See https://lethal.wiki/dev/intermediate/custom-configs

        // We can apply our hooks here.
        // See https://lethal.wiki/dev/fundamentals/patching-code

        // Log our awake here so we can see it in LogOutput.log file
        Log.LogInfo($"Plugin {Name} is loaded!");
        
        // Start coroutine to create test UI after game initializes
        StartCoroutine(CreateTestUI());
    }
    
    private IEnumerator CreateTestUI()
    {
        // Wait for game to initialize - wait for a frame to ensure Unity is ready
        yield return null;
        yield return new WaitForSeconds(0.5f);
        
        // Create canvas GameObject
        canvasObj = new GameObject("TempStatusCanvas");
        Object.DontDestroyOnLoad(canvasObj);
        
        // Add Canvas component
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Ensure it's on top
        
        // Add CanvasScaler for proper scaling
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        // Add GraphicRaycaster
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create text GameObject as child of canvas
        var textObj = new GameObject("TempStatusText");
        textObj.transform.SetParent(canvasObj.transform, false);
        
        // Add Text component
        statusText = textObj.AddComponent<Text>();
        statusText.text = "TempStatus Test UI";
        statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (statusText.font == null)
        {
            // Fallback to any available font
            statusText.font = Resources.FindObjectsOfTypeAll<Font>()[0];
        }
        statusText.fontSize = 24;
        statusText.color = Color.white;
        statusText.alignment = TextAnchor.UpperLeft;
        
        // Setup RectTransform for positioning
        var rectTransform = textObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(10, -10);
        rectTransform.sizeDelta = new Vector2(400, 50);
        
        Log.LogInfo("Test UI created!");
    }
    
    private void OnDestroy()
    {
        // Cleanup when plugin is unloaded
        if (canvasObj != null)
        {
            Object.Destroy(canvasObj);
        }
    }
}
