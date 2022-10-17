using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP;
using System.Linq;
using System.IO;
using KSP.UI.Screens;

[KSPAddon(KSPAddon.Startup.Flight, false)]
partial class ContinuousCollision : MonoBehaviour
{
    #region Fields

    private Coroutine autoCoroutine;
    internal static List<Vessel> loadedVessels;
    private static bool autoswitch = true;
    private static bool autoEnable = true;
    private static bool speculativeCollision = false;
    private static bool applauncherButton = true;

    public static CollisionDetectionMode continuousCollisionMode = CollisionDetectionMode.ContinuousDynamic;
    public static List<PartCategories> categories = new List<PartCategories> { 
        PartCategories.Structural, 
        PartCategories.Aero, 
        PartCategories.Control
    };

    // Internal GUI logic variables.
    private Rect windowRect = new Rect(Screen.width * 0.75f, Screen.height / 2, 0, 0 );
    private Vector2 scrollPositionAllVessels;
    private bool showAutoStats = false;
    private GUIStyle boxStyle;

    // Toolbar.
    public static bool addedAppLauncherButton = false;
    public static bool guiEnabled = false;
    internal static bool guiHidden = false;

    // Config variables.
    string kspRoot;
    string pluginDataPath;
    string configPath;

    // Perhaps link up to a calculation of each vessels' width; for now,
    // a very large size of 50 is assumed as creating a vessel larger than
    // that is difficult.
    public float vesselWidth = 50;

    #endregion

    #region Main

    private void Start()
    {
        kspRoot = KSPUtil.ApplicationRootPath;
        pluginDataPath = Path.Combine(kspRoot, "GameData", "ContinuousCollisions", "PluginData");
        configPath = Path.Combine(pluginDataPath, "settings.cfg");
        LoadSettings();

        GameEvents.onHideUI.Add(OnHideUI);
        GameEvents.onShowUI.Add(OnShowUI);

        AddToolbarButton();

        SetSpeculative(speculativeCollision);
        SetUpdateAuto(autoEnable);
    }

    private protected void SetAllRigidBodies(CollisionDetectionMode collisionDetectionMode)
    {
        Rigidbody[] rigidbodies = GameObject.FindObjectsOfType<Rigidbody>();
        int rbLen = rigidbodies.Length;
        for (int i = 0; i < rbLen; ++i)
        {
            rigidbodies[i].collisionDetectionMode = collisionDetectionMode;
        }
        Debug.Log($"[ContinuousCollision]: Collision mode for all rigidbodies set to {collisionDetectionMode}.");
    }

    private void SetSpeculative(bool enable)
    {
        speculativeCollision = enable;
        continuousCollisionMode = enable ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
        SetAllRigidBodies(CollisionDetectionMode.Discrete);
    }

    private void SetUpdateAuto(bool enable)
    {
        if (enable)
        {
            Debug.Log("[ContinuousCollision]: Started automatic updates.");
            autoCoroutine = StartCoroutine(UpdateAuto());
        }
        else if (autoCoroutine != null)
        {
            Debug.Log("[ContinuousCollision]: Stopped automatic updates.");
            StopCoroutine(autoCoroutine);

            SetAllRigidBodies(CollisionDetectionMode.Discrete);
        }
    }

    public IEnumerator UpdateAuto()
    {
        Vector3 vslVel;
        float relVelMag;
        bool desireContinuous;
        ITargetable tgt;

        while (true)
        {
            loadedVessels = FlightGlobals.VesselsLoaded;

            foreach (Vessel vsl in loadedVessels)
            {
                //if (vsl.packed && !IsVesselContinuous(vsl)) continue;

                vslVel = vsl.GetObtVelocity();
                desireContinuous = false;

                foreach (Vessel otherVsl in loadedVessels)
                {
                    if (otherVsl.persistentId == vsl.persistentId) continue;

                    relVelMag = Mathf.Abs((otherVsl.GetObtVelocity() - vslVel).magnitude) + (float)otherVsl.acceleration.magnitude;

                    if (relVelMag > 100 && Vector3d.Distance(vsl.GetTransform().position, otherVsl.GetTransform().position) - vesselWidth <= relVelMag)
                    {
                        tgt = null;
                        tgt = vsl.targetObject;
                        if (tgt != null)
                        {
                            if (autoswitch 
                                && otherVsl.GetTotalMass() > vsl.GetTotalMass() 
                                && otherVsl.persistentId == tgt.GetVessel().persistentId
                                && vsl.persistentId == FlightGlobals.ActiveVessel.persistentId )
                            {
                                FlightGlobals.ForceSetActiveVessel(otherVsl);
                            }
                        }

                        desireContinuous = true;

                        break;
                    }
                }

                SetVesselCollisionContinuous(vsl, desireContinuous);
            }

            yield return new WaitForSeconds(1);
        }
    }

    private void SetVesselCollisionContinuous(Vessel vessel, bool desireContinuous)
    {
        bool currentlyContinuous = IsVesselContinuous(vessel);

        if (desireContinuous)
        {
            if (!currentlyContinuous) SetVesselCollisionMode(vessel, continuousCollisionMode);
        }
        else
        {
            if (currentlyContinuous) SetVesselCollisionMode(vessel, CollisionDetectionMode.Discrete);
        }
    }

