using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// Pure static utility for computing cube rotation symmetry.
    /// All 256 cube configurations can be reduced to ~23 canonical cases
    /// via the 24 unique rotational symmetries of a cube.
    /// </summary>
    public static class CubeSymmetry
    {
        private static readonly Vector3 s_center = new Vector3(0.5f, 0.5f, 0.5f);
        private const float k_snapThreshold = 0.01f;

        /// <summary>
        /// Returns the 8 D4 transforms: 4 Y-axis rotations × (no flip / LR flip).
        /// "Flip" = mirror across x=0.5 plane (scale.x = -1 applied to mesh).
        /// Convention: flip is applied FIRST, then rotation.
        /// </summary>
        private static void GetD4Transforms(out Quaternion[] rotations, out bool[] flips)
        {
            rotations = new Quaternion[]
            {
                Quaternion.Euler(0f,   0f, 0f),
                Quaternion.Euler(0f,  90f, 0f),
                Quaternion.Euler(0f, 180f, 0f),
                Quaternion.Euler(0f, 270f, 0f),
                Quaternion.Euler(0f,   0f, 0f),
                Quaternion.Euler(0f,  90f, 0f),
                Quaternion.Euler(0f, 180f, 0f),
                Quaternion.Euler(0f, 270f, 0f),
            };
            flips = new bool[] { false, false, false, false, true, true, true, true };
        }

        /// <summary>
        /// For each vertex Vi, optionally mirror then rotate it around cube center
        /// and find which Vj it maps to. Returns array where perm[i] = j.
        /// When flip=true, LR mirror permutation (V0-V1, V2-V3, V4-V5, V6-V7) is applied first.
        /// </summary>
        public static int[] GetVertexPermutation(Quaternion rotation, bool flip)
        {
            // LR mirror permutation: V0<->V1, V2<->V3, V4<->V5, V6<->V7
            int[] mirrorPerm = new int[] { 1, 0, 3, 2, 5, 4, 7, 6 };

            int[] perm = new int[8];
            for (int i = 0; i < 8; i++)
            {
                int src = flip ? mirrorPerm[i] : i;
                var v = CubeTable.Vertices[src];
                Vector3 pos = new Vector3(v.x, v.y, v.z);
                Vector3 rotated = RotateAroundCenter(pos, rotation);
                perm[i] = FindNearestVertex(rotated);
            }
            return perm;
        }

        /// <summary>
        /// Applies a vertex permutation to a cube index bit mask.
        /// If bit i is set in cubeIndex, bit perm[i] is set in the result.
        /// </summary>
        public static int ApplyPermutation(int cubeIndex, int[] perm)
        {
            int result = 0;
            for (int i = 0; i < 8; i++)
            {
                if ((cubeIndex & (1 << i)) != 0)
                    result |= 1 << perm[i];
            }
            return result;
        }

        /// <summary>
        /// For each cube index 0-255, finds the minimum equivalent index (canonical)
        /// under all 8 D4 transforms (4 Y rotations × no-flip/LR-flip).
        /// Stores the FORWARD transform (canonical → ci): apply (rotation, flip) to the
        /// canonical prefab to produce the shape for ci.
        /// </summary>
        public static void ComputeSymmetryTable(
            out int[] canonicalIndex,
            out Quaternion[] canonicalRotation,
            out bool[] canonicalFlipped)
        {
            GetD4Transforms(out Quaternion[] rotations, out bool[] flips);
            Debug.Log($"[CubeSymmetry] D4 transforms: {rotations.Length}");

            int[][] perms = new int[rotations.Length][];
            for (int r = 0; r < rotations.Length; r++)
                perms[r] = GetVertexPermutation(rotations[r], flips[r]);

            canonicalIndex   = new int[256];
            canonicalRotation = new Quaternion[256];
            canonicalFlipped  = new bool[256];

            for (int ci = 0; ci < 256; ci++)
            {
                int bestIndex = ci;
                Quaternion bestRot = Quaternion.identity;
                bool bestFlip = false;

                for (int r = 0; r < rotations.Length; r++)
                {
                    int mapped = ApplyPermutation(ci, perms[r]);
                    if (mapped < bestIndex)
                    {
                        bestIndex = mapped;
                        // Store the forward transform: maps canonical back to ci.
                        // Apply transform[r] to canonical to get ci.
                        bestRot = rotations[r];
                        bestFlip = flips[r];
                    }
                }

                canonicalIndex[ci]    = bestIndex;
                canonicalRotation[ci] = bestRot;
                canonicalFlipped[ci]  = bestFlip;
            }

            var canonicals = new HashSet<int>();
            for (int ci = 0; ci < 256; ci++)
                canonicals.Add(canonicalIndex[ci]);
            Debug.Log($"[CubeSymmetry] Canonical case count: {canonicals.Count}");
        }

        // --- private helpers ---

        private static Vector3 RotateAroundCenter(Vector3 pos, Quaternion rotation)
        {
            return rotation * (pos - s_center) + s_center;
        }

        private static int FindNearestVertex(Vector3 rotated)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int j = 0; j < 8; j++)
            {
                var v = CubeTable.Vertices[j];
                Vector3 vPos = new Vector3(v.x, v.y, v.z);
                float dist = (rotated - vPos).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = j;
                }
            }
            return best;
        }

        private static string PermutationSignature(int[] perm)
        {
            // Build a compact string "p0p1p2p3p4p5p6p7"
            return string.Concat(
                perm[0], perm[1], perm[2], perm[3],
                perm[4], perm[5], perm[6], perm[7]);
        }
    }
}
