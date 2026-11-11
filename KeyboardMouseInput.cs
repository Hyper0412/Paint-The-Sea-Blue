using UnityEngine;

// Temporary input while the ultrasonic sensor isn't available.
// Mouse mode: mouse X across the screen = lever position (closest to how the real lever feels).
// Keyboard mode: hold A / D to move the lever (useful for player 2 in co-op later).
public class KeyboardMouseInput : ArmInputSource
{
    public enum Mode { Mouse, Keyboard }

    public Mode mode = Mode.Mouse;

    [Header("Keyboard")]
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode buttonKey = KeyCode.Space;
    [Tooltip("How fast the lever moves with keys (full sweep = 1 / speed seconds)")]
    public float keyboardSpeed = 0.8f;

    float lever = 0.5f; // start in the middle (arm pointing straight down)

    public override float GetLever01()
    {
        if (mode == Mode.Mouse)
        {
            lever = Mathf.Clamp01(Input.mousePosition.x / Screen.width);
        }
        else
        {
            float dir = 0f;
            if (Input.GetKey(leftKey)) dir -= 1f;
            if (Input.GetKey(rightKey)) dir += 1f;
            lever = Mathf.Clamp01(lever + dir * keyboardSpeed * Time.deltaTime);
        }
        return lever;
    }

    public override bool GetButtonDown()
    {
        if (Input.GetKeyDown(buttonKey)) return true;
        return mode == Mode.Mouse && Input.GetMouseButtonDown(0);
    }

    public override bool GetButton()
    {
        if (Input.GetKey(buttonKey)) return true;
        return mode == Mode.Mouse && Input.GetMouseButton(0);
    }
}