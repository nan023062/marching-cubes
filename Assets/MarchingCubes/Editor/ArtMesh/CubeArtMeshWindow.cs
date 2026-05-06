using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// Editor tool for configuring art-mesh prefab assignments for all 256 cube configurations.
    /// Computes rotational symmetry, allows per-slot prefab assignment, and generates 256 prefabs.
    /// </summary>
    public sealed class CubeArtMeshWindow : EditorWindow
    {
        // --- constants ---
        private const string OutputDir = "Assets/MarchingCubes/Sample/Resources/ArtCubeMesh";
        private const float DetailPanelWidth = 260f;
        private const float CellSize256 = 64f;
        private const float CellSizeCanonical = 80f;
        private const int GridColumns = 16;
        private static readonly Vector3 s_cubeCenter = new Vector3(0.5f, 0.5f, 0.5f);

        // --- state ---
        private CubeArtMeshConfig _config;
        private bool _showCanonicalOnly;
        private int _selectedIndex = -1;
        private Vector2 _scrollPos;
        private int _highlightDebugIndex;

        // Preview
        private PreviewRenderUtility _previewUtil;
        private bool _previewUtilFailed;

        [MenuItem("MarchingCubes/Art Mesh Config Tool")]
        public static void Open()
        {
            var window = GetWindow<CubeArtMeshWindow>("Art Mesh Config");
            window.minSize = new Vector2(700, 500);
        }

        private void OnDisable()
        {
            CleanupPreview();
        }

        private void OnDestroy()
        {
            CleanupPreview();
        }

        private void CleanupPreview()
        {
            if (_previewUtil != null)
            {
                _previewUtil.Cleanup();
                _previewUtil = null;
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMainGrid();
                DrawDetailPanel();
            }

            EditorGUILayout.Space(4);
            DrawDebugPanel();
        }

        // -----------------------------------------------------------------------
        // Toolbar
        // -----------------------------------------------------------------------

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                CubeArtMeshConfig newConfig = (CubeArtMeshConfig)EditorGUILayout.ObjectField(
                    _config, typeof(CubeArtMeshConfig), false, GUILayout.Width(240));
                if (newConfig != _config)
                {
                    _config = newConfig;
                    _selectedIndex = -1;
                    CleanupPreview();
                }

                GUILayout.Space(8);

                GUI.enabled = _config != null;

                if (GUILayout.Button("Compute Symmetry", EditorStyles.toolbarButton))
                    ComputeSymmetry();

                if (GUILayout.Button("Validate", EditorStyles.toolbarButton))
                    ValidateConfig();

                GUILayout.Space(8);

                bool allMode = GUILayout.Toggle(!_showCanonicalOnly, "All 256", EditorStyles.toolbarButton);
                bool canonMode = GUILayout.Toggle(_showCanonicalOnly, "Canonical Only", EditorStyles.toolbarButton);
                if (allMode) _showCanonicalOnly = false;
                if (canonMode) _showCanonicalOnly = true;

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Generate 256 Prefabs", EditorStyles.toolbarButton))
                    GeneratePrefabs();

                GUI.enabled = true;
            }
        }

        // -----------------------------------------------------------------------
        // Main grid
        // -----------------------------------------------------------------------

        private void DrawMainGrid()
        {
            float cellSize = _showCanonicalOnly ? CellSizeCanonical : CellSize256;

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPos,
                GUILayout.Width(position.width - DetailPanelWidth - 8f)))
            {
                _scrollPos = scroll.scrollPosition;

                if (_config == null)
                {
                    EditorGUILayout.HelpBox("Select a CubeArtMeshConfig asset.", MessageType.Info);
                    return;
                }

                if (_showCanonicalOnly)
                    DrawCanonicalGrid(cellSize);
                else
                    DrawAll256Grid(cellSize);
            }
        }

        private void DrawAll256Grid(float cellSize)
        {
            int rows = Mathf.CeilToInt(256f / GridColumns);
            // Reserve layout space for the entire grid
            Rect gridRect = GUILayoutUtility.GetRect(GridColumns * cellSize, rows * cellSize);

            for (int i = 0; i < 256; i++)
            {
                int row = i / GridColumns;
                int col = i % GridColumns;
                Rect cellRect = new Rect(
                    gridRect.x + col * cellSize,
                    gridRect.y + row * cellSize,
                    cellSize - 1f,
                    cellSize - 1f);
                DrawCell(cellRect, i, cellSize);
            }
        }

        private void DrawCanonicalGrid(float cellSize)
        {
            var canonicals = new List<int>(30);
            for (int i = 0; i < 256; i++)
            {
                if (_config.IsCanonical(i))
                    canonicals.Add(i);
            }

            int cols = Mathf.Max(1, Mathf.FloorToInt((position.width - DetailPanelWidth - 24f) / cellSize));
            int rows = Mathf.CeilToInt(canonicals.Count / (float)cols);

            Rect gridRect = GUILayoutUtility.GetRect(cols * cellSize, rows * cellSize);

            for (int n = 0; n < canonicals.Count; n++)
            {
                int ci = canonicals[n];
                int row = n / cols;
                int col = n % cols;
                Rect cellRect = new Rect(
                    gridRect.x + col * cellSize,
                    gridRect.y + row * cellSize,
                    cellSize - 1f,
                    cellSize - 1f);
                DrawCell(cellRect, ci, cellSize);
            }
        }

        private void DrawCell(Rect rect, int cubeIndex, float cellSize)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color bg = CellColor(cubeIndex);
                if (cubeIndex == _selectedIndex)
                    bg = Color.cyan;

                EditorGUI.DrawRect(rect, bg);

                // Thumbnail
                if (_config != null)
                {
                    int canonical = _config.GetCanonicalIndex(cubeIndex);
                    var entry = _config.GetEntry(canonical);
                    if (entry != null && entry.prefab != null)
                    {
                        Texture2D thumb = AssetPreview.GetAssetPreview(entry.prefab);
                        if (thumb != null)
                        {
                            float padding = 4f;
                            GUI.DrawTexture(
                                new Rect(rect.x + padding, rect.y + padding,
                                    rect.width - padding * 2f, rect.height - padding * 2f - 14f),
                                thumb, ScaleMode.ScaleToFit);
                        }
                    }
                }

                // Index label
                GUI.Label(new Rect(rect.x + 2f, rect.yMax - 14f, rect.width - 4f, 13f),
                    cubeIndex.ToString(),
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedIndex = cubeIndex;
                CleanupPreview();
                GUI.changed = true;
                Repaint();
            }
        }

        private Color CellColor(int cubeIndex)
        {
            if (_config == null) return new Color(0.3f, 0.3f, 0.3f);

            int canonical = _config.GetCanonicalIndex(cubeIndex);
            var entry = _config.GetEntry(canonical);
            bool hasPrefab = entry != null && entry.prefab != null;

            if (!hasPrefab)
                return new Color(0.6f, 0.15f, 0.15f);   // red = missing

            var ownEntry = _config.GetEntry(cubeIndex);
            if (ownEntry != null && ownEntry.isManualOverride)
                return new Color(0.1f, 0.55f, 0.1f);    // green = manual override

            return new Color(0.5f, 0.45f, 0.1f);        // yellow = auto-derived
        }

        // -----------------------------------------------------------------------
        // Detail panel
        // -----------------------------------------------------------------------

        private void DrawDetailPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(DetailPanelWidth)))
            {
                if (_config == null || _selectedIndex < 0)
                {
                    EditorGUILayout.HelpBox("Select a cell.", MessageType.None);
                    return;
                }

                int ci = _selectedIndex;
                int canonical = _config.GetCanonicalIndex(ci);
                Quaternion rot = _config.GetRotation(ci);
                Vector3 euler = rot.eulerAngles;

                EditorGUILayout.LabelField($"Case: {ci}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Canonical: {canonical}");
                EditorGUILayout.LabelField($"Rotation: ({euler.x:F0}, {euler.y:F0}, {euler.z:F0})");

                EditorGUILayout.Space(6);

                // Prefab field on the canonical slot
                var canonicalEntry = _config.GetEntry(canonical);
                if (canonicalEntry != null)
                {
                    EditorGUI.BeginChangeCheck();
                    GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(
                        "Prefab", canonicalEntry.prefab, typeof(GameObject), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        canonicalEntry.prefab = newPrefab;
                        EditorUtility.SetDirty(_config);
                        CleanupPreview();
                    }
                }

                // Manual override on this specific slot
                var ownEntry = _config.GetEntry(ci);
                if (ownEntry != null)
                {
                    EditorGUI.BeginChangeCheck();
                    bool newOverride = EditorGUILayout.Toggle("Manual Override", ownEntry.isManualOverride);
                    if (EditorGUI.EndChangeCheck())
                    {
                        ownEntry.isManualOverride = newOverride;
                        EditorUtility.SetDirty(_config);
                    }
                }

                if (GUILayout.Button("Clear"))
                {
                    if (canonicalEntry != null)
                    {
                        canonicalEntry.prefab = null;
                        canonicalEntry.isManualOverride = false;
                    }
                    if (ownEntry != null)
                        ownEntry.isManualOverride = false;

                    EditorUtility.SetDirty(_config);
                    CleanupPreview();
                }

                EditorGUILayout.Space(8);

                // Preview area 160x160
                Rect previewRect = GUILayoutUtility.GetRect(160f, 160f);
                DrawPreview(previewRect, canonical, rot);
            }
        }

        private void DrawPreview(Rect rect, int canonical, Quaternion rotation)
        {
            if (_config == null) return;

            var entry = _config.GetEntry(canonical);
            if (entry == null || entry.prefab == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
                GUI.Label(rect, "No prefab", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (!_previewUtilFailed)
            {
                bool rendered = TryRenderPreview(rect, entry.prefab, rotation);
                if (!rendered)
                    _previewUtilFailed = true;
            }

            if (_previewUtilFailed)
            {
                // Fallback: static thumbnail + rotation text
                Texture2D thumb = AssetPreview.GetAssetPreview(entry.prefab);
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
                if (thumb != null)
                    GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, rect.height - 20f), thumb, ScaleMode.ScaleToFit);

                Vector3 euler = rotation.eulerAngles;
                GUI.Label(new Rect(rect.x, rect.yMax - 20f, rect.width, 20f),
                    $"Rot: ({euler.x:F0},{euler.y:F0},{euler.z:F0})",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        private bool TryRenderPreview(Rect rect, GameObject prefab, Quaternion rotation)
        {
            try
            {
                if (_previewUtil == null)
                    _previewUtil = new PreviewRenderUtility();

                _previewUtil.BeginPreview(rect, GUIStyle.none);

                // Setup camera
                Camera cam = _previewUtil.camera;
                cam.transform.position = new Vector3(0.5f, 0.5f, -3f);
                cam.transform.rotation = Quaternion.identity;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 20f;

                // Manually instantiate and add to preview scene
                GameObject instance = Object.Instantiate(prefab);
                if (instance != null)
                {
                    instance.transform.localPosition = s_cubeCenter - rotation * s_cubeCenter;
                    instance.transform.localRotation = rotation;
                    instance.transform.localScale = Vector3.one;
                    _previewUtil.AddSingleGO(instance);
                }

                _previewUtil.lights[0].intensity = 1f;
                _previewUtil.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);

                _previewUtil.camera.Render();

                Texture result = _previewUtil.EndPreview();
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);

                if (instance != null)
                    Object.DestroyImmediate(instance);

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CubeArtMeshWindow] PreviewRenderUtility failed: " + ex.Message);
                CleanupPreview();
                return false;
            }
        }

        // -----------------------------------------------------------------------
        // Debug panel
        // -----------------------------------------------------------------------

        private void DrawDebugPanel()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Debug Highlight Index", GUILayout.Width(150));
                _highlightDebugIndex = EditorGUILayout.IntField(_highlightDebugIndex, GUILayout.Width(60));
                if (GUILayout.Button("Apply to Scene", GUILayout.Width(120)))
                    ApplyHighlightToScene();
            }
        }

        private void ApplyHighlightToScene()
        {
            // ArtMeshBuilding is implemented in a later task; find by type name to avoid hard dependency
            var allObjects = Object.FindObjectsOfType<MonoBehaviour>();
            int applied = 0;
            foreach (var mb in allObjects)
            {
                System.Type t = mb.GetType();
                if (t.Name == "ArtMeshBuilding")
                {
                    System.Reflection.FieldInfo field = t.GetField("debugHighlightIndex",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        field.SetValue(mb, _highlightDebugIndex);
                        EditorUtility.SetDirty(mb);
                        applied++;
                    }
                }
            }

            if (applied == 0)
                Debug.Log("[CubeArtMeshWindow] No ArtMeshBuilding found in scene (silent skip).");
            else
                Debug.Log($"[CubeArtMeshWindow] Applied debugHighlightIndex={_highlightDebugIndex} to {applied} ArtMeshBuilding(s).");
        }

        // -----------------------------------------------------------------------
        // Actions
        // -----------------------------------------------------------------------

        private void ComputeSymmetry()
        {
            if (_config == null) return;

            CubeSymmetry.ComputeSymmetryTable(out int[] canonicalIndex, out Quaternion[] canonicalRotation);
            _config.SetSymmetryData(canonicalIndex, canonicalRotation);
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        private void ValidateConfig()
        {
            if (_config == null) return;

            int missingCanonical = 0;
            var missingList = new List<int>();

            for (int i = 0; i < 256; i++)
            {
                if (_config.IsCanonical(i) && !_config.HasEntry(i))
                {
                    missingCanonical++;
                    missingList.Add(i);
                }
            }

            if (missingCanonical == 0)
                Debug.Log("[CubeArtMeshWindow] Validate: All canonical cases have prefabs assigned.");
            else
                Debug.LogWarning($"[CubeArtMeshWindow] Validate: {missingCanonical} canonical case(s) missing prefab: [{string.Join(", ", missingList)}]");
        }

        private void GeneratePrefabs()
        {
            if (_config == null) return;

            EnsureOutputDir();

            int generated = 0;
            for (int i = 0; i < 256; i++)
            {
                if (i == 0) continue; // empty case

                if (!_config.TryGetEntry(i, out GameObject prefab, out Quaternion rotation))
                    continue;

                string path = $"{OutputDir}/cm_art_{i}.prefab";

                // Build wrapper
                GameObject wrapper = new GameObject($"cm_art_{i}");
                GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                child.transform.SetParent(wrapper.transform);

                // Apply rotation: child.localPosition = center - rotation * center
                child.transform.localPosition = s_cubeCenter - rotation * s_cubeCenter;
                child.transform.localRotation = rotation;
                child.transform.localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(wrapper, path);
                Object.DestroyImmediate(wrapper);

                generated++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[CubeArtMeshWindow] Generated {generated} art cube prefabs in {OutputDir}/");
        }

        private static void EnsureOutputDir()
        {
            if (!AssetDatabase.IsValidFolder(OutputDir))
            {
                // Create recursively
                string parent = "Assets/MarchingCubes/Sample/Resources";
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    string sampleParent = "Assets/MarchingCubes/Sample";
                    if (!AssetDatabase.IsValidFolder(sampleParent))
                        AssetDatabase.CreateFolder("Assets/MarchingCubes", "Sample");
                    AssetDatabase.CreateFolder(sampleParent, "Resources");
                }
                AssetDatabase.CreateFolder(parent, "ArtCubeMesh");
            }
        }
    }
}
