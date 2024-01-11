using UnityEngine;

/**
 * Simple script to copy the orientation of the cube to the AxisDisplay gizmo
 */
public class AxisDisplay : MonoBehaviour
{
    public Transform copyFrom = null;
    private Camera _camera = null;

    private void Awake()
    {
        _camera = Camera.main;
    }

    void Update()
    {
        if (copyFrom != null)
        {
            //how is the target rotated with respect to the camera?
            //we take the target's world rotation as seen by the camera and then store it as a 
            //localRotation for this object since it is a direct child of ot's rendering camera
            transform.localRotation = Quaternion.Inverse(_camera.transform.rotation) * copyFrom.rotation;
        }
    }
}
