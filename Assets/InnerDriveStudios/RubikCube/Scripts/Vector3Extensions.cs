using UnityEngine;

/**
 * An extension class to add some convenience methods to the Vector3 class
 */
public static class Vector3Extensions { 

    /**
     * This rounds all elements in the input vector3 and takes their absolute values.
     */
    public static Vector3 GetAbsRounded(this Vector3 pInput)
    {
        return  new Vector3(
               Mathf.Round(Mathf.Abs(pInput.x)),
               Mathf.Round(Mathf.Abs(pInput.y)),
               Mathf.Round(Mathf.Abs(pInput.z))
           );
    }

}



