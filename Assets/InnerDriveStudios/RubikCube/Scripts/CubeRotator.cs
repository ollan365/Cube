using UnityEngine;

/**
 * This script has an effect similar to the camera mouse orbit, but it's implementation is completely different.
 * While the camera mouse orbit script rotates around the cube, this script rotates the cube itself in 90 degree increments (with easing).
 * 
 * This allows you to quickly turn the cube upside down as if you are turning it in your hands.
 */
public class CubeRotator : MonoBehaviour
{
	private bool _isDragging = false;
	private Vector3 _dragStartPosition = Vector3.zero;

	//to avoid jaggy rotations of the cube we store the end rotation and ease to it every frame
	private Quaternion rotationTarget = Quaternion.identity;

	[Tooltip("How fast do we ease into a target rotation?")]
	[SerializeField] private float _easingSpeed = 10;
	[Tooltip("Distance in pixels we need to drag before the cube rotates")]
	[SerializeField] private float _dragDistanceToRotate = 100;

	//reference to a box collider we create from script to intercept raycasts/user interaction, since we only want to rotate if we
	//did NOT mouse DOWN on the cube
	private BoxCollider _cubeBoxCollider = null;

	//cache the camera for raycasts
	private Camera _camera = null;

	//Axis configuration (see documentation below to understand how this is used)
	private Vector3[] _axis = { Vector3.right, Vector3.back, Vector3.left, Vector3.forward };

	private void Awake()
	{
		_cubeBoxCollider = GetComponent<BoxCollider>();
		_camera = Camera.main;
		rotationTarget = transform.localRotation;
	}

	private void Update()
	{
		//None of the other methods refer to the Input class, so if you want to change the control triggers, 
		//should you be able to do (most of) that here.
		int dragMouseButton = 0;
		bool wantsToStartDragging = Input.GetMouseButtonDown(dragMouseButton);
		bool wantsToKeepDragging = Input.GetMouseButton(dragMouseButton);

		Vector3 raycastPosition = Input.mousePosition;

		if (_isDragging) { 
			if (wantsToKeepDragging)
			{
				limitedRotateCube(raycastPosition);
			} else
			{
				_isDragging = false;
			}
		} 
		else if (wantsToStartDragging)
		{
			//only allow the drag start if we are not raycasting over the rubikcube
			RaycastHit hit;
			Ray ray = _camera.ScreenPointToRay(raycastPosition);
			if (!_cubeBoxCollider.Raycast(ray, out hit, Vector3.Distance(_camera.transform.position, transform.position) * 2))
			{
				_isDragging = true;
				_dragStartPosition = Input.mousePosition;
			}
		}

		//enables smooth drag rotation on the cube
		transform.localRotation = Quaternion.Slerp(transform.localRotation, rotationTarget, _easingSpeed * Time.deltaTime);
	}

	private void limitedRotateCube(Vector3 pPosition)
	{
		//while dragging we want to rotate the cube by 90 degree increments in a specific direction,
		//BUT we also want to allow time to actually ease towards the new orientation
		//We implement that by simply ignoring further 90 degree as long as we haven't reached the target rotation:
		if (Quaternion.Angle(rotationTarget, transform.localRotation) > 1) return;

		//if we are on or close to the rotationTarget, set that rotation as a starting point
		transform.localRotation = rotationTarget;

		//then check if we moved enough to warrant a rotation of the cube
		Vector3 delta = pPosition - _dragStartPosition;
		float max = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
		if (max < _dragDistanceToRotate) return;

		//if we have to rotate, do we have to rotate around the world y axis (the up axis) or around one of the ground plane axis?
		bool horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);

		if (horizontal)
		{
			//the easy situation
			rotationTarget = Quaternion.AngleAxis(delta.x > 0 ? -90 : 90, Vector3.up) * transform.localRotation;
		}
		else
		{
			//in this situation we want to differentiate between two scenario's:
			//1. drag up and down on the left side of the cube
			//2. drag up and down on the right side of the cube

			//In situation 1 we would like to rotate over an axis that goes from left forward to right back (from the perspective of the camera, so back is close and forward is further away)
			//In situation 2 we would like to rotate over an axis that goes from right forward to left back (from the perspective of the camera etc...)
			//The difficulty however is that the camera is only orbitting around the cube, which causes these axis to constantly change.

			//The way we approach this by looking at what the situation two axis is, based on the current camera y angle.
			//As it turns out (by simply trying this out in the editor or by trying to do it in your head), this is the relation between
			//the camera y angle and that "right" vector: 0-90 +X 90-180 -Z 180-270 -X 270-360 +Z
			//So to figure out which right vector to use, we simply put these in an array called _axis (see above)
			//Then we turn the angle into a value between 0 and 360 and divide by 90 to get a number between 0 and 3 inclusive:
			float angle = _camera.transform.eulerAngles.y;
			angle = ((angle % 360) + 360) % 360;
			int index = (int)angle / 90;
			Vector3 rightAxis = _axis[index];

			//But we also need the left axis, is the one left of the right axis ;)
			Vector3 leftAxis = _axis[(index + 3) % _axis.Length];

			//now do the rotation based on where we dragged
			if (pPosition.x < Screen.width / 2)
			{
				//If we look at the leftAxis from left forward to right back, a drag up means we want to rotate counterclockwise, 
				//so we need a negative angle (and a positive in the other situation)
				rotationTarget = Quaternion.AngleAxis(delta.y > 0 ? -90 : 90, leftAxis) * transform.localRotation;
			}
			else
			{
				//If we look at the right axis from right forward to right back, a drag up means we want to rotate clockwise, 
				//so we need a positive angle (and a positive in the other situation)
				rotationTarget = Quaternion.AngleAxis(delta.y > 0 ? 90 : -90, rightAxis) * transform.localRotation;
			}
		}
	}

}

