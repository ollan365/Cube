#define DEBUG_USER_INTENT_INTERPRETER
using System.Text;
using UnityEngine;

/** 
 * This script allows you to rotate the discs on the cube by dragging discs with your mouse or using the mouse scroll wheel
 * on one of the sides. 
 * 
 * If you are building a mobile application, you will have to replace all the mouse and key controls and replace them with
 * touch controls (see the Update method). 
 * 
 * You should be able to reuse most of the methods however or at least use them as a starting point.
 * 
 * @author J.C. Wichman - InnerDriveStudios.com
 */
public class DiscRotator : MonoBehaviour
{
	[Header("Interaction settings")]

	[Tooltip("Can we rotate a disc by dragging on its side with the left mouse?")]
	[SerializeField] private bool _dragRotateEnabled = true;
    [Tooltip("How many world units do we have to drag before we rotate a disc?")]
    [SerializeField] private float _discRotationDragThreshold = 0.5f;

	[Space(10)]
	[Tooltip("Can we rotate a disc by scrolling the mouse wheel?")]
    [SerializeField] private bool _scrollRotateEnabled = true;

	[Space(10)]
	[Tooltip("Can we undo a disc rotation by clicking with the right mouse button? Note that this will only work if you have actually enabled the move history for the cube while initializing it")]
	[SerializeField] private bool _undoEnabled = true;

	[Header("Rotation settings")]

	[Tooltip("Disc speed measured in 90 degree rotations per second.")]
	[SerializeField] private float _discRotationSpeed = 1;

	[Tooltip("Undo disc speed measured in 90 degree rotations per second")]
	[SerializeField] private float _undoDiscRotationSpeed = 1;

	[Header("Debug settings")]
    
    [Tooltip("Toggle this to show/hide debug info. Note that it is better to leave this flag to true and comment out DEBUG_USER_INTENT_INTERPRETER at the top of this script.")]
    [SerializeField] private bool _generateDebugInfo = true;
    private StringBuilder _debugInfo = new StringBuilder();
    private string _lastDebugInfo = "";
    private bool _debugDirty = false;

    //reference to the cube (of course otherwise we cannot do anything)
    private RubikCube _rubikCube = null;

    //reference to our own private box collider we create intercept raycasts/user interaction
    private BoxCollider _cubeBoxCollider = null;

	//reference to a plane instance that is 'active' while we are actually dragging the mouse on a cube side
	//Since the cubecollider is limited in size and we want to be able to move the mouse 'outside'
	//of that cube while still dragging
	private Plane _collisionPlane = new Plane();

	//while we are dragging a disc we keep track of some instance values 
	//a) because we need them initially, but b) also for debugging (some values could've been made local otherwise)
    private Vector3 _startPointInLocalSpace;    //where on the cube did we start dragging
    private Vector3 _startNormalInLocalSpace;   //what was the local normal at that impact point? 
    private Vector3 _uAxis;						//What is the current tangent...
    private Vector3 _vAxis;						//... and bitangent of the cube we are dragging on

    //cache the camera for getting the rays required for the raycasts
    private Camera _camera = null;
   
	//Are we currently dragging a disc? 
	//Basically if both rubikCube.isRotating and isDragging are false, we are leaving the cube alone
	public bool isDragging { get; private set; }

    private void Awake()
    {
		_rubikCube = GetComponent<RubikCube>();
		_camera = Camera.main;

		//create a collider and set it's size 
		_cubeBoxCollider = GetComponent<BoxCollider>();

		ClearLog();

		isDragging = false;
	}

	private void Update()
    {
        //every update we clear the log so we can print info for the next merrygoround
        //if you disable the DEBUG_USER_INTENT_INTERPRETER compiler constant this will be compiled out
        ClearLog();
        DebugLog("Is rotating? "+_rubikCube.isDiscRotating+"\n");
        DebugLog("Is dragging ? "+isDragging+"\n");

        //None of the other methods refer to the Input class, so if you want to change the control triggers, 
        //should you be able to do (most of) that here:
        Vector3 raycastPosition = Input.mousePosition;
		bool wantToUndo = Input.GetMouseButtonDown(1);
        
        int dragRotateMouseButton = 0;
		bool wantToStartDragRotate = Input.GetMouseButtonDown(dragRotateMouseButton);
		bool wantToKeepDragRotate = Input.GetMouseButton(dragRotateMouseButton);
		
        bool wantToScrollRotate = Input.mouseScrollDelta.y != 0;
		bool scrollRotateIsClockWise = Input.mouseScrollDelta.y > 0;

		//you see a lot of if(!isDragging && !_rubikCube.isDiscRotating .. below)
		//the reason for the duplication is that every line after it may change that state,
		//so you cannot just evaluate it once and store it, but have to reevaluate it every line instead

		if (!isDragging && !_rubikCube.isDiscRotating && _undoEnabled && wantToUndo) _rubikCube.Undo(_undoDiscRotationSpeed);
		if (!isDragging && !_rubikCube.isDiscRotating && _dragRotateEnabled && wantToStartDragRotate) checkDiscDragStart(raycastPosition);
        if (!isDragging && !_rubikCube.isDiscRotating && _scrollRotateEnabled && wantToScrollRotate) checkMouseWheelDiscRotation(raycastPosition, scrollRotateIsClockWise);

		if (isDragging && wantToKeepDragRotate)
		{
			doDiscDragInProgress(raycastPosition);
		}
		else
		{
			isDragging = false;
		}
    }

