using UnityEngine;
using System.Collections;
using KSP.UI.Screens;

internal class ContinuousCollisionGUI : MonoBehaviour
{
    // Internal GUI logic variables.
    private Rect windowRect = new Rect(230, 10, 500, 500);
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
    public bool ApplyAtOnce;
    public bool ApplyAsync;

    public bool isApplyingAsync = false;
    public int rigidbodyCount = 0;

    // Toolbar.
    public static bool addedAppLauncherButton = false;
    public static bool guiEnabled = false;

    public void DrawGUI() =>
        windowRect = GUI.Window(0, windowRect, drawWindow, "Continuous Collision GUI");
    public void StartCoroutines() =>
        StartCoroutine(setFPS(0.5f)); 

    private void drawWindow(int windowID)
    {
        GUI.Label(new Rect(20, 30, 460, 40),
            "This is an early release, not meant for stable use. " +
            "We are not responsible if your game crashes or otherwise breaks.");

        GUI.Label(new Rect(20, 100, 460, 20), "Select Collision Mode");

        newModeToggleBools[0] = GUI.Toggle(new Rect(20, 120, 460, 20), modeToggleBools[0], " Continuous-Dynamic (best, least performance.)");
        newModeToggleBools[1] = GUI.Toggle(new Rect(20, 140, 460, 20), modeToggleBools[1], " Continuous");
        newModeToggleBools[2] = GUI.Toggle(new Rect(20, 160, 460, 20), modeToggleBools[2], " Continuous-Speculative");
        newModeToggleBools[3] = GUI.Toggle(new Rect(20, 180, 460, 20), modeToggleBools[3], " Discrete (defualt, worst, most performance.)");
        if (!isApplyingAsync)
        {
            VerifyToggles();
            AssertainDesiredCollisionMode();
            isApplied = DesiredCollisionMode == CurrentCollisionMode;
        }

        GUI.Toggle(new Rect(20, 220, 460, 20), false, " Auto (Not configued.)");
        GUI.Label(new Rect(20, 250, 460, 20), (isApplied ? $"All changes applied. Number of rigidbodies updated: {rigidbodyCount.ToString()}." : "Not Applied Yet!"));

        if (GUI.Button(new Rect(20, 280, 110, 20), "Apply") && !isApplyingAsync)
        {
            ApplyAtOnce = true;
            ApplyAsync = false;
        }
        if (GUI.Button(new Rect(140, 280, 110, 20), isApplyingAsync ? "Applying Async..." : "Apply Async") && !isApplyingAsync)
        {
            ApplyAtOnce = false;
            ApplyAsync = true;
        }

        GUI.Label(new Rect(20, 470, 150, 20), $"Current FPS: {framesPerSecond.ToString()}");

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
        if (modeToggleBools[4])
        {
            // reset, if this is somehow triggered, as auto is not configured yet.
            Debug.LogWarning("modeToggleBools[4] (auto) selected erroneously! resetting...");
            modeToggleBools[4] = false;
            modeToggleBools[3] = true;
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
            Texture buttonTexture = GameDatabase.Instance.GetTexture("ContinuousCollision/Textures/icon", false);
            ApplicationLauncher.Instance.AddModApplication(ToggleGui, ToggleGui, null, null, null, null, (ApplicationLauncher.AppScenes)63, buttonTexture);
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

