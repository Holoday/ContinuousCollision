using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP;
using System.Linq;

/*class CollisionEvent
{
    public EventReport evt;
    public float time;
    public float deltaTime;
    public float fixedDeltaTime;

    public CollisionEvent(EventReport e, float t, float dt, float fdt)
    {
        evt = e;
        time = t;
        deltaTime = dt;
        fixedDeltaTime = fdt;
    } 
}*/

[KSPAddon(KSPAddon.Startup.Flight, false)]
public class ContinuousCollision : MonoBehaviour
{
    private ContinuousCollisionGUI GUI;
    private Coroutine autoCoroutine;
    private bool applyAuto = false;
    private bool applySpeculative = false;
    private bool applyAutoswitch = false;
    internal static List<Vessel> loadedVessels;

    public static CollisionDetectionMode continuousCollisionMode = CollisionDetectionMode.ContinuousDynamic;
    public static List<PartCategories> categories = new List<PartCategories> { 
        PartCategories.Structural, 
        PartCategories.Aero, 
        PartCategories.Control
    };

    // Perhaps link up to a calculation of each vessels' width; for now,
    // a very large size of 50 is assumed as creating a vessel larger than
    // that is difficult.
    public float vesselWidth = 50;

    private void Start()
    {
        GUI = gameObject.AddComponent<ContinuousCollisionGUI>();
        GUI.AddToolbarButton();
    }

    // Log collisions for debugging.

    /*List<Part> structuralParts;
    List<CollisionEvent> structuralCollisions;

    void registerCollisionEvents()
    {
        GameEvents.onCollision.Add(OnCollision);
        GameEvents.OnCollisionEnhancerHit.Add(OnCollisionEnhancerHit);
        structuralParts = new List<Part>();
        structuralCollisions = new List<CollisionEvent>();
    }

    private void OnCollision(EventReport Evt)
    {
        Debug.Log(Evt.ToString());
        string pName = Evt.origin.partInfo.name;

        if (pName == "structuralIBeam3")
        {
            if (!structuralParts.Contains(Evt.origin))
            {
                structuralParts.Add(Evt.origin);
                CollisionEvent clEvent = new CollisionEvent(Evt, Time.time, Time.deltaTime, Time.fixedDeltaTime);
                structuralCollisions.Add(clEvent);
            } 
        }
    }

    private void OnCollisionEnhancerHit(Part p, RaycastHit hit)
    {
        Debug.Log(p.ToString());
        Debug.Log(hit.ToString());
    }*/

    private void Update()
    {
        if (GUI.enableCC != applyAuto)
            UpdateAutoEnable(GUI.enableCC);

        if (GUI.useAutoSwitch != applyAutoswitch)
            applyAutoswitch = GUI.useAutoSwitch;

        if (GUI.useSpeculative2 != applySpeculative)
        {
            applySpeculative = GUI.useSpeculative2;
            continuousCollisionMode = applySpeculative ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
            SetAllRigidBodies(CollisionDetectionMode.Discrete);
        }

        //if (GUI.applySimple)
        //    SetAllRigidBodies(CollisionDetectionMode.ContinuousDynamic);

        //if (GUI.disableRigidbodies)
        //{
        //    StartCoroutine(DisableAllRigidbodies(true));
        //}
    }

    private void OnGUI()
    {
        if (ContinuousCollisionGUI.guiEnabled && !ContinuousCollisionGUI.guiHidden)
            GUI.DrawGUI();
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

    private protected IEnumerator DisableAllRigidbodies(bool disable)
    {
        /*Rigidbody[] rigidbodies = GameObject.FindObjectsOfType<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = disable;
            rb.detectCollisions = !disable;
            rb.constraints = disable ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.None;
            rb.freezeRotation = !disable;
            rb.solverIterations = 0;
            rb.solverIterationCount = 0;
            rb.solverVelocityIterations = 0;
            rb.solverVelocityIterationCount = 0;

            if (disable)
                rb.Sleep();
                rb.Sleep();
            else
                rb.WakeUp();

            //Destroy(rb);
            rb.gameObject.GetComponent<Part>().attachJoint.DestroyJoint();
            
        }*/

        /*var vessels = FlightGlobals.VesselsLoaded;
        foreach (Vessel v in vessels)
        {
            v.GoOnRails();
            v.packed = true;

            var parts = v.parts;
            foreach (Part p in parts)
            {
                var aj = p.attachJoint;
                if (aj == null) continue;

                var joints = p.attachJoint.joints;
                if (joints == null) continue;

                foreach (var joint in p.attachJoint.joints)
                {
                    Destroy(joint);
                }

            }

            yield return new WaitForSeconds(0.1f);

            foreach (Part p in parts)
            {
                var aj = p.attachJoint;

                if (aj == null) continue;

                var joints = p.attachJoint.joints;
                if (joints == null) continue;

                Destroy(aj);
                Destroy(p.Rigidbody);
            }

        }*/

        yield break;
    }

    private void UpdateAutoEnable(bool enable)
    {
        applyAuto = enable;

        if (enable)
        {
            Debug.Log("[ContinuousCollision]: Started automatic updates.");
            autoCoroutine = StartCoroutine(UpdateAuto());
        }
        else
        {
            Debug.Log("[ContinuousCollision]: Stopped automatic updates.");
            StopCoroutine(autoCoroutine);
            SetAllRigidBodies(CollisionDetectionMode.Discrete);
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
            ITargetable tgt;

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
                            if (applyAutoswitch 
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

            //if (part.partInfo.category == PartCategories.Engine) {
            //if (part.partInfo.category != PartCategories.Structural) {
            if (!categories.Contains(part.partInfo.category)) {
                //part.Rigidbody.detectCollisions = false;
                //Debug.Log($"[ContinuousCollision]: Skipped part {part.partInfo.name}.");
                continue;
            }
            else
            {
                part.Rigidbody.collisionDetectionMode = collisionMode;
            }

        }

        //Debug.Log($"[ContinuousCollision]: Set the collision mode of {vessel.GetDisplayName()} to {collisionMode}.");
    }
}