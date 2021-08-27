using System.Collections;
using UnityEngine;
using KSP;

[KSPAddon(KSPAddon.Startup.Flight, false)]
public class ContinuousCollision : MonoBehaviour
{
    private int asyncDeltaI = 200;
    private bool asyncSetAllRigidBodiesIsRunning = false;
    private ContinuousCollisionGUI GUI;
    private int rigidbodyCount = 0;

    private void Start()
    {
        Debug.Log("ContinuousCollision is running...");
        GUI = gameObject.AddComponent<ContinuousCollisionGUI>();
        GUI.StartCoroutines();
        GUI.AddToolbarButton();
    }

    private void Update()
    {
        if (!asyncSetAllRigidBodiesIsRunning)
        {
            if (GUI.ApplyAtOnce)
            {
                setAllRigidBodies(GUI.DesiredCollisionMode);
                GUI.ApplyAtOnce = GUI.ApplyAsync = false;
                GUI.CurrentCollisionMode = GUI.DesiredCollisionMode;
                GUI.rigidbodyCount = rigidbodyCount;
            }
            else if (GUI.ApplyAsync)
            {
                StartCoroutine(asyncSetAllRigidBodies(GUI.DesiredCollisionMode));
                GUI.ApplyAtOnce = GUI.ApplyAsync = false;
                GUI.CurrentCollisionMode = GUI.DesiredCollisionMode;
                GUI.rigidbodyCount = rigidbodyCount;
            }
        }
    }

    private void OnGUI()
    {
        if (ContinuousCollisionGUI.guiEnabled)
        {
            GUI.DrawGUI();
        }
    }

    private IEnumerator asyncSetAllRigidBodies(CollisionDetectionMode collisionDetectionMode)
    {
        GUI.isApplyingAsync = true;
        asyncSetAllRigidBodiesIsRunning = true;
        Rigidbody[] rigidbodies = GameObject.FindObjectsOfType<Rigidbody>();
        int rbLen = rigidbodies.Length;
        rigidbodyCount = rbLen;
        // Updates the list of rigidbodies <asyncDeltaI> at a time
        // then waits for the next FixedUpdate cycle.
        //
        // Unclear whether this is actually more "efficient" than
        // just doing this all on one frame.
        // TODO: ~ ~ ~ ~ TEST PERFORMANCE ~ ~ ~ ~

        int i = -1;
        int deltaI;
        while ((i + 1) < rbLen)
        {
            deltaI = 0;
            while (deltaI < asyncDeltaI && (i + 1) < rbLen)
            {
                ++i;
                ++deltaI;
                rigidbodies[i].collisionDetectionMode = collisionDetectionMode;
            }
            yield return new WaitForFixedUpdate();
        }

        Debug.Log("All rigidbodies updated. (async)");
        asyncSetAllRigidBodiesIsRunning = false;
        GUI.isApplyingAsync = false;
    }

    private protected void setAllRigidBodies(CollisionDetectionMode collisionDetectionMode)
    {
        Rigidbody[] rigidbodies = GameObject.FindObjectsOfType<Rigidbody>();
        int rbLen = rigidbodies.Length;
        for (int i = 0; i < rbLen; ++i)
        {
            rigidbodies[i].collisionDetectionMode = collisionDetectionMode;
        }
        Debug.Log("All rigidbodies updated.");
        rigidbodyCount = rbLen;
    }
}

