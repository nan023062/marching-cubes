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
        /// Returns the 4 Y-axis rotations (0/90/180/270 degrees).
        /// Only horizontal rotations are used: up/down are artistically distinct
        /// and must not be treated as equivalent.
        /// </summary>
        public static Quaternion[] Generate24Rotations()
        {
            return new Quaternion[]
            {
                Quaternion.Euler(0f,   0f, 0f),
                Quaternion.Euler(0f,  90f, 0f),
                Quaternion.Euler(0f, 180f, 0f),
                Quaternion.Euler(0f, 270f, 0f),
            };
        }

        /// <summary>
        /// For each vertex Vi, rotate it around cube center and find which Vj it maps to.
        /// Returns array where perm[i] = j.
        /// </summary>
        public static int[] GetVertexPermutation(Quaternion rotation)
        {
            int[] perm = new int[8];
            for (int i = 0; i < 8; i++)
            {
                var v = CubeTable.Vertices[i];
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
        /// under all 24 rotations. Returns canonical indices and the rotation that
        /// maps canonical -> ci (i.e., Inverse of the rotation ci -> canonical).
        /// </summary>
        public static void ComputeSymmetryTable(out int[] canonicalIndex, out Quaternion[] canonicalRotation)
        {
            Quaternion[] rotations = Generate24Rotations();
            Debug.Log($"[CubeSymmetry] Generated {rotations.Length} unique rotations");

            int[][] perms = new int[rotations.Length][];
            for (int r = 0; r < rotations.Length; r++)
                perms[r] = GetVertexPermutation(rotations[r]);

            canonicalIndex = new int[256];
            canonicalRotation = new Quaternion[256];

            for (int ci = 0; ci < 256; ci++)
            {
                int bestIndex = ci;
                Quaternion bestRot = Quaternion.identity;

                for (int r = 0; r < rotations.Length; r++)
                {
                    int mapped = ApplyPermutation(ci, perms[r]);
                    if (mapped < bestIndex)
                    {
                        bestIndex = mapped;
                        bestRot = rotations[r];
                    }
                }

                canonicalIndex[ci] = bestIndex;
                // Store inverse: rotation that takes canonical prefab and places it as ci
                canonicalRotation[ci] = Quaternion.Inverse(bestRot);
            }

            // Count distinct canonical cases
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
