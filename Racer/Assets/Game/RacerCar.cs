// Copyright CodeGamified 2025-2026
// MIT License — Racer
using UnityEngine;
using CodeGamified.Time;

namespace Racer.Game
{
    /// <summary>
    /// Car physics on XZ plane.
    /// Position, velocity, heading (degrees), speed, steering.
    /// Off-road penalty: drag increases substantially.
    ///
    /// Controls exposed to bytecode:
    ///   - set_throttle(v)  : -1..1 (reverse..full)
    ///   - set_steering(v)  : -1..1 (left..right)
    ///
    /// Sim-time-aware: physics step uses sim delta.
    /// </summary>
    public class RacerCar : MonoBehaviour
    {
        // State (readable by script)
        public float PosX { get; private set; }
        public float PosZ { get; private set; }
        public float Speed { get; private set; }           // units/sec
        public float Heading { get; private set; }         // degrees, 0=+Z, 90=+X
        public float VelX { get; private set; }
        public float VelZ { get; private set; }

        // Controls (writable by script, clamped)
        public float Throttle { get; set; }                // -1..1
        public float Steering { get; set; }                // -1..1

        // Physics constants
        public const float MaxSpeed           = 8f;
        public const float Acceleration       = 6f;
        public const float BrakeDecel         = 10f;
        public const float RoadDrag           = 0.5f;
        public const float OffRoadDrag        = 4f;
        public const float SteeringRate       = 180f;      // deg/sec at max speed
        public const float MinSpeedForSteering = 0.3f;

        private RacerTrack _track;
        private bool _onRoad = true;

        public bool IsOnRoad => _onRoad;

        public void Initialize(RacerTrack track, Vector3 startPos, float startHeading)
        {
            _track = track;
            PosX = startPos.x;
            PosZ = startPos.z;
            Heading = startHeading;
            Speed = 0f;
            VelX = 0f;
            VelZ = 0f;
            Throttle = 0f;
            Steering = 0f;
        }

        /// <summary>Step physics by dt sim-seconds.</summary>
        public void PhysicsStep(float dt)
        {
            if (dt <= 0f) return;

            float throttle = Mathf.Clamp(Throttle, -1f, 1f);
            float steering = Mathf.Clamp(Steering, -1f, 1f);

            // Steering (only when moving)
            if (Mathf.Abs(Speed) > MinSpeedForSteering)
            {
                float steerFactor = Mathf.Clamp01(Speed / MaxSpeed);
                float steerDeg = steering * SteeringRate * steerFactor * dt;
                Heading += steerDeg;
            }

            // Normalize heading
            Heading = ((Heading % 360f) + 360f) % 360f;

            // Acceleration / braking
            float accel = 0f;
            if (throttle > 0f)
                accel = throttle * Acceleration;
            else if (throttle < 0f)
            {
                if (Speed > 0.1f)
                    accel = throttle * BrakeDecel; // braking
                else
                    accel = throttle * Acceleration * 0.3f; // gentle reverse
            }

            Speed += accel * dt;

            // Drag
            _onRoad = _track != null && _track.IsOnRoad(new Vector3(PosX, 0, PosZ));
            float drag = _onRoad ? RoadDrag : OffRoadDrag;
            Speed -= Speed * drag * dt;

            // Clamp speed
            Speed = Mathf.Clamp(Speed, -MaxSpeed * 0.3f, MaxSpeed);
            if (Mathf.Abs(Speed) < 0.01f) Speed = 0f;

            // Velocity vector
            float headRad = Heading * Mathf.Deg2Rad;
            VelX = Mathf.Sin(headRad) * Speed;
            VelZ = Mathf.Cos(headRad) * Speed;

            // Position update
            PosX += VelX * dt;
            PosZ += VelZ * dt;
        }

        /// <summary>Reset car to a position and heading.</summary>
        public void ResetTo(Vector3 pos, float heading)
        {
            PosX = pos.x;
            PosZ = pos.z;
            Heading = heading;
            Speed = 0f;
            VelX = 0f;
            VelZ = 0f;
            Throttle = 0f;
            Steering = 0f;
        }
    }
}
