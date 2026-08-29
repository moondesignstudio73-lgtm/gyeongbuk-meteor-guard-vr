using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    public enum PcGazeMode { Auto, VREyeTracking, WebcamGaze, MouseGaze, CameraCenterGaze, WindowsVRSimulation }

    public static class GazeModePolicy
    {
        public static PcGazeMode Select(PcGazeMode requested, bool vr, bool webcam, bool mouse)
        {
            if (requested == PcGazeMode.WindowsVRSimulation) return vr ? PcGazeMode.VREyeTracking : PcGazeMode.WindowsVRSimulation;
            if ((requested == PcGazeMode.Auto || requested == PcGazeMode.VREyeTracking) && vr) return PcGazeMode.VREyeTracking;
            if ((requested == PcGazeMode.Auto || requested == PcGazeMode.WebcamGaze) && webcam) return PcGazeMode.WebcamGaze;
            if (requested != PcGazeMode.CameraCenterGaze && mouse) return PcGazeMode.MouseGaze;
            return PcGazeMode.CameraCenterGaze;
        }
    }

    // Device-independent two-dimensional affine calibration. No video is retained.
    [Serializable]
    public sealed class WebcamCalibrationMap
    {
        public static readonly Vector2[] Targets = {
            new Vector2(.5f, .5f), new Vector2(.18f, .5f), new Vector2(.82f, .5f),
            new Vector2(.5f, .8f), new Vector2(.5f, .2f)
        };
        private Vector3 horizontal;
        private Vector3 vertical;
        public bool IsCalibrated { get; private set; }
        public float Error { get; private set; }
        public void Reset() { IsCalibrated = false; Error = 0f; horizontal = vertical = Vector3.zero; }
        public bool Fit(IReadOnlyList<Vector2> samples)
        {
            Reset();
            if (samples == null || samples.Count != 5) return false;
            double[,] normal = new double[3, 3];
            double[] bx = new double[3], by = new double[3];
            for (int i = 0; i < 5; i++)
            {
                if (!Finite(samples[i])) return false;
                double[] row = { samples[i].x, samples[i].y, 1d };
                for (int r = 0; r < 3; r++)
                {
                    bx[r] += row[r] * Targets[i].x;
                    by[r] += row[r] * Targets[i].y;
                    for (int c = 0; c < 3; c++) normal[r, c] += row[r] * row[c];
                }
            }
            if (!Solve(normal, bx, out horizontal) || !Solve(normal, by, out vertical)) return false;
            for (int i = 0; i < 5; i++) Error += Vector2.Distance(MapUnclamped(samples[i]), Targets[i]);
            Error /= 5f;
            IsCalibrated = Error < .13f && (samples[1] - samples[2]).magnitude > .015f && (samples[3] - samples[4]).magnitude > .01f;
            return IsCalibrated;
        }
        public Vector2 Map(Vector2 raw)
        {
            Vector2 p = IsCalibrated ? MapUnclamped(raw) : new Vector2(.5f - (raw.x - .5f) * 3f, .5f - (raw.y - .5f) * 2f);
            return new Vector2(Mathf.Clamp01(p.x), Mathf.Clamp01(p.y));
        }
        private Vector2 MapUnclamped(Vector2 raw) => new Vector2(Vector3.Dot(horizontal, new Vector3(raw.x, raw.y, 1f)), Vector3.Dot(vertical, new Vector3(raw.x, raw.y, 1f)));
        public static bool Finite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
        public static Vector2 Smooth(Vector2 previous, Vector2 next, float deltaTime, float smoothing, float deadzone)
        {
            if (!Finite(next) || Vector2.Distance(previous, next) < deadzone) return previous;
            return Vector2.Lerp(previous, next, 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / Mathf.Max(.001f, smoothing)));
        }
        private static bool Solve(double[,] matrix, double[] rhs, out Vector3 result)
        {
            double[,] a = new double[3, 4];
            for (int r = 0; r < 3; r++) { for (int c = 0; c < 3; c++) a[r, c] = matrix[r, c]; a[r, 3] = rhs[r]; }
            for (int i = 0; i < 3; i++)
            {
                int pivot = i;
                for (int r = i + 1; r < 3; r++) if (Math.Abs(a[r, i]) > Math.Abs(a[pivot, i])) pivot = r;
                if (Math.Abs(a[pivot, i]) < 1e-8) { result = default; return false; }
                for (int c = i; c < 4; c++) { double t = a[i, c]; a[i, c] = a[pivot, c]; a[pivot, c] = t; }
                double divisor = a[i, i];
                for (int c = i; c < 4; c++) a[i, c] /= divisor;
                for (int r = 0; r < 3; r++) if (r != i) { double factor = a[r, i]; for (int c = i; c < 4; c++) a[r, c] -= factor * a[i, c]; }
            }
            result = new Vector3((float)a[0, 3], (float)a[1, 3], (float)a[2, 3]);
            return true;
        }
    }
}
