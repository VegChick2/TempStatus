using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Peak.Afflictions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static CharacterAfflictions;

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

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} is loaded!");
        
        // Make sure the plugin GameObject persists across scene changes
        Object.DontDestroyOnLoad(gameObject);
        
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
        
        // Find player CharacterAfflictions (needed to read status values)
        var playerAfflictions = Object.FindFirstObjectByType<CharacterAfflictions>();
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
        
        // Add GraphicRaycaster
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create text GameObject as child of canvas
        var textObj = new GameObject("TempStatusText");
        textObj.transform.SetParent(canvasObj.transform, false);
        
        // Add Text component
        statusText = textObj.AddComponent<Text>();
        statusText.text = GetDisplayTextWithTimestamp("Loading...");
        
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
                    afflictionList.Add($"{baseAfflictionName} | N/A | N/A | N/A | N/A");
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
    
    private void OnDestroy()
    {
        // Unsubscribe from scene events
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // Cleanup when plugin is unloaded
        if (canvasObj != null)
        {
            Object.Destroy(canvasObj);
        }
    }
}
