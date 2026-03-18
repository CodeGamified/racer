// Copyright CodeGamified 2025-2026
// MIT License — Racer
using System.Collections.Generic;
using UnityEngine;

namespace Racer.Game
{
    /// <summary>
    /// Procedural oval/circuit track on XZ plane.
    /// Track is defined as a series of waypoints forming a closed loop.
    /// The road has a width; going off-road slows the car.
    ///
    /// Waypoints are generated as a smoothed oval with some variation.
    /// The car can query distance to track center, angle to next waypoint,
    /// and how many laps remain.
    /// </summary>
    public class RacerTrack : MonoBehaviour
    {
        public List<Vector3> Waypoints { get; private set; } = new();

        public float TrackWidth { get; private set; } = 1.2f;
        public int WaypointCount => Waypoints.Count;

        // Track bounds (for camera framing)
        public float TrackMinX { get; private set; }
        public float TrackMaxX { get; private set; }
        public float TrackMinZ { get; private set; }
        public float TrackMaxZ { get; private set; }

        private const int POINT_COUNT = 32;
        private const float BASE_RADIUS_X = 4f;
        private const float BASE_RADIUS_Z = 2.5f;

        public void Initialize(int seed = -1)
        {
            if (seed >= 0) Random.InitState(seed);
            GenerateTrack();
        }

        private void GenerateTrack()
        {
            Waypoints.Clear();

            // Generate oval with perturbation
            for (int i = 0; i < POINT_COUNT; i++)
            {
                float t = (float)i / POINT_COUNT * Mathf.PI * 2f;
                float rx = BASE_RADIUS_X + Random.Range(-0.6f, 0.6f);
                float rz = BASE_RADIUS_Z + Random.Range(-0.4f, 0.4f);
                float x = Mathf.Cos(t) * rx;
                float z = Mathf.Sin(t) * rz;
                Waypoints.Add(new Vector3(x, 0f, z));
            }

            // Smooth pass (average neighbors)
            var smoothed = new List<Vector3>(Waypoints.Count);
            for (int i = 0; i < Waypoints.Count; i++)
            {
                int prev = (i - 1 + Waypoints.Count) % Waypoints.Count;
                int next = (i + 1) % Waypoints.Count;
                smoothed.Add((Waypoints[prev] + Waypoints[i] * 2f + Waypoints[next]) / 4f);
            }
            Waypoints = smoothed;

            // Compute bounds
            TrackMinX = float.MaxValue; TrackMaxX = float.MinValue;
            TrackMinZ = float.MaxValue; TrackMaxZ = float.MinValue;
            foreach (var wp in Waypoints)
            {
                if (wp.x < TrackMinX) TrackMinX = wp.x;
                if (wp.x > TrackMaxX) TrackMaxX = wp.x;
                if (wp.z < TrackMinZ) TrackMinZ = wp.z;
                if (wp.z > TrackMaxZ) TrackMaxZ = wp.z;
            }
        }

        /// <summary>Get waypoint position (wraps index).</summary>
        public Vector3 GetWaypoint(int index) => Waypoints[((index % Waypoints.Count) + Waypoints.Count) % Waypoints.Count];

        /// <summary>Distance from a world position to the nearest point on the track centerline.</summary>
        public float DistanceToTrack(Vector3 pos)
        {
            float minDist = float.MaxValue;
            for (int i = 0; i < Waypoints.Count; i++)
            {
                int next = (i + 1) % Waypoints.Count;
                float d = DistanceToSegment(pos, Waypoints[i], Waypoints[next]);
                if (d < minDist) minDist = d;
            }
            return minDist;
        }

        /// <summary>Is a position on the road (within TrackWidth/2 of centerline)?</summary>
        public bool IsOnRoad(Vector3 pos) => DistanceToTrack(pos) <= TrackWidth * 0.5f;

        /// <summary>Find the closest waypoint index to a position.</summary>
        public int ClosestWaypointIndex(Vector3 pos)
        {
            float minDist = float.MaxValue;
            int best = 0;
            for (int i = 0; i < Waypoints.Count; i++)
            {
                float d = (Waypoints[i] - pos).sqrMagnitude;
                if (d < minDist) { minDist = d; best = i; }
            }
            return best;
        }

        /// <summary>Angle (degrees, XZ plane) from pos toward the given waypoint.</summary>
        public float AngleToWaypoint(Vector3 pos, int waypointIndex)
        {
            var wp = GetWaypoint(waypointIndex);
            var dir = wp - pos;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var ap = p - a;
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            var closest = a + ab * t;
            return Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(closest.x, 0, closest.z));
        }
    }
}
