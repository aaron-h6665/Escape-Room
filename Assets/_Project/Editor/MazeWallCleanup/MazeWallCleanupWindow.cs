using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MazeWallCleanup
{
    public sealed class MazeWallCleanupWindow : EditorWindow
    {
        enum CleanupScope
        {
            ActiveScene,
            SelectionAndChildren,
            RootAndChildren
        }

        sealed class WallRecord
        {
            public Transform Transform;
            public MazeWallCoordinates Coordinates;
            public float Length;
            public string Path;
        }

        sealed class WallChange
        {
            public WallRecord Wall;
            public MazeWallTarget Target;
            public Vector3 NewPosition;
            public float PositionDelta;
            public float LengthDelta;
        }

        const float MinimumUsableLength = 0.05f;
        const float ChangeEpsilon = 0.0001f;

        CleanupScope scope = CleanupScope.ActiveScene;
        Transform root;
        bool includeInactive = true;
        float tolerance = 0.25f;
        float maximumThickness = 0.5f;
        float minimumHeight = 1f;
        bool drawScenePreview = true;
        Vector2 scrollPosition;
        List<WallRecord> detectedWalls = new List<WallRecord>();
        List<WallChange> changes = new List<WallChange>();
        string statusMessage;
        MessageType statusType = MessageType.Info;

        [MenuItem("Tools/Maze/Wall Cleanup")]
        static void OpenWindow()
        {
            MazeWallCleanupWindow window = GetWindow<MazeWallCleanupWindow>();
            window.titleContent = new GUIContent("Maze Wall Cleanup");
            window.minSize = new Vector2(440f, 430f);
            window.Show();
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += HandleUndoRedo;
            SceneView.duringSceneGui += DrawScenePreview;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            SceneView.duringSceneGui -= DrawScenePreview;
        }

        void OnGUI()
        {
            bool playMode = EditorApplication.isPlayingOrWillChangePlaymode;
            EditorGUILayout.LabelField("Maze Wall Cleanup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Preview groups nearly matching wall coordinates, then calculates new centers and lengths so the wall endpoints meet exactly. Preview does not modify the scene; Apply is a single Undo operation.",
                MessageType.Info);
            if (playMode)
            {
                EditorGUILayout.HelpBox(
                    "Exit Play Mode before previewing or applying cleanup so the tool operates on the saved scene objects.",
                    MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            scope = (CleanupScope)EditorGUILayout.EnumPopup(
                new GUIContent("Search Scope", "Choose where the tool looks for cube-shaped walls."),
                scope);

            if (scope == CleanupScope.RootAndChildren)
            {
                root = (Transform)EditorGUILayout.ObjectField(
                    new GUIContent("Maze Root"),
                    root,
                    typeof(Transform),
                    true);
            }

            includeInactive = EditorGUILayout.Toggle(
                new GUIContent("Include Inactive"),
                includeInactive);
            tolerance = EditorGUILayout.FloatField(
                new GUIContent(
                    "Connection Tolerance",
                    "Coordinates separated by this distance or less are grouped. Start with 0.25 for New Scene 2."),
                tolerance);
            maximumThickness = EditorGUILayout.FloatField(
                new GUIContent("Maximum Wall Thickness"),
                maximumThickness);
            minimumHeight = EditorGUILayout.FloatField(
                new GUIContent("Minimum Wall Height"),
                minimumHeight);
            drawScenePreview = EditorGUILayout.Toggle(
                new GUIContent("Draw Scene Preview"),
                drawScenePreview);

            tolerance = Mathf.Max(0f, tolerance);
            maximumThickness = Mathf.Max(0.001f, maximumThickness);
            minimumHeight = Mathf.Max(0.001f, minimumHeight);

            if (EditorGUI.EndChangeCheck())
            {
                ClearPreview();
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(playMode))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preview Cleanup", GUILayout.Height(30f)))
                    {
                        Analyze();
                    }

                    using (new EditorGUI.DisabledScope(changes.Count == 0))
                    {
                        if (GUILayout.Button("Apply Preview", GUILayout.Height(30f)))
                        {
                            ApplyPreview();
                        }
                    }
                }
            }

            using (new EditorGUI.DisabledScope(detectedWalls.Count == 0))
            {
                if (GUILayout.Button("Select Detected Walls"))
                {
                    Selection.objects = detectedWalls
                        .Select(wall => (UnityEngine.Object)wall.Transform.gameObject)
                        .ToArray();
                }
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }

            if (detectedWalls.Count == 0)
            {
                return;
            }

            float maxMove = changes.Count > 0 ? changes.Max(change => change.PositionDelta) : 0f;
            float maxResize = changes.Count > 0 ? changes.Max(change => Mathf.Abs(change.LengthDelta)) : 0f;
            EditorGUILayout.LabelField(
                "Preview Summary",
                detectedWalls.Count + " wall cubes found; " + changes.Count + " would change.");
            EditorGUILayout.LabelField("Largest center movement", maxMove.ToString("0.###") + " units");
            EditorGUILayout.LabelField("Largest length adjustment", maxResize.ToString("0.###") + " units");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Proposed Changes", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (WallChange change in changes)
            {
                EditorGUILayout.LabelField(
                    change.Wall.Path,
                    "move " + change.PositionDelta.ToString("0.###")
                    + ", length " + FormatSigned(change.LengthDelta));
            }
            EditorGUILayout.EndScrollView();
        }

        void Analyze()
        {
            detectedWalls = FindWalls();
            changes = new List<WallChange>();

            if (detectedWalls.Count == 0)
            {
                statusMessage = "No compatible walls were found. The tool expects cube meshes whose local X axis is the wall length and whose local Z axis is the height.";
                statusType = MessageType.Warning;
                SceneView.RepaintAll();
                Repaint();
                return;
            }

            IReadOnlyList<MazeWallTarget> targets = MazeWallCoordinateMath.BuildTargets(
                detectedWalls.Select(wall => wall.Coordinates).ToList(),
                tolerance);
            Dictionary<int, WallRecord> wallById = detectedWalls.ToDictionary(
                wall => wall.Coordinates.Id);

            foreach (MazeWallTarget target in targets)
            {
                WallRecord wall = wallById[target.Id];
                Vector3 currentPosition = wall.Transform.position;
                Vector3 newPosition = target.Axis == MazeWallAxis.Horizontal
                    ? new Vector3(target.Center, currentPosition.y, target.Line)
                    : new Vector3(target.Line, currentPosition.y, target.Center);
                float positionDelta = Vector3.Distance(currentPosition, newPosition);
                float lengthDelta = target.Length - wall.Length;

                if (positionDelta <= ChangeEpsilon && Mathf.Abs(lengthDelta) <= ChangeEpsilon)
                {
                    continue;
                }

                if (target.Length <= MinimumUsableLength)
                {
                    statusMessage = "Preview was stopped because one proposed wall length was too small. Reduce the connection tolerance.";
                    statusType = MessageType.Error;
                    changes.Clear();
                    SceneView.RepaintAll();
                    Repaint();
                    return;
                }

                changes.Add(new WallChange
                {
                    Wall = wall,
                    Target = target,
                    NewPosition = newPosition,
                    PositionDelta = positionDelta,
                    LengthDelta = lengthDelta
                });
            }

            changes = changes.OrderBy(change => change.Wall.Path).ToList();
            statusMessage = changes.Count == 0
                ? "All detected wall coordinates already agree at this tolerance."
                : "Preview ready. Orange lines show current walls; cyan lines show the proposed result. Inspect the Scene view before applying.";
            statusType = MessageType.Info;
            SceneView.RepaintAll();
            Repaint();
        }

        List<WallRecord> FindWalls()
        {
            IEnumerable<Transform> candidates = GetCandidateTransforms();
            List<WallRecord> walls = new List<WallRecord>();
            HashSet<Transform> visited = new HashSet<Transform>();
            int nextWallId = 0;

            foreach (Transform candidate in candidates)
            {
                if (candidate == null || !visited.Add(candidate))
                {
                    continue;
                }

                if (TryCreateWall(candidate, nextWallId, out WallRecord wall))
                {
                    walls.Add(wall);
                    nextWallId++;
                }
            }

            return walls
                .OrderBy(wall => wall.Path, StringComparer.Ordinal)
                .ToList();
        }

        IEnumerable<Transform> GetCandidateTransforms()
        {
            switch (scope)
            {
                case CleanupScope.ActiveScene:
                    Scene scene = SceneManager.GetActiveScene();
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        return Array.Empty<Transform>();
                    }

                    return scene.GetRootGameObjects()
                        .SelectMany(gameObject => gameObject.GetComponentsInChildren<Transform>(includeInactive));

                case CleanupScope.SelectionAndChildren:
                    return Selection.transforms
                        .SelectMany(transform => transform.GetComponentsInChildren<Transform>(includeInactive));

                case CleanupScope.RootAndChildren:
                    return root != null
                        ? root.GetComponentsInChildren<Transform>(includeInactive)
                        : Array.Empty<Transform>();

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        bool TryCreateWall(Transform candidate, int wallId, out WallRecord wall)
        {
            wall = null;
            MeshFilter meshFilter = candidate.GetComponent<MeshFilter>();
            Renderer renderer = candidate.GetComponent<Renderer>();
            BoxCollider boxCollider = candidate.GetComponent<BoxCollider>();

            if (meshFilter == null
                || meshFilter.sharedMesh == null
                || renderer == null
                || boxCollider == null
                || !string.Equals(meshFilter.sharedMesh.name, "Cube", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Vector3 lengthDirection = candidate.TransformDirection(Vector3.right).normalized;
            Vector3 heightDirection = candidate.TransformDirection(Vector3.forward).normalized;
            float horizontalAlignment = Mathf.Abs(Vector3.Dot(lengthDirection, Vector3.right));
            float verticalAlignment = Mathf.Abs(Vector3.Dot(lengthDirection, Vector3.forward));

            if (Mathf.Abs(Vector3.Dot(heightDirection, Vector3.up)) < 0.999f
                || Mathf.Max(horizontalAlignment, verticalAlignment) < 0.999f)
            {
                return false;
            }

            MazeWallAxis axis = horizontalAlignment >= verticalAlignment
                ? MazeWallAxis.Horizontal
                : MazeWallAxis.Vertical;
            Bounds bounds = renderer.bounds;
            float length = axis == MazeWallAxis.Horizontal ? bounds.size.x : bounds.size.z;
            float thickness = axis == MazeWallAxis.Horizontal ? bounds.size.z : bounds.size.x;

            if (bounds.size.y < minimumHeight
                || thickness > maximumThickness
                || length <= Mathf.Max(MinimumUsableLength, thickness * 2f)
                || Vector3.Distance(bounds.center, candidate.position) > 0.001f)
            {
                return false;
            }

            Vector3 position = candidate.position;
            float center = axis == MazeWallAxis.Horizontal ? position.x : position.z;
            float line = axis == MazeWallAxis.Horizontal ? position.z : position.x;
            wall = new WallRecord
            {
                Transform = candidate,
                Coordinates = new MazeWallCoordinates(
                    wallId,
                    axis,
                    line,
                    center - length * 0.5f,
                    center + length * 0.5f),
                Length = length,
                Path = GetHierarchyPath(candidate)
            };
            return true;
        }

        void ApplyPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                statusMessage = "Exit Play Mode before applying cleanup.";
                statusType = MessageType.Warning;
                return;
            }

            Analyze();
            if (changes.Count == 0)
            {
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Apply Maze Wall Cleanup?",
                "This will move or resize " + changes.Count
                + " wall objects. The operation can be reverted with Undo.",
                "Apply",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Clean Up Maze Walls");
            UnityEngine.Object[] transforms = changes
                .Select(change => (UnityEngine.Object)change.Wall.Transform)
                .ToArray();
            Undo.RecordObjects(transforms, "Clean Up Maze Walls");

            HashSet<Scene> dirtyScenes = new HashSet<Scene>();
            int appliedCount = 0;
            foreach (WallChange change in changes)
            {
                Transform transform = change.Wall.Transform;
                if (transform == null || change.Wall.Length <= MinimumUsableLength)
                {
                    continue;
                }

                Vector3 localScale = transform.localScale;
                localScale.x *= change.Target.Length / change.Wall.Length;
                transform.position = change.NewPosition;
                transform.localScale = localScale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                EditorUtility.SetDirty(transform);
                dirtyScenes.Add(transform.gameObject.scene);
                appliedCount++;
            }

            foreach (Scene scene in dirtyScenes)
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            detectedWalls.Clear();
            changes.Clear();
            statusMessage = "Applied cleanup to " + appliedCount + " walls. Use Edit > Undo or Cmd+Z to revert it, and save the scene only after inspecting the result.";
            statusType = MessageType.Info;
            SceneView.RepaintAll();
            Repaint();
        }

        void DrawScenePreview(SceneView sceneView)
        {
            if (!drawScenePreview || changes.Count == 0)
            {
                return;
            }

            Color previousColor = Handles.color;
            foreach (WallChange change in changes)
            {
                if (change.Wall.Transform == null)
                {
                    continue;
                }

                float y = change.Wall.Transform.position.y;
                GetWorldEndpoints(change.Wall.Coordinates, y, out Vector3 oldStart, out Vector3 oldEnd);
                GetWorldEndpoints(change.Target, y, out Vector3 newStart, out Vector3 newEnd);

                Handles.color = new Color(1f, 0.55f, 0.1f, 0.8f);
                Handles.DrawAAPolyLine(3f, oldStart, oldEnd);
                Handles.color = new Color(0.1f, 0.9f, 1f, 0.95f);
                Handles.DrawAAPolyLine(3f, newStart, newEnd);
            }

            Handles.color = previousColor;
        }

        static void GetWorldEndpoints(
            MazeWallCoordinates wall,
            float y,
            out Vector3 start,
            out Vector3 end)
        {
            if (wall.Axis == MazeWallAxis.Horizontal)
            {
                start = new Vector3(wall.Start, y, wall.Line);
                end = new Vector3(wall.End, y, wall.Line);
            }
            else
            {
                start = new Vector3(wall.Line, y, wall.Start);
                end = new Vector3(wall.Line, y, wall.End);
            }
        }

        static void GetWorldEndpoints(
            MazeWallTarget wall,
            float y,
            out Vector3 start,
            out Vector3 end)
        {
            if (wall.Axis == MazeWallAxis.Horizontal)
            {
                start = new Vector3(wall.Start, y, wall.Line);
                end = new Vector3(wall.End, y, wall.Line);
            }
            else
            {
                start = new Vector3(wall.Line, y, wall.Start);
                end = new Vector3(wall.Line, y, wall.End);
            }
        }

        void HandleUndoRedo()
        {
            ClearPreview();
            statusMessage = "The scene changed through Undo/Redo. Run Preview Cleanup again to refresh the proposed changes.";
            statusType = MessageType.Info;
            Repaint();
        }

        void ClearPreview()
        {
            detectedWalls.Clear();
            changes.Clear();
            statusMessage = null;
            SceneView.RepaintAll();
            Repaint();
        }

        static string GetHierarchyPath(Transform transform)
        {
            List<string> parts = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        static string FormatSigned(float value)
        {
            return value >= 0f
                ? "+" + value.ToString("0.###")
                : value.ToString("0.###");
        }
    }
}
