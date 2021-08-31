using UnityEngine;
using System.Collections;
using KSP.UI.Screens;
using System.Linq;

internal class ContinuousCollisionGUI : MonoBehaviour
{
    // Internal GUI logic variables.
    private Rect windowRect = new Rect(100, 100, 0, 0);
    private Vector2 scrollPositionAllVessels;
    private bool showAutoStats = false;
    // Used for the toggle switches, as to only allow one to be enabled at once,
    // defaulting to Discrete Collision.
    private bool[] modeToggleBools = new bool[] { false, false, false, true, false };
    private bool[] newModeToggleBools = new bool[] { false, false, false, true, false };
    private float framesPerSecond = 0;
    // Internal collision mode logic variables
    private CollisionDetectionMode desiredCollisionMode = CollisionDetectionMode.Discrete;
    private bool isApplied;

    // Public read only variables
    public CollisionDetectionMode DesiredCollisionMode { get { return desiredCollisionMode; } }
    public CollisionDetectionMode CurrentCollisionMode = CollisionDetectionMode.Discrete;
    public bool desireAuto = true;
    public bool ApplyAtOnce;
    public bool ApplyAsync;

    public bool isApplyingAsync = false;
    public int rigidbodyCount = 0;

    // Toolbar.
    public static bool addedAppLauncherButton = false;
    public static bool guiEnabled = false;

    public void DrawGUI() =>
        windowRect = GUILayout.Window(0, windowRect, FillWindow, "Continuous Collision", GUILayout.Height(1), GUILayout.Width(350));
    public void StartCoroutines() =>
        StartCoroutine(setFPS(0.5f));

    private void FillWindow(int windowID)
    {
        GUIStyle boxStyle = GUI.skin.GetStyle("Box");

        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("This is an early release, not meant for stable use. " +
            "We are not responsible if your game crashes or otherwise breaks.");
        GUILayout.EndVertical();

        desireAuto = GUILayout.Toggle(desireAuto, "Auto");

        if (!desireAuto)
        {
            GUILayout.Label("Select Collision Mode");

            newModeToggleBools[0] = GUILayout.Toggle(modeToggleBools[0], " Continuous-Continuous (best, least performance.)");
            newModeToggleBools[1] = GUILayout.Toggle(modeToggleBools[1], " Continuous");
            newModeToggleBools[2] = GUILayout.Toggle(modeToggleBools[2], " Continuous-Speculative");
            newModeToggleBools[3] = GUILayout.Toggle(modeToggleBools[3], " Discrete (defualt, worst, most performance.)");
            if (!isApplyingAsync)
            {
                VerifyToggles();
                AssertainDesiredCollisionMode();
                isApplied = DesiredCollisionMode == CurrentCollisionMode;
            }

            GUILayout.Label(isApplied ? $"All changes applied. Number of rigidbodies updated: {rigidbodyCount.ToString()}." : "Not Applied Yet!");

            if (GUILayout.Button("Apply") && !isApplyingAsync)
            {
                ApplyAtOnce = true;
                ApplyAsync = false;
            }
            if (GUILayout.Button(isApplyingAsync ? "Applying Async..." : "Apply Async") && !isApplyingAsync)
            {
                ApplyAtOnce = false;
                ApplyAsync = true;
            }
        }
        else
        {
            if (showAutoStats = GUILayout.Toggle(showAutoStats, "Show Stats"))
            {
                GUILayout.BeginVertical(boxStyle);
                scrollPositionAllVessels = GUILayout.BeginScrollView(scrollPositionAllVessels, GUILayout.Height(300));

                foreach (Vessel vessel in ContinuousCollision.loadedVessels)
                {
                    GUILayout.BeginVertical(boxStyle);
                        GUILayout.Label(vessel.GetDisplayName());
                        int continuousParts = vessel.Parts.Count(p => p.Rigidbody.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic);
                        GUILayout.Label($"Continuous Rigidbodies: {continuousParts.ToString()}");
                        GUILayout.Label($"Unpacked: {!vessel.packed}");
                    GUILayout.EndVertical();
                }

                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }

        GUILayout.Label($"Current FPS: {framesPerSecond.ToString()}");

        GUI.DragWindow(new Rect(0, 0, 10000, 500));
    }

    private void VerifyToggles()
    {
        for (int i = 0; i < newModeToggleBools.Length; ++i)
        {
            if (newModeToggleBools[i] != modeToggleBools[i])
            {
                for (int k = 0; k < modeToggleBools.Length; ++k)
                {
                    modeToggleBools[k] = false;
                }
                modeToggleBools[i] = true;
                break;
            }
        }
    }

    private void AssertainDesiredCollisionMode()
    {
        // Awful, but necessary. Feel a bit like yanderedev.
        // In fact, all of this awful GUI code feels like that.
        if (modeToggleBools[0])
        {
            desiredCollisionMode = CollisionDetectionMode.ContinuousDynamic;
            return;
        }
        if (modeToggleBools[1])
        {
            desiredCollisionMode = CollisionDetectionMode.Continuous;
            return;
        }
        if (modeToggleBools[2])
        {
            desiredCollisionMode = CollisionDetectionMode.ContinuousSpeculative;
            return;
        }
        if (modeToggleBools[3])
        {
            desiredCollisionMode = CollisionDetectionMode.Discrete;
            return;
        }
    }

    public IEnumerator setFPS(float refreshRate)
    {
        int frameCount = 0;
        float timeCount = 0f;
        while (true)
        {
            yield return null;
            if (timeCount > refreshRate)
            {
                framesPerSecond = frameCount / timeCount;
                frameCount = 0;
                timeCount = 0f;
                continue;
            }
            timeCount += Time.deltaTime;
            ++frameCount;
        }
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
    void DisableGui() => guiEnabled = false;
    #endregion
}

