using UnityEngine;

/**
 * Basic camera mouse orbit script, which rotates the camera around our target.
 */
public class CameraMouseOrbit : MonoBehaviour
{
    private float _xRotation;                   //the current x euler angle of the camera
    private float _yRotation;                   //the current y euler angle of the camera
    private float _distance;                    //the distance from the target we would like the camera to be

    private float _targetXRotation;             //the x euler angle we are aiming for 
    private float _targetYRotation;             //the y euler angle we are aiming for
    
    public float targetDistance;

    private Camera _camera;                     //reference to the camera itself

    [Tooltip("Which target are we looking at?")]
    [SerializeField] private Transform _target = null;
    [Tooltip("Are we looking at a point at an offset from the actual target?")]
    [SerializeField] private Vector3 _worldLookAtOffsetFromTarget = Vector3.zero;

    [Tooltip("How fast do we rotate horizontally? Negative values flip the direction")]
    [SerializeField] private float _xMouseSensitity = 5;
    [Tooltip("How fast do we rotate vertically? Negative values flip the direction")]
    [SerializeField] private float _yMouseSensitity = 5;
    [Tooltip("How fast do we ease towards the target rotation?")]
    [SerializeField] private float _easeSpeed = 1;
    [Tooltip("What is the minimum and maximum tilt angle ?")]
    [SerializeField] private Vector2 _xAngleRange = new Vector2(-30, 30);

    void Awake()
    {
        _camera = Camera.main;

        //set some default values based on the current camera's position
        _xRotation = _targetXRotation = _camera.transform.eulerAngles.x;
        _yRotation = _targetYRotation = _camera.transform.eulerAngles.y;
        _distance = targetDistance = Vector3.Distance(_camera.transform.position, _target.position + _worldLookAtOffsetFromTarget);

        //set the camera to the position we calculate based on the values above so that we don't start out easing right away
        _camera.transform.position = getCurrentTargetPosition();
        //_camera.transform.LookAt(target.position + worldLookAtOffsetFromTarget);
    }

    // Update is called once per frame
    void Update()
    {
        //if we are dragging with the middle mouse, update our target values
        if (Input.GetMouseButton(2))
		{
            int modifier = (Application.platform == RuntimePlatform.WebGLPlayer) ? 1 : 2;
            _targetXRotation += Input.GetAxis("Mouse Y") * _yMouseSensitity * modifier;
            _targetYRotation += Input.GetAxis("Mouse X") * _xMouseSensitity * modifier;

            _targetXRotation = Mathf.Clamp(_targetXRotation, _xAngleRange.x, _xAngleRange.y);
		}

        //the ease towards these values
        _xRotation += (_targetXRotation - _xRotation) * _easeSpeed * Time.deltaTime;
        _yRotation += (_targetYRotation - _yRotation) * _easeSpeed * Time.deltaTime;
        _distance += (targetDistance - _distance) * _easeSpeed * Time.deltaTime;

        //and lastly update the camera position and make sure we look at our target
        _camera.transform.position = getCurrentTargetPosition();
        _camera.transform.LookAt(_target.position + _worldLookAtOffsetFromTarget);
    }

    private Vector3 getCurrentTargetPosition ()
	{
        Vector3 forwardVector = -Vector3.forward * _distance;
        Quaternion rotation = Quaternion.Euler(_xRotation, _yRotation, 0);
        Vector3 targetOffset = rotation * forwardVector;
        Vector3 targetPosition = _target.position + _worldLookAtOffsetFromTarget + targetOffset;
        return targetPosition;
	}
}