    /**
     * Check if a ray from the camera to our raycast position hit the cube collider, if so, then we switch to disc dragging state.
     */
    private void checkDiscDragStart(Vector3 pRaycastTarget)
    {
        RaycastHit hit;
        Ray ray = _camera.ScreenPointToRay(pRaycastTarget);

        if (_cubeBoxCollider.Raycast(ray, out hit, Vector3.Distance(_camera.transform.position, transform.position) * 2))
        {
            //get the normal of the side we hit relative to our own transform
            _startNormalInLocalSpace = transform.InverseTransformDirection(hit.normal);
            //get the coordinates of the point we hit relative to our own transform
            _startPointInLocalSpace = transform.InverseTransformPoint(hit.point);

			//setup a collision plane that overlaps with the side of the collider we just hit, but extends into infinity
			_collisionPlane.normal = hit.normal;
			//the plane distance is the distance from the origin in the opposite direction of the normal
            //for safety we include the lossyScale but there are limitations, non uniformly scaled rubik cube WILL break (which they should ;)).
			_collisionPlane.distance = - (_rubikCube.dimensions * transform.lossyScale.x)/2.0f - Vector3.Dot(hit.normal, transform.position);

			//Now we need to know the possible axis we can possibly rotate around when dragging on this specific side
			//If we are dragging a 'disc' on the Z side, we have two options of rotating it: X & Y.
			//Similar principle holds for the other axis.
			//Looking at the start normal in local space all values must be 0 with the exception of either X, Y, or Z.
			//One of these three will be either -1 or 1, and that determines which axis we will rotate around
			//Jus to be very cautious with floats and equality comparisons we use > 0.5 instead of == 1

			Vector3 absoluteNormal = _startNormalInLocalSpace.GetAbsRounded();
			if (absoluteNormal.x > 0.5)
            {
                _uAxis = Vector3.up;
                _vAxis = Vector3.forward;
            }
            else if (absoluteNormal.y > 0.5)
            {
                _uAxis = Vector3.forward;
                _vAxis = Vector3.right;
            }
            else if (absoluteNormal.z > 0.5)
            {
                _uAxis = Vector3.right;
                _vAxis = Vector3.up;
            }

			isDragging = true;
        }
    }

