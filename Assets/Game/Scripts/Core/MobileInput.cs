using UnityEngine;

namespace StreetRush
{
    /// <summary>
    /// Bridge between on-screen touch controls and whatever the player is
    /// controlling. Touch UI writes here; the player and cars read here.
    /// Keyboard input is read separately as a desktop fallback.
    /// </summary>
    public static class MobileInput
    {
        // Driving
        public static float Steer;      // -1 left .. 1 right
        public static float Throttle;   // 0..1
        public static bool Brake;
        public static bool Reverse;

        // On foot
        public static Vector2 MoveVector;   // x = right, y = forward
        public static Vector2 ExternalMove; // scripted input (automated tests), used when the joystick is idle
        public static bool RunHeld;
        public static bool JumpPressed;     // consumed by the player
        public static Vector2 LookDelta;    // accumulated pixels, consumed by the camera

        // Shared
        public static bool InteractPressed; // consumed once used

        public static void ResetAll()
        {
            Steer = 0f; Throttle = 0f; Brake = false; Reverse = false;
            MoveVector = Vector2.zero; ExternalMove = Vector2.zero; RunHeld = false; JumpPressed = false;
            LookDelta = Vector2.zero; InteractPressed = false;
        }
    }
}
