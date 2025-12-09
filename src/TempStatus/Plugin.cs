using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Peak.Afflictions;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    private float updateTimer = 0f;
    private const float updateInterval = 0.1f; // Update 10 times per second
    
    // Configuration for fog distance visibility offset
    private ConfigEntry<float>? distanceVisibilityOffsetConfig;
    internal static float DistanceVisibilityOffset { get; private set; } = 30f;
    
    // Configuration for fog tint color
    private ConfigEntry<float>? fogTintColorRConfig;
    private ConfigEntry<float>? fogTintColorGConfig;
    private ConfigEntry<float>? fogTintColorBConfig;
    private ConfigEntry<float>? fogTintColorAConfig;
    internal static Vector4 FogTintColor { get; private set; } = new Vector4(0.72067463f, 0.7391309f, 0.745283f, 0f);
    
    private Harmony? harmony;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} is loaded!");
        
        // Initialize configuration
        distanceVisibilityOffsetConfig = Config.Bind(
            "FogSettings",
            "DistanceVisibilityOffset",
            30f,
            "Controls the _DistanceVisibilityOffset material property for fog spheres. Default: 30"
        );
        DistanceVisibilityOffset = distanceVisibilityOffsetConfig.Value;
        
        // Subscribe to config change events
        distanceVisibilityOffsetConfig.SettingChanged += (sender, args) =>
        {
            DistanceVisibilityOffset = distanceVisibilityOffsetConfig.Value;
            Log.LogInfo($"DistanceVisibilityOffset updated to: {DistanceVisibilityOffset}");
        };
        
        // Initialize fog tint color configuration
        fogTintColorRConfig = Config.Bind(
            "FogSettings",
            "FogTintColorR",
            0.72067463f,
            "Red component of _FogtintColor material property. Default: 0.72067463"
        );
        fogTintColorGConfig = Config.Bind(
            "FogSettings",
            "FogTintColorG",
            0.7391309f,
            "Green component of _FogtintColor material property. Default: 0.7391309"
        );
        fogTintColorBConfig = Config.Bind(
            "FogSettings",
            "FogTintColorB",
            0.745283f,
            "Blue component of _FogtintColor material property. Default: 0.745283"
        );
        fogTintColorAConfig = Config.Bind(
            "FogSettings",
            "FogTintColorA",
            0f,
            "Alpha component of _FogtintColor material property. Default: 0"
        );
        
        // Update fog tint color from config
        UpdateFogTintColor();
        
        // Subscribe to fog tint color config change events
        fogTintColorRConfig.SettingChanged += (sender, args) => UpdateFogTintColor();
        fogTintColorGConfig.SettingChanged += (sender, args) => UpdateFogTintColor();
        fogTintColorBConfig.SettingChanged += (sender, args) => UpdateFogTintColor();
        fogTintColorAConfig.SettingChanged += (sender, args) => UpdateFogTintColor();
        
        // Apply Harmony patches
        harmony = new Harmony(Info.Metadata.GUID);
        harmony.PatchAll();
        Log.LogInfo("Harmony patches applied!");
        
        // Make sure the plugin GameObject persists across scene changes
        //Object.DontDestroyOnLoad(gameObject);
        
        // Subscribe to scene loaded events
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void Update()
    {
        // Check if UI exists
        if (statusText == null || canvasObj == null)
        {

            // Try to find existing canvas
            var existingCanvas = GameObject.Find("TempStatusCanvas");
            if (existingCanvas != null)
            {
                canvasObj = existingCanvas;
                var foundTextObj = canvasObj.transform.Find("TempStatusText");
                if (foundTextObj != null)
                {
                    statusText = foundTextObj.GetComponent<Text>();
                }
            }
            
            // If still null, return early
            if (statusText == null || canvasObj == null)
            {
                return;
            }
        }
        
        // Throttle updates to 10 times per second using deltaTime
        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval)
        {
            return;
        }
        updateTimer = 0f;
        
        // Get player CharacterAfflictions from static localCharacter (optimized)
        var playerAfflictions = Character.localCharacter?.refs?.afflictions;
        if (playerAfflictions == null)
        {
            if (statusText != null)
            {
                statusText.text = GetDisplayTextWithTimestamp("Waiting for player...");
            }
            return;
        }
        
        // Create UI if it doesn't exist
        if (canvasObj == null || statusText == null)
        {
            CreateUI();
        }
        
        // Read afflictions
        List<string> afflictionInfo;
        try
        {
            afflictionInfo = ReadAfflictions(playerAfflictions);
        }
        catch (System.Exception ex)
        {
            Log.LogError($"Exception in ReadAfflictions: {ex}");
            afflictionInfo = new List<string>();
        }
        
        // Update UI (always update to show current timestamp)
        if (statusText != null)
        {
            string newText;
            if (afflictionInfo.Count == 0)
            {
                newText = GetDisplayTextWithTimestamp("No active afflictions");
            }
            else
            {
                // Add header for time columns
                string header = "Affliction | Remaining | Total | Elapsed | Bonus";
                string separator = new string('-', header.Length);
                string afflictionText = string.Join("\n", afflictionInfo);
                newText = GetDisplayTextWithTimestamp($"{header}\n{separator}\n{afflictionText}");
            }
            
            statusText.text = newText;
        }
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only create UI for game scenes (not menu/airport scenes)
        if (scene.name == "Title" || scene.name == "Airport" || scene.name == "Pretitle")
        {
            // Clean up UI if it exists
            if (canvasObj != null)
            {
                Object.Destroy(canvasObj);
                canvasObj = null;
                statusText = null;
            }
            return;
        }
        
        // Create UI for game scenes (player will be found in Update())
        CreateUI();
    }
    
    private void CreateUI()
    {
        // Check if UI already exists
        if (GameObject.Find("TempStatusCanvas") != null)
        {
            // Update references if they're null
            if (canvasObj == null)
            {
                canvasObj = GameObject.Find("TempStatusCanvas");
            }
            if (statusText == null && canvasObj != null)
            {
                var foundTextObj = canvasObj.transform.Find("TempStatusText");
                if (foundTextObj != null)
                {
                    statusText = foundTextObj.GetComponent<Text>();
                }
            }
            return;
        }
        
        // Create canvas GameObject
        canvasObj = new GameObject("TempStatusCanvas");
        
        // Add Canvas component
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Ensure it's on top
        
        // Add CanvasScaler for proper scaling
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        // Don't add GraphicRaycaster - we don't need interaction, just display
        // This prevents blocking other UI elements
        
        // Create text GameObject as child of canvas
        var textObj = new GameObject("TempStatusText");
        textObj.transform.SetParent(canvasObj.transform, false);
        
        // Add Text component
        statusText = textObj.AddComponent<Text>();
        statusText.text = GetDisplayTextWithTimestamp("Loading...");
        // Disable raycast target so clicks pass through to other UI elements
        statusText.raycastTarget = false;
        
        // Try to get a monospace font for proper alignment
        // Unity's built-in fonts: Arial (proportional), CourierNew (monospace)
        // Try Courier New first (monospace) for proper column alignment
        statusText.font = Resources.GetBuiltinResource<Font>("CourierNew.ttf");
        if (statusText.font == null)
        {
            // Try LegacyRuntime (monospace)
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        if (statusText.font == null)
        {
            // Fallback to Arial (proportional - alignment may not be perfect)
            statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (statusText.font == null)
        {
            // Last resort: any available font
            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            if (fonts.Length > 0)
            {
                statusText.font = fonts[0];
            }
        }
        
        // Use monospace font settings if available
        // For TextMeshPro or other advanced text, we'd set fontAsset, but for basic Text we rely on font choice
        
        statusText.fontSize = 12; // Smaller font to fit more lines
        statusText.color = Color.white;
        statusText.alignment = TextAnchor.UpperLeft;
        // Allow text to overflow so all lines are visible
        statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
        statusText.verticalOverflow = VerticalWrapMode.Overflow;
        
        // Setup RectTransform for positioning
        var rectTransform = textObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(10, -10);
        // Increase size to accommodate all afflictions (19 types + header + separator + timestamp)
        // With font size 12, each line is ~14px, so 25 lines = 350px, but make it bigger for safety
        rectTransform.sizeDelta = new Vector2(700, 800);
        
        Log.LogInfo("UI created!");
    }
    
    private string GetDisplayTextWithTimestamp(string text)
    {
        var timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
        return $"[{timestamp}]\n{text}";
    }
    
    private List<string> ReadAfflictions(CharacterAfflictions afflictions)
    {
        var afflictionList = new List<string>();
        
        Log.LogInfo("ReadAfflictions: Starting");
        
        if (afflictions == null)
        {
            Log.LogWarning("ReadAfflictions: afflictions is null");
            return afflictionList;
        }
        
        // Get all AfflictionType enum values
        var allAfflictionTypes = System.Enum.GetValues(typeof(Affliction.AfflictionType)).Cast<Affliction.AfflictionType>();
        Log.LogInfo($"ReadAfflictions: Found {allAfflictionTypes.Count()} affliction types in enum");
        
        // Create a dictionary to map affliction types to lists of their instances (multiple can exist)
        var activeAfflictionsByType = new Dictionary<Affliction.AfflictionType, List<Affliction>>();
        
        try
        {
            if (afflictions.afflictionList != null)
            {
                Log.LogInfo($"ReadAfflictions: Found {afflictions.afflictionList.Count} afflictions in list");
                
                foreach (var affliction in afflictions.afflictionList)
                {
                    if (affliction == null)
                    {
                        continue;
                    }
                    
                    try
                    {
                        var afflictionType = affliction.GetAfflictionType();
                        if (!activeAfflictionsByType.ContainsKey(afflictionType))
                        {
                            activeAfflictionsByType[afflictionType] = new List<Affliction>();
                        }
                        activeAfflictionsByType[afflictionType].Add(affliction);
                    }
                    catch (System.Exception ex)
                    {
                        Log.LogWarning($"ReadAfflictions: Error getting affliction type: {ex.Message}");
                    }
                }
            }
            else
            {
                Log.LogInfo("ReadAfflictions: afflictionList is null");
            }
        }
        catch (System.Exception ex)
        {
            Log.LogWarning($"ReadAfflictions: Error accessing afflictionList: {ex.Message}");
            Log.LogWarning($"ReadAfflictions: Stack trace: {ex.StackTrace}");
        }
        
        // List all affliction types, showing time data if present
        foreach (var afflictionType in allAfflictionTypes)
        {
            try
            {
                string baseAfflictionName = FormatAfflictionName(afflictionType.ToString()).TrimEnd();
                
                if (activeAfflictionsByType.TryGetValue(afflictionType, out var afflictionsOfType) && afflictionsOfType.Count > 0)
                {
                    // One or more afflictions of this type exist - show each with index
                    for (int i = 0; i < afflictionsOfType.Count; i++)
                    {
                        var affliction = afflictionsOfType[i];
                        
                        // Build affliction name with index, always show index for consistency
                        string afflictionNameWithIndex = $"{baseAfflictionName} #{i + 1}";
                        
                        // Get all time data from affliction
                        float totalTime = affliction.totalTime;
                        float timeElapsed = affliction.timeElapsed;
                        float bonusTime = affliction.bonusTime;
                        float remainingTime = totalTime - timeElapsed;
                        
                        // Format time values
                        afflictionList.Add($"{afflictionNameWithIndex} | {remainingTime:F1}s | {totalTime:F1}s | {timeElapsed:F1}s | {bonusTime:F1}s");
                        
                        Log.LogInfo($"ReadAfflictions: {afflictionNameWithIndex.TrimEnd()} - Remaining={remainingTime:F1}s, Total={totalTime:F1}s, Elapsed={timeElapsed:F1}s, Bonus={bonusTime:F1}s");
                    }
                }
                else
                {
                    // Affliction is not active - show as absent
                    // afflictionList.Add($"{baseAfflictionName} | N/A | N/A | N/A | N/A");
                }
            }
            catch (System.Exception ex)
            {
                Log.LogWarning($"ReadAfflictions: Error processing affliction type {afflictionType}: {ex.Message}");
            }
        }
        
        Log.LogInfo($"ReadAfflictions: Completed - Listed {afflictionList.Count} affliction types");
        return afflictionList;
    }
    
    private string FormatAfflictionName(string afflictionType)
    {
        // Format affliction type names to be more readable
        // e.g., "InfiniteStamina" -> "Infinite Stamina"
        return System.Text.RegularExpressions.Regex.Replace(afflictionType, "([a-z])([A-Z])", "$1 $2");
    }
    
    private void UpdateFogTintColor()
    {
        FogTintColor = new Vector4(
            fogTintColorRConfig?.Value ?? 0.72067463f,
            fogTintColorGConfig?.Value ?? 0.7391309f,
            fogTintColorBConfig?.Value ?? 0.745283f,
            fogTintColorAConfig?.Value ?? 0f
        );
        Log.LogInfo($"FogTintColor updated to: R={FogTintColor.x}, G={FogTintColor.y}, B={FogTintColor.z}, A={FogTintColor.w}");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from scene events
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // Unpatch Harmony
        harmony?.UnpatchSelf();
        
        // Cleanup when plugin is unloaded
        if (canvasObj != null)
        {
            Object.Destroy(canvasObj);
        }
    }
}

// Harmony patch to add _DistanceVisibilityOffset control to FogSphere.SetSharderVars()
[HarmonyPatch(typeof(FogSphere), nameof(FogSphere.SetSharderVars))]
public static class FogSphereSetSharderVarsPatch
{
    static void Postfix(FogSphere __instance)
    {
        // Apply custom material properties after the original method sets other properties
        if (__instance.mpb != null && __instance.rend != null)
        {
            __instance.mpb.SetFloat("_DistanceVisibilityOffset", Plugin.DistanceVisibilityOffset);
            __instance.mpb.SetVector("_FogtintColor", Plugin.FogTintColor);
            __instance.rend.SetPropertyBlock(__instance.mpb);
        }
    }
}