    private bool IsVesselContinuous(Vessel vessel)
    {
        var rb = vessel.Parts[0].Rigidbody;
        if (rb == null) return false;

        return vessel.Parts[0].Rigidbody.collisionDetectionMode == continuousCollisionMode;
    }

    private void SetVesselCollisionMode(Vessel vessel, CollisionDetectionMode collisionMode)
    {
        foreach (Part part in vessel.Parts)
        {
            if (!categories.Contains(part.partInfo.category))
                continue;

            part.Rigidbody.collisionDetectionMode = collisionMode;
        }

        //Debug.Log($"[ContinuousCollision]: Set the collision mode of {vessel.GetDisplayName()} to {collisionMode}.");
    }

    #endregion

    #region GUI

    private void OnGUI()
    {
        if (guiEnabled && !guiHidden)
            DrawGUI();
    }

    public void DrawGUI() =>
        windowRect = GUILayout.Window(GUIUtility.GetControlID(FocusType.Passive), windowRect, FillWindow, "Continuous Collisions", GUILayout.Height(1), GUILayout.Width(200));

    private void FillWindow(int windowID)
    {
        if (guiHidden) return;

        if (GUI.Button(new Rect(windowRect.width - 18, 2, 16, 16), ""))
            ToggleGui();

        if (boxStyle == null)
            boxStyle = GUI.skin.GetStyle("Box");

        // Warning.

        GUILayout.BeginVertical(boxStyle);
            GUILayout.Label("This mod is experimental, it may break your game, destroy your craft or just not work.");
        GUILayout.EndVertical();

        // Settings.

        GUILayout.BeginVertical();

        bool auto = GUILayout.Toggle(autoEnable, "Enabled");
        if (auto != autoEnable)
        {
            autoEnable = auto;
            SetUpdateAuto(autoEnable);
        }

        bool spec = GUILayout.Toggle(speculativeCollision, "Use Speculative Collision");
        if (spec != speculativeCollision)
        {
            speculativeCollision = spec;
            SetSpeculative(speculativeCollision);
        }

        if (speculativeCollision)
        {
            GUILayout.Label("Speculative collision is more consistent, slower, and produces more damage, but sometimes causes phantom collisions.");
        }

        autoswitch = GUILayout.Toggle(autoswitch, "Switch to Target");
        if (!speculativeCollision && !autoswitch)
        {
            GUILayout.Label("In order to prevent instability, Switch to Target must be enabled when Speculative Collision is disabled.");
        }

        GUILayout.EndVertical();
        
        // Auto stats.

        if (autoEnable)
        {
            if (showAutoStats = GUILayout.Toggle(showAutoStats, "Show Stats"))
            {
                GUILayout.BeginVertical(boxStyle);
                scrollPositionAllVessels = GUILayout.BeginScrollView(scrollPositionAllVessels, GUILayout.Height(400));

                foreach (Vessel vessel in loadedVessels)
                {
                    int continuousParts = vessel.Parts.Count(p => p.Rigidbody.collisionDetectionMode == continuousCollisionMode);
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

    public void AddToolbarButton()
    {
        if (!addedAppLauncherButton && applauncherButton && HighLogic.LoadedSceneIsFlight)
        {
            Texture buttonTexture = GameDatabase.Instance.GetTexture("ContinuousCollisions/Textures/icon", false);
            ApplicationLauncher.Instance.AddModApplication(ToggleGui, ToggleGui, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
            addedAppLauncherButton = true;
        }
    }

    void ToggleGui()
    {
        if (guiEnabled)
            DisableGui();
        else
            EnableGui();
    }

    void EnableGui() =>
        guiEnabled = true;

    void DisableGui()
    {
        guiEnabled = false;
        SaveSettings();
    }

    private void OnShowUI() =>
        OnToggleUI(false);

    private void OnHideUI() =>
        OnToggleUI(true);

    private void OnToggleUI(bool hide) =>
        guiHidden = hide;

    #endregion

    #region Config

    void SaveSettings()
    {
        if (!Directory.Exists(pluginDataPath))
            Directory.CreateDirectory(pluginDataPath);

        ConfigNode settingsFile = new ConfigNode();
        ConfigNode settingsNode = new ConfigNode("ContinuousCollisionsSettings");
        settingsFile.AddNode(settingsNode);
        settingsNode.SetValue("enabled", autoEnable, true);
        settingsNode.SetValue("speculativeCollision", speculativeCollision, true);
        settingsNode.SetValue("useAutoSwitch", autoswitch, true);
        settingsNode.SetValue("applauncherButton", applauncherButton, true);
        settingsFile.Save(configPath);
    }

    void LoadSettings()
    {
        if (!File.Exists(configPath)) return;

        ConfigNode settingsFile = ConfigNode.Load(configPath);
        ConfigNode settingsNode = settingsFile.GetNode("ContinuousCollisionsSettings");

        settingsNode.TryGetValue("enabled", ref autoEnable);
        settingsNode.TryGetValue("speculativeCollision", ref speculativeCollision);
        settingsNode.TryGetValue("useAutoSwitch", ref autoswitch);
        settingsNode.TryGetValue("applauncherButton", ref applauncherButton);
    }

    #endregion
}