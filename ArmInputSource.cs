using UnityEngine;

// Base class for anything that can drive the claw arm.
// Right now: KeyboardMouseInput. Later: an UltrasonicInput that reads the sensor.
// The arm only talks to this class, so swapping input never touches the arm code.
public abstract class ArmInputSource : MonoBehaviour
{
    // Lever position, normalized: 0 = far left, 1 = far right.
    // (The ultrasonic sensor will map distance -> 0..1 the same way.)
    public abstract float GetLever01();

    // True only on the frame the button is pressed.
    public abstract bool GetButtonDown();

    // True the whole time the button is held down (used by the boss QTE).
    public abstract bool GetButton();
}