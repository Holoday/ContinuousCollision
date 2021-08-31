using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP;

[KSPAddon(KSPAddon.Startup.Flight, false)]
public class ContinuousCollision : MonoBehaviour
{
    private int asyncDeltaI = 200;
    private bool asyncSetAllRigidBodiesIsRunning = false;
    private ContinuousCollisionGUI GUI;
    private int rigidbodyCount = 0;

    private bool applyAuto = false;
    public static List<Vessel> loadedVessels;

    // Perhaps link up to a calculation of each vessels' width; for now,
    // a very large size of 50 is assumed as creating a vessel larger than
    // that is difficult.
    private const float vesselWidth = 50;

    private void Start()
    {
        Debug.Log("[ContinuousCollision]: ContinuousCollision is running...");

        GUI = gameObject.AddComponent<ContinuousCollisionGUI>();
        GUI.StartCoroutines();
        GUI.AddToolbarButton();
    }

    private void Update()
    {
        if (!asyncSetAllRigidBodiesIsRunning)
        {
            if (GUI.desireAuto != applyAuto)
            {
                UpdateAutoEnable(GUI.desireAuto);
            }
            else if (GUI.ApplyAtOnce)
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

    #region Rigidbody Modification

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

        Debug.Log("[ContinuousCollision]: All rigidbodies updated. (async)");
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
        Debug.Log("[ContinuousCollision]: All rigidbodies updated.");
        rigidbodyCount = rbLen;
    }

    #endregion

    private void UpdateAutoEnable(bool enable)
    {
        applyAuto = enable;

        if (applyAuto)
        {
            Debug.Log("[ContinuousCollision]: Started automatic updates.");
            StartCoroutine(UpdateAuto());
        }
        else
        {
            Debug.Log("[ContinuousCollision]: Stopped automatic updates.");
            StopCoroutine(UpdateAuto());
        }
    }

    public IEnumerator UpdateAuto()
    {
        while (true)
        {
            loadedVessels = FlightGlobals.VesselsLoaded;

            // Save on some GC.
            Vector3 vslVel;
            float relVelMag;
            bool desireContinuous;

            foreach (Vessel vsl in loadedVessels)
            {
                if (vsl.packed) continue;

                vslVel = vsl.GetObtVelocity();
                desireContinuous = false;

                foreach (Vessel otherVsl in loadedVessels)
                {
                    if (otherVsl.persistentId == vsl.persistentId) continue;

                    relVelMag = Mathf.Abs((otherVsl.GetObtVelocity() - vslVel).magnitude) + (float)otherVsl.acceleration.magnitude;

                    if (relVelMag > 100 && Vector3d.Distance(vsl.GetTransform().position, otherVsl.GetTransform().position) - vesselWidth <= relVelMag)
                    {
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
            if (!currentlyContinuous) SetVesselCollisionMode(vessel, CollisionDetectionMode.ContinuousDynamic);
        }
        else
        {
            if (currentlyContinuous) SetVesselCollisionMode(vessel, CollisionDetectionMode.Discrete);
        }
    }

    private bool IsVesselContinuous(Vessel vessel)
    {
        try
        {
            return vessel.Parts[0].Rigidbody.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic;
        }
        catch
        {
            return false;
        }
    }

    private void SetVesselCollisionMode(Vessel vessel, CollisionDetectionMode collisionMode)
    {
        foreach (Part part in vessel.Parts)
        {
            part.Rigidbody.collisionDetectionMode = collisionMode;
        }

        Debug.Log($"[ContinuousCollision]: Set the collision mode of {vessel.GetDisplayName()} to {collisionMode.ToString()}.");
    }
}