using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;

using KSP.UI.Screens;

namespace ContinuousCollision
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class ContinuousCollision : MonoBehaviour
    {
        #region Fields

        // Automatic checking.

        private Coroutine autoCoroutine;
        public static float updateInterval = 1f;

        public bool Automatic
        {
            get => automatic;
            set
            {
                if (automatic == value)
                    return;

                automatic = value;
                SetUpdateAuto(value);
                SaveSettings();
            }
        }


        // GUI variables.
        private Rect windowRect = new Rect(Screen.width * 0.75f, Screen.height / 2, 0, 0 );
        private Vector2 scrollPositionAllVessels;
        private bool showAutoStats = false;
        private GUIStyle boxStyle;
        private int windowID;
        public static bool addedAppLauncherButton = false;
        public static bool guiEnabled = false;
        private static bool guiHidden = false;


        // Config variables.
        private string pluginDataPath;
        private string configPath;
        private static bool automatic = true;
        private static bool applauncherButton = true;


        // Settings.
        public static float vesselWidth = 50;
        public static float continuousCollisionSpeed = 100; // How fast do vessels need to be closing to trigger continuous collision detection?
        public static CollisionDetectionMode defaultCollisionMode = CollisionDetectionMode.Discrete;
        public static CollisionDetectionMode continuousCollisionMode = CollisionDetectionMode.ContinuousDynamic;
        public static CollisionDetectionMode targetCollisionMode = CollisionDetectionMode.Continuous;
        public static HashSet<PartCategories> partCategories = new HashSet<PartCategories> {
            PartCategories.Structural,
            PartCategories.Aero,
            PartCategories.Control,
            PartCategories.FuelTank
        };

        #endregion

        #region Main

        internal void Start()
        {
            pluginDataPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "ContinuousCollisions", "PluginData");
            configPath = Path.Combine(pluginDataPath, "settings.cfg");
            LoadSettings();

            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
            windowID = GUIUtility.GetControlID(FocusType.Passive);
            AddToolbarButton();

            if (Automatic)
                SetUpdateAuto(true);
        }

        private void SetUpdateAuto(bool enable)
        {
            if (enable)
            {
                CDebug("Started automatic updates.");
                autoCoroutine = StartCoroutine(UpdateAuto());
            }
            else if (autoCoroutine != null)
            {
                CDebug("Stopped automatic updates. Resetting rigidbodies.");
                StopCoroutine(autoCoroutine);

                SetAllRigidBodies(CollisionDetectionMode.Discrete);
            }
        }

        private IEnumerator UpdateAuto()
        {
            Vector3 velocity, position;
            bool continuous, dynamic;
            float closingSpeed, distance;

            while (true)
            {
                foreach (Vessel x in FlightGlobals.VesselsLoaded)
                {
                    if (x.packed)
                        continue;

                    continuous = false;
                    dynamic = false;

                    foreach (Vessel y in FlightGlobals.VesselsLoaded)
                    {
                        if (y.persistentId == x.persistentId)
                            continue;

                        position = y.transform.position - x.transform.position;
                        velocity = y.rb_velocity - x.rb_velocity;
                        closingSpeed = Vector3.Dot(velocity, -position.normalized);
                        distance = Vector3.Distance(x.CoM, y.CoM) - vesselWidth;

                        if (closingSpeed > continuousCollisionSpeed 
                            && distance <= closingSpeed * updateInterval)
                        {
                            continuous = true;
                            dynamic = dynamic || x.parts.Count < y.parts.Count;

                            // Spectating a missile as it hits a target causes physics chaos.
                            // Our best chance is to switch to the reference frame of the more complex vessel so that the collision is more stable.
                            if (dynamic && SpectatingInboundToTarget(x, y))
                                FlightGlobals.ForceSetActiveVessel(y);
                        }
                    }

                    SetVesselContinuous(x, continuous, dynamic);
                }

                yield return new WaitForSeconds(updateInterval);
            }
        }

        #endregion

        #region Functions

        private static void SetAllRigidBodies(CollisionDetectionMode collisionDetectionMode)
        {
            Rigidbody[] rigidbodies = FindObjectsOfType<Rigidbody>();
            int rbLen = rigidbodies.Length;

            for (int i = 0; i < rbLen; ++i)
                rigidbodies[i].collisionDetectionMode = collisionDetectionMode;

            CDebug($"Collision mode for all rigidbodies set to {collisionDetectionMode}.");
        }

        private static void SetVesselContinuous(Vessel vessel, bool continuous, bool dynamic)
        {
            CollisionDetectionMode mode = continuous ? (dynamic ? continuousCollisionMode : targetCollisionMode) : defaultCollisionMode;

            if (GetVesselCollisionMode(vessel) == mode)
                return;

            SetVesselCollisionMode(vessel, mode);
        }

        private static CollisionDetectionMode GetVesselCollisionMode(Vessel vessel)
        {
            var rb = vessel?.rootPart?.Rigidbody;
            if (rb == null)
                return defaultCollisionMode;

            return rb.collisionDetectionMode;
        }

        private static void SetVesselCollisionMode(Vessel vessel, CollisionDetectionMode collisionMode)
        {
            foreach (Part part in vessel.Parts)
            {
                if (!partCategories.Contains(part.partInfo.category))
                    continue;

                part.Rigidbody.collisionDetectionMode = collisionMode;
            }

            CDebug($"Set the collision mode of {vessel.GetDisplayName()} to {collisionMode}.");
        }

        public static void CDebug(string line)
        {
            Debug.Log($"[ContinuousCollisions]: {line}");
        }

        public static bool SpectatingInboundToTarget(Vessel x, Vessel y)
        {
            return x == FlightGlobals.ActiveVessel
                && y == x.targetObject.GetVessel()
                && (float)y.parts.Count / x.parts.Count > 3;
        }

        #endregion

        #region GUI

        internal void OnGUI()
        {
            if (guiEnabled && !guiHidden)
                DrawGUI();
        }

        public void DrawGUI() =>
            windowRect = GUILayout.Window(windowID, windowRect, FillWindow, "Continuous Collisions v1.2.0", GUILayout.Height(1), GUILayout.Width(200));

        private void FillWindow(int windowID)
        {
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
            Automatic = GUILayout.Toggle(Automatic, "Enabled");
            GUILayout.EndVertical();
        

            // Auto stats.

            if (Automatic)
            {
                if (showAutoStats = GUILayout.Toggle(showAutoStats, "Show Stats"))
                {
                    GUILayout.BeginVertical(boxStyle);
                    scrollPositionAllVessels = GUILayout.BeginScrollView(scrollPositionAllVessels, GUILayout.Height(400));

                    foreach (Vessel vessel in FlightGlobals.VesselsLoaded)
                    {
                        int dynamicParts = 0;
                        int targetParts = 0;

                        foreach (Part part in vessel.Parts)
                        {
                            if (part.Rigidbody.collisionDetectionMode == continuousCollisionMode)
                                dynamicParts++;
                            else if (part.Rigidbody.collisionDetectionMode == targetCollisionMode)
                                targetParts++;
                        }
                        
                        if (targetParts + dynamicParts > 0)
                        {
                            GUI.contentColor = Color.red;
                        }
                        if (vessel.packed)
                        {
                            GUI.contentColor = Color.grey;
                        }

                        GUILayout.BeginVertical(boxStyle);
                            GUILayout.Label(vessel.GetDisplayName());
                            GUILayout.Label($"Dynamic Rigidbodies: {dynamicParts}");
                            GUILayout.Label($"Target Rigidbodies: {targetParts}");
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

        private void AddToolbarButton()
        {
            if (addedAppLauncherButton || !applauncherButton || !HighLogic.LoadedSceneIsFlight)
                return;

            Texture buttonTexture = GameDatabase.Instance.GetTexture("ContinuousCollisions/Textures/icon", false);
            ApplicationLauncher.Instance.AddModApplication(ToggleGui, ToggleGui, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
            addedAppLauncherButton = true;
        }

        public void ToggleGui()
        {
            if (guiEnabled) DisableGui(); else EnableGui();
        }

        public void EnableGui() =>
            guiEnabled = true;

        public void DisableGui()
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
            settingsNode.SetValue("enabled", Automatic, true);
            settingsNode.SetValue("applauncherButton", applauncherButton, true);
            settingsFile.Save(configPath);
        }

        void LoadSettings()
        {
            if (!File.Exists(configPath)) return;

            ConfigNode settingsFile = ConfigNode.Load(configPath);
            ConfigNode settingsNode = settingsFile.GetNode("ContinuousCollisionsSettings");

            settingsNode.TryGetValue("enabled", ref automatic);
            settingsNode.TryGetValue("applauncherButton", ref applauncherButton);
        }

        #endregion
    }
}