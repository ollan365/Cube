using UnityEngine;

/**
 * Small script to fade in/out some menu elements.
 */
[RequireComponent(typeof(CanvasGroup))]
public class Fader : MonoBehaviour
{
    private float _targetAlpha;
    private bool _visible;
    private CanvasGroup _canvasGroup;

    public float fadeSpeed = 5;
    public KeyCode keyCode;

    void Start()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _visible = _canvasGroup.alpha > 0.9f;
        _targetAlpha = _visible?1:0;
    }

    void Update()
    {
        if (Input.GetKeyDown(keyCode))
		{
            _visible = !_visible;
            _targetAlpha = _visible ? 1 : 0;
		}

        _canvasGroup.alpha += (_targetAlpha - _canvasGroup.alpha) * Time.deltaTime * fadeSpeed;
    }
}