    /**
     * If we indeed switched to dragging a disc 'mode', we check how far our raycast target has moved along one of the axis
	 * of the cube collider side we originally hit (represented by the plane). 
	 * Based on whether we dragged further along the plane's U or V axis we decide which disc to actually rotate.
     */
    private void doDiscDragInProgress(Vector3 pRaycastTarget)
    {
        DebugLog("Start normal:" + _startNormalInLocalSpace + "\n");
        DebugLog("Start point:" + _startPointInLocalSpace + "\n");

        float hit;
        Ray ray = _camera.ScreenPointToRay(pRaycastTarget);

        if (_collisionPlane.Raycast(ray, out hit))
        {
            //start out by getting our current hit in our own local space (same as we did with the startPoint)
            Vector3 currentHitPointInMySpace = transform.InverseTransformPoint(ray.GetPoint(hit));
            DebugLog("Current hit point in local space:" + currentHitPointInMySpace + "\n");
            //then calculate the difference vector on our cube
            Vector3 differenceVector = currentHitPointInMySpace - _startPointInLocalSpace;

            //Use the difference vector to calculate how far we dragged along our local tangent and bitangent axis (U & V)
            float distanceAlongU = Vector3.Dot(differenceVector, _uAxis);
            DebugLog("Distance along U:" + distanceAlongU + "\n");
            float distanceAlongV = Vector3.Dot(differenceVector, _vAxis);
            DebugLog("Distance along V:" + distanceAlongV + "\n");

            float absDistanceAlongU = Mathf.Abs(distanceAlongU);
            float absDistanceAlongV = Mathf.Abs(distanceAlongV);

            //check if we dragged far enough on either of them
            if (Mathf.Max(absDistanceAlongU, absDistanceAlongV) < _discRotationDragThreshold) return;

            //if so, if we dragged furthest along the uAxis, we want to rotate over the vAxis and vice versa
            Vector3 localRotationAxis = Vector3.zero;
			localRotationAxis = absDistanceAlongU < absDistanceAlongV ? _uAxis : _vAxis;
            DebugLog("Rotation axis:" + localRotationAxis + "\n");

			//Ok, now that we have the rotation axis, we now have to calculate the INDEX of the disc we want to rotate.

			//A disc can be oriented in three different directions, x, y or z. 
			//Imagine we are facing the cube on the -z plane, looking in the direction of +z, so x is right and y is up.
			//Now we hit the bottom right cublet with our mouse and drag up/down on the hit plane (in other words in the +y direction).
			//This means we have to rotate a disc over the X axis, and the starting point might be something like (1.5, -1.5, -1.5)
			//Since we are rotating over the X axis only the X element of this starting point is interesting since that indicates
			//the disc we would like to rotate.
			//A similar thing holds if we were to move in the direction of the x axis on the hit plane, that would mean rotating
			//over the y axis and the disc index would be indicated by the y component of the starting point.

			//To 'sift' out this element and get our disc index,
			//we simply Dot that startpoint (eg (1.5, -1.5, -1.5)) on our rotation axis (eg 1,0,0)
            float discIndex = Vector3.Dot(localRotationAxis, _startPointInLocalSpace);
            //now we have a value from -dimensions/2  to _dimensions/2
            //but to use it as a disc index we actually need it to be in the range 0 to dimension-1, so we shift and clamp it
            discIndex = Mathf.Clamp(discIndex + _rubikCube.dimensions / 2.0f, 0, _rubikCube.dimensions - 1);
            DebugLog("Disc index:" + discIndex + "\n");

			//But which way are we turning? First we get our 3rd axis (the axis that we are dragging along)
			Vector3 directionIndicator = Vector3.Cross(_startNormalInLocalSpace, localRotationAxis);
			//Ok this next part is a little bit hard to understand (and took some trial and error ;))
			//Let's say the local normal was x and the rotation vector was y, then our direction indicator would be z, since x X y = z
			//Assuming a left handed coordinate system, imagine holding a cube between your thumb on the bottom and 
			//middle finger on the top with your left hand while pointing your index finger in the direction of the rotation axis.
			//Now drag a piece on the front of the cube in the direction of the directionIndicator (if you haven't broken any 
			//fingers yet, both your thumb and index finger will currently be pointing in that direction) and notice how, 
			//when looking down on the cube from above (in the direction of the negative rotation axis) it spins counterclockwise. 
			//However the left hand rule tells us that that matches a negative rotation. 
			//In other words, if our difference vector is in the direction of the direction indicator, rotation is negative,
			//and if it is opposite to the directionVector, it is positive:
			bool positive = Vector3.Dot(differenceVector, directionIndicator) < 0;
            DebugLog("Positive rotation:" + positive + "\n");

			//(On a side note the whole story also holds for a right coordinate system, but you would have to use your right hand to try that out ;))

            _rubikCube.RotateDisc(localRotationAxis, (int)discIndex, positive, _discRotationSpeed, true);
        } else
		{
			isDragging = false;
		}
    }

    /**
     * Check which disc is under the raycast target and rotate it in a CW or CCW direction.
     */
    private void checkMouseWheelDiscRotation(Vector3 pRaycastTarget, bool pClockWise)
    {
        RaycastHit hit;
        Ray ray = _camera.ScreenPointToRay(pRaycastTarget);

		if (_cubeBoxCollider.Raycast(ray, out hit, Vector3.Distance(_camera.transform.position, transform.position) * 2))
		{
            //first get the normal of the side we hit relative to our own transform
            _startNormalInLocalSpace = transform.InverseTransformDirection(hit.normal);

            //is this a positive or a negative normal? 2 out of 3 elements are 0, the other one is +-1
            bool positiveNormal = (_startNormalInLocalSpace.x + _startNormalInLocalSpace.y + _startNormalInLocalSpace.z) > 0;

            //rotation axis has to be positive, and in the form of 1's and 0's
            Vector3 rotationAxis = _startNormalInLocalSpace.GetAbsRounded();

            //if we are looking at a negative normal the layer is 0 (the first layer) otherwise dimensions - 1 (the last layer)
            int layer = !positiveNormal ? 0 : (_rubikCube.dimensions - 1);

			//in addition pClockWise should be CW rotation, but if we are looking at a negative normal, 
			//it will act as CCW rotation, so we need to flip it in that case
            _rubikCube.RotateDisc(rotationAxis, layer,  positiveNormal ? pClockWise : !pClockWise, _discRotationSpeed, true);
        }
    }

	[System.Diagnostics.Conditional("DEBUG_USER_INTENT_INTERPRETER")]
    private void DebugLog (string pInfo)
    {
        if (!_generateDebugInfo) return;

        _debugInfo.Append(pInfo);
        _debugDirty = true;
    }

    [System.Diagnostics.Conditional("DEBUG_USER_INTENT_INTERPRETER")]
    private void ClearLog()
    {
        if (!_generateDebugInfo) return;

        _debugInfo.Clear();
        _debugDirty = true;
    }

    public string GetDebugInfo()
	{
        if (_debugDirty)
		{
            _lastDebugInfo = _debugInfo.ToString();
		}

        return _lastDebugInfo;
	}
}
