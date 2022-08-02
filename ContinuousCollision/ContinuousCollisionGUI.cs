using UnityEngine;
using System.Collections;
using KSP.UI.Screens;
using System.Linq;
using System.IO;

internal class ContinuousCollisionGUI : MonoBehaviour
{
    // Internal GUI logic variables.
    private Rect windowRect = new Rect(Screen.width * 0.75f, Screen.height / 2, 0, 0 );
    private Vector2 scrollPositionAllVessels;
    private bool showAutoStats = false;

    // Public read only variables
    public bool enableCC = true;
    public bool useSpeculative = true;

    // Toolbar.
    public static bool addedAppLauncherButton = false;
    public static bool guiEnabled = false;

    // Config variables.
    string kspRoot;
    string pluginDataPath;
    string configPath;

    void Start()
    {
        kspRoot = KSPUtil.ApplicationRootPath;
        pluginDataPath = Path.Combine(kspRoot, "GameData", "ContinuousCollisions", "PluginData");
        configPath = Path.Combine(pluginDataPath, "settings.cfg");
        LoadSettings();
    }
    
    public void DrawGUI() =>
        windowRect = GUILayout.Window(GUIUtility.GetControlID(FocusType.Passive), windowRect, FillWindow, "Continuous Collision", GUILayout.Height(1), GUILayout.Width(200));

    private void FillWindow(int windowID)
    {
        if (GUI.Button(new Rect(windowRect.width - 18, 2, 16, 16), ""))
            ToggleGui();

        GUIStyle boxStyle = GUI.skin.GetStyle("Box");

        GUILayout.BeginVertical(boxStyle);
            GUILayout.Label("This mod is experimental, it may break your game, destroy your craft or just not work.");
        GUILayout.EndVertical();

        GUILayout.BeginVertical();

        enableCC = GUILayout.Toggle(enableCC, "Enabled");
        useSpeculative = GUILayout.Toggle(useSpeculative, "Use Speculative Collision");
        if (useSpeculative)
        {
            GUILayout.Label("Speculative collision is slower, less buggy, and produces slightly more damage.");
        }
        else
        {
            GUILayout.Label("With speculative collision disabled you must switch away from projectiles before a collision.");
        } 

        GUILayout.EndVertical();
        
        if (enableCC)
        {
            if (showAutoStats = GUILayout.Toggle(showAutoStats, "Show Stats"))
            {
                GUILayout.BeginVertical(boxStyle);
                scrollPositionAllVessels = GUILayout.BeginScrollView(scrollPositionAllVessels, GUILayout.Height(400));

                foreach (Vessel vessel in ContinuousCollision.loadedVessels)
                {
                    int continuousParts = vessel.Parts.Count(p => p.Rigidbody.collisionDetectionMode == ContinuousCollision.continuousCollisionMode);
                    if (continuousParts > 0)
                    {
                        GUI.contentColor = Color.red;
                    }
                    if (vessel.packed)
                    {
                        GUI.contentColor = Color.grey;
                    }

                    GUILayout.BeginVertical(boxStyle);
                        GUILayout.Label(vessel.GetDisplayName());
                        GUILayout.Label($"Continuous Rigidbodies: {continuousParts.ToString()}");
                        GUILayout.Label($"Unpacked: {!vessel.packed}");
                    GUILayout.EndVertical();
                    GUI.contentColor = Color.white;
                }

                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 500));
    }

    void SaveSettings()
    {
        if (!Directory.Exists(pluginDataPath))
            Directory.CreateDirectory(pluginDataPath);

        ConfigNode settingsFile = new ConfigNode();
        ConfigNode settingsNode = new ConfigNode("ContinuousCollisionsSettings");
        settingsFile.AddNode(settingsNode);
        settingsNode.SetValue("enabled", enableCC, true);
        settingsNode.SetValue("useSpeculative", useSpeculative, true);
        settingsFile.Save(configPath);
    }

    void LoadSettings()
    {
        if (!File.Exists(configPath)) return;

        ConfigNode settingsFile = ConfigNode.Load(configPath);
        ConfigNode settingsNode = settingsFile.GetNode("ContinuousCollisionsSettings");
        settingsNode.TryGetValue("enabled", ref enableCC);
        settingsNode.TryGetValue("useSpeculative", ref useSpeculative);
    }

    #region Toolbar Config.
    public void AddToolbarButton()
    {
        if (!addedAppLauncherButton)
        {
            Texture buttonTexture = GameDatabase.Instance.GetTexture("ContinuousCollisions/Textures/icon", false);
            ApplicationLauncher.Instance.AddModApplication(ToggleGui, ToggleGui, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
            addedAppLauncherButton = true;
        }
    }

    void ToggleGui()
    {
        if (guiEnabled) DisableGui();
        else EnableGui();
    }
    void EnableGui() => guiEnabled = true;
    void DisableGui()
    {
        guiEnabled = false;
        SaveSettings();
    }

    #endregion
}

