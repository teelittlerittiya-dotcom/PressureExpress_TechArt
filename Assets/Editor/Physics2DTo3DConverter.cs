using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace PressureExpress.EditorTools
{
    /// <summary>
    /// Converts 2D physics components on prefabs and scenes to their 3D counterparts for the
    /// 2.5D port (2D sprites, 3D physics).
    ///
    /// Why this exists as a tool rather than hand-edited YAML: a Collider2D reference cannot
    /// deserialize into a Collider field, so every Inspector slot that pointed at a 2D
    /// component silently becomes null when the script field type changes. This tool records
    /// those references before the swap and re-assigns them afterwards.
    ///
    /// It refuses to half-convert. Anything it cannot do safely - a Rigidbody2D held alive by
    /// another component's [RequireComponent], a TilemapCollider2D, a collider whose shape it
    /// cannot measure - is left untouched and reported, never silently dropped.
    ///
    /// Open via Tools &gt; Physics 2D to 3D Converter.
    /// </summary>
    public class Physics2DTo3DConverter : EditorWindow
    {
        private const string MaterialFolder = "Assets/PhysicsMaterial";
        private const string Material3DPath = MaterialFolder + "/NoFriction.physicsMaterial";

        /// <summary>Layer 14 in TagManager.asset. Matches the controllers' oneWayPlatformLayer default.</summary>
        private const string OneWayPlatformLayerName = "Platform";

        private float colliderDepth = 2f;
        private float triggerDepth = 20f;

        private bool freezeZPosition = true;
        private bool includePrefabs = true;
        private bool includeScenes = false;
        private string pathFilter = "Assets/Prefab";

        private Vector2 scroll;
        private string lastReport = "";

        [MenuItem("Tools/Physics 2D to 3D Converter")]
        private static void Open()
        {
            GetWindow<Physics2DTo3DConverter>("2D to 3D Physics").minSize = new Vector2(480, 460);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Converts Rigidbody2D / Collider2D components to 3D and re-assigns the Inspector " +
                "references that the C# field type changes broke.\n\n" +
                "COMMIT FIRST. Scenes and prefabs are modified in place and there is no Undo.",
                MessageType.Warning);

            EditorGUILayout.Space();
            colliderDepth = EditorGUILayout.FloatField(
                new GUIContent("Collider Z depth",
                    "A flat (zero-depth) 3D collider behaves badly in PhysX and breaks Bounds.Contains."),
                colliderDepth);
            triggerDepth = EditorGUILayout.FloatField(
                new GUIContent("Trigger Z depth", "Trigger volumes must comfortably contain the play plane."),
                triggerDepth);
            freezeZPosition = EditorGUILayout.Toggle("Freeze Z on Rigidbodies", freezeZPosition);

            EditorGUILayout.Space();
            pathFilter = EditorGUILayout.TextField(
                new GUIContent("Search folder", "Keep this narrow so third-party demo assets are not touched."),
                pathFilter);
            includePrefabs = EditorGUILayout.Toggle("Process prefabs", includePrefabs);
            includeScenes = EditorGUILayout.Toggle("Process scenes", includeScenes);

            EditorGUILayout.Space();
            if (GUILayout.Button("1. Scan only (no changes)", GUILayout.Height(26)))
                lastReport = Run(dryRun: true);

            if (GUILayout.Button("2. Create 3D NoFriction material", GUILayout.Height(26)))
                lastReport = CreateMaterial();

            GUI.backgroundColor = new Color(1f, 0.75f, 0.75f);
            if (GUILayout.Button("3. CONVERT", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog(
                        "Convert 2D physics to 3D?",
                        "This rewrites prefabs and scenes in place, with no Undo. " +
                        "Make sure your work is committed.",
                        "Convert", "Cancel"))
                {
                    lastReport = Run(dryRun: false);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3D physics material
        // ─────────────────────────────────────────────────────────────────────

        private static string CreateMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Material3DPath) != null)
                return $"{Material3DPath} already exists.";

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets", "PhysicsMaterial");

            // Mirrors NoFriction.physicsMaterial2D (friction 0, bounciness 0, combine Minimum).
            PhysicsMaterial mat = new PhysicsMaterial("NoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            AssetDatabase.CreateAsset(mat, Material3DPath);
            AssetDatabase.SaveAssets();
            return $"Created {Material3DPath}.\nAssign it to MapGenerate.mapPhysicsMaterial in " +
                   "MainLevel.unity and Development/Sonar.unity.";
        }

        // ─────────────────────────────────────────────────────────────────────
        // Driver
        // ─────────────────────────────────────────────────────────────────────

        private string Run(bool dryRun)
        {
            StringBuilder log = new StringBuilder();
            log.AppendLine(dryRun ? "=== SCAN (no changes written) ===" : "=== CONVERT ===");

            string[] searchFolders = { string.IsNullOrWhiteSpace(pathFilter) ? "Assets" : pathFilter.Trim() };
            if (!AssetDatabase.IsValidFolder(searchFolders[0]))
                return $"'{searchFolders[0]}' is not a valid folder.";

            int changedAssets = 0;

            if (includePrefabs)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", searchFolders))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject contents = null;
                    try
                    {
                        contents = PrefabUtility.LoadPrefabContents(path);
                        StringBuilder assetLog = new StringBuilder();
                        int changes = ConvertHierarchy(new[] { contents }, assetLog, dryRun, out int rewired);

                        // Save when only references changed too: a prefab whose sole edit is a
                        // re-wired Inspector slot would otherwise have that edit thrown away by
                        // UnloadPrefabContents - the exact failure this tool exists to prevent.
                        if (changes > 0 || rewired > 0)
                        {
                            changedAssets++;
                            log.AppendLine($"\n[PREFAB] {path}");
                            log.Append(assetLog);

                            if (!dryRun && !PrefabUtility.SaveAsPrefabAsset(contents, path))
                                log.AppendLine("  ERROR: SaveAsPrefabAsset FAILED - changes discarded.");
                        }
                    }
                    catch (System.Exception e)
                    {
                        log.AppendLine($"\n[PREFAB] {path}\n  FAILED: {e.Message}");
                    }
                    finally
                    {
                        if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                    }
                }
            }

            if (includeScenes)
            {
                if (!dryRun && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return "Cancelled: unsaved scene changes.";

                string originalScene = SceneManager.GetActiveScene().path;

                foreach (string guid in AssetDatabase.FindAssets("t:Scene", searchFolders))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    try
                    {
                        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        if (!scene.IsValid() || !scene.isLoaded)
                        {
                            log.AppendLine($"\n[SCENE] {path}\n  SKIPPED: failed to load.");
                            continue;
                        }

                        StringBuilder assetLog = new StringBuilder();
                        int changes = ConvertHierarchy(scene.GetRootGameObjects(), assetLog, dryRun, out int rewired);
                        if (changes > 0 || rewired > 0)
                        {
                            changedAssets++;
                            log.AppendLine($"\n[SCENE] {path}");
                            log.Append(assetLog);
                            if (!dryRun)
                            {
                                EditorSceneManager.MarkSceneDirty(scene);
                                EditorSceneManager.SaveScene(scene);
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        log.AppendLine($"\n[SCENE] {path}\n  FAILED: {e.Message}");
                    }
                }

                // Put the user back where they started.
                if (!string.IsNullOrEmpty(originalScene))
                    EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            log.AppendLine($"\n=== {changedAssets} asset(s) {(dryRun ? "would be" : "were")} affected ===");
            return log.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Conversion
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>A reference that pointed at a 2D physics component and must be re-wired.</summary>
        private struct PendingRewire
        {
            public Component Owner;
            public string PropertyPath;
            public GameObject Target;
            public bool WantsRigidbody;
        }

        private int ConvertHierarchy(GameObject[] roots, StringBuilder log, bool dryRun, out int rewiresApplied)
        {
            List<PendingRewire> rewires = new List<PendingRewire>();
            int changes = 0;
            rewiresApplied = 0;

            // Pass 1 - record every serialized reference to a 2D physics component, so the
            // Inspector slot can be restored to the 3D replacement afterwards.
            foreach (GameObject root in roots)
                CollectRewires(root, rewires);

            // Pass 2 - swap the components.
            foreach (GameObject root in roots)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    changes += ConvertGameObject(t.gameObject, log, dryRun);
            }

            // Pass 3 - restore the references.
            if (!dryRun)
            {
                foreach (PendingRewire r in rewires)
                {
                    if (r.Owner == null || r.Target == null) continue;

                    Object replacement = r.WantsRigidbody
                        ? (Object)r.Target.GetComponent<Rigidbody>()
                        : r.Target.GetComponent<Collider>();
                    if (replacement == null)
                    {
                        log.AppendLine($"    NOT re-wired: {r.Owner.GetType().Name}.{r.PropertyPath} " +
                                       $"- '{r.Target.name}' has no 3D replacement. Slot left NULL.");
                        continue;
                    }

                    SerializedObject so = new SerializedObject(r.Owner);
                    SerializedProperty prop = so.FindProperty(r.PropertyPath);
                    if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    prop.objectReferenceValue = replacement;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    // Unity type-checks on apply and silently stores null on a mismatch - e.g.
                    // a SphereCollider into a field declared as BoxCollider, or any 3D collider
                    // into a field still declared Collider2D. Confirm it actually took.
                    so.Update();
                    SerializedProperty verify = so.FindProperty(r.PropertyPath);
                    if (verify == null || verify.objectReferenceValue != replacement)
                    {
                        log.AppendLine($"    ERROR: {r.Owner.GetType().Name}.{r.PropertyPath} would not " +
                                       $"accept {replacement.GetType().Name} (field type mismatch). " +
                                       "Slot left NULL - fix the field type or assign by hand.");
                        continue;
                    }

                    rewiresApplied++;
                    log.AppendLine($"    re-wired {r.Owner.GetType().Name}.{r.PropertyPath} " +
                                   $"-> {replacement.GetType().Name} on '{r.Target.name}'");
                }
            }
            else if (rewires.Count > 0)
            {
                log.AppendLine($"    {rewires.Count} Inspector reference(s) would be re-wired");
            }

            return changes;
        }

        private static void CollectRewires(GameObject root, List<PendingRewire> rewires)
        {
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;

                SerializedObject so = new SerializedObject(mb);
                SerializedProperty prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;

                    Object value = prop.objectReferenceValue;
                    if (value is Collider2D col2d)
                    {
                        rewires.Add(new PendingRewire
                        {
                            Owner = mb,
                            PropertyPath = prop.propertyPath,
                            Target = col2d.gameObject,
                            WantsRigidbody = false
                        });
                    }
                    else if (value is Rigidbody2D rb2d)
                    {
                        rewires.Add(new PendingRewire
                        {
                            Owner = mb,
                            PropertyPath = prop.propertyPath,
                            Target = rb2d.gameObject,
                            WantsRigidbody = true
                        });
                    }
                }
            }
        }

        private int ConvertGameObject(GameObject go, StringBuilder log, bool dryRun)
        {
            // Nested prefab instances are owned by their source asset. Converting them here
            // would write added/removed-component overrides that duplicate the components once
            // the source prefab is converted in its own pass.
            if (PrefabUtility.IsPartOfPrefabInstance(go)) return 0;

            int changes = 0;

            // A TilemapCollider2D cannot be auto-converted - its shape comes from tile data.
            // If one is present, leave the whole object alone rather than stripping the
            // effector and saving a platform that has no collider the 3D player can touch.
            bool hasUnconvertibleCollider = go.GetComponent<TilemapCollider2D>() != null;
            if (hasUnconvertibleCollider)
            {
                log.AppendLine($"  '{go.name}': SKIPPED - has a TilemapCollider2D. Author 3D " +
                               "BoxColliders over the tilemap by hand, delete the 2D components, " +
                               $"then put the object on the \"{OneWayPlatformLayerName}\" layer if " +
                               "it is a one-way platform.");
                return 0;
            }

            // Effectors first: they depend on a Collider2D and have no 3D equivalent.
            foreach (PlatformEffector2D eff in go.GetComponents<PlatformEffector2D>())
            {
                log.AppendLine($"  '{go.name}': PlatformEffector2D removed (surfaceArc {eff.surfaceArc}, " +
                               $"rotationalOffset {eff.rotationalOffset}). 3D one-way platforms are " +
                               $"driven by Physics.IgnoreCollision - put this object on the " +
                               $"\"{OneWayPlatformLayerName}\" layer so the player controllers pick it up.");
                if (!dryRun) DestroyImmediate(eff, true);
                changes++;
            }

            foreach (BuoyancyEffector2D buoy in go.GetComponents<BuoyancyEffector2D>())
            {
                log.AppendLine($"  '{go.name}': BuoyancyEffector2D removed (surfaceLevel {buoy.surfaceLevel}, " +
                               $"density {buoy.density}). NO 3D EQUIVALENT - buoyancy must be " +
                               "re-implemented by hand.");
                if (!dryRun) DestroyImmediate(buoy, true);
                changes++;
            }

            foreach (Collider2D c in go.GetComponents<Collider2D>())
            {
                if (c is CompositeCollider2D) continue;
                if (ConvertCollider(go, c, log, dryRun)) changes++;
            }

            // Rigidbody last, so collider conversion above still sees a consistent object.
            foreach (Rigidbody2D rb2d in go.GetComponents<Rigidbody2D>())
            {
                if (ConvertRigidbody(go, rb2d, log, dryRun)) changes++;
            }

            return changes;
        }

        private bool ConvertCollider(GameObject go, Collider2D source, StringBuilder log, bool dryRun)
        {
            bool isTrigger = source.isTrigger;
            float depth = isTrigger ? triggerDepth : colliderDepth;
            Vector2 offset = source.offset;
            string sourceName = source.GetType().Name;
            PhysicsMaterial2D material2D = source.sharedMaterial;

            if (dryRun)
            {
                log.AppendLine($"  '{go.name}': {sourceName} -> 3D collider (trigger: {isTrigger})");
                return true;
            }

            Collider created;

            switch (source)
            {
                case BoxCollider2D box:
                {
                    Vector2 size = box.size;
                    if (!TryRemove(go, box, sourceName, log)) return false;
                    BoxCollider b = go.AddComponent<BoxCollider>();
                    b.center = new Vector3(offset.x, offset.y, 0f);
                    b.size = new Vector3(size.x, size.y, depth);
                    created = b;
                    break;
                }
                case CircleCollider2D circle:
                {
                    float radius = circle.radius;
                    if (!TryRemove(go, circle, sourceName, log)) return false;
                    SphereCollider s = go.AddComponent<SphereCollider>();
                    s.center = new Vector3(offset.x, offset.y, 0f);
                    s.radius = radius;
                    created = s;
                    break;
                }
                case CapsuleCollider2D capsule:
                {
                    Vector2 size = capsule.size;
                    bool vertical = capsule.direction == CapsuleDirection2D.Vertical;
                    if (!TryRemove(go, capsule, sourceName, log)) return false;
                    CapsuleCollider c = go.AddComponent<CapsuleCollider>();
                    c.center = new Vector3(offset.x, offset.y, 0f);
                    c.direction = vertical ? 1 : 0;                     // 0 = X, 1 = Y
                    c.radius = vertical ? size.x * 0.5f : size.y * 0.5f;
                    // Both 2D and 3D measure height including the hemispherical caps, so a
                    // squashed capsule (height < 2*radius) is simply clamped by Unity.
                    c.height = vertical ? size.y : size.x;
                    created = c;
                    break;
                }
                default:
                {
                    // Polygon / Edge / anything else: approximate with the bounding box. But a
                    // collider on an inactive object - and pass 2 deliberately walks those -
                    // reports an empty bounds, which would destroy the shape irrecoverably.
                    Bounds b2d = source.bounds;
                    Vector3 lossy = go.transform.lossyScale;
                    float sx = Mathf.Abs(lossy.x) > 1e-4f ? b2d.size.x / Mathf.Abs(lossy.x) : b2d.size.x;
                    float sy = Mathf.Abs(lossy.y) > 1e-4f ? b2d.size.y / Mathf.Abs(lossy.y) : b2d.size.y;

                    if (sx <= 1e-4f || sy <= 1e-4f)
                    {
                        log.AppendLine($"  '{go.name}': {sourceName} reported an empty bounds " +
                                       "(inactive object, or no physics world in this context). " +
                                       "LEFT AS 2D - convert this one by hand.");
                        return false;
                    }

                    Vector3 localCenter = go.transform.InverseTransformPoint(b2d.center);
                    if (!TryRemove(go, source, sourceName, log)) return false;

                    BoxCollider b = go.AddComponent<BoxCollider>();
                    b.center = new Vector3(localCenter.x, localCenter.y, 0f);
                    b.size = new Vector3(sx, sy, depth);
                    created = b;

                    log.AppendLine($"  '{go.name}': {sourceName} approximated by a BoxCollider - " +
                                   "re-author the shape if the outline matters.");
                    break;
                }
            }

            if (created == null) return false;

            created.isTrigger = isTrigger;

            if (material2D != null)
            {
                PhysicsMaterial mat3D = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Material3DPath);
                if (mat3D != null) created.sharedMaterial = mat3D;
                log.AppendLine($"  '{go.name}': PhysicsMaterial2D '{material2D.name}' -> " +
                               (mat3D != null ? Material3DPath : "NOTHING (run step 2 first!)"));
            }

            log.AppendLine($"  '{go.name}': {sourceName} -> {created.GetType().Name} " +
                           $"(trigger: {isTrigger}, depth: {depth})");
            return true;
        }

        private bool ConvertRigidbody(GameObject go, Rigidbody2D source, StringBuilder log, bool dryRun)
        {
            if (dryRun)
            {
                log.AppendLine($"  '{go.name}': Rigidbody2D ({source.bodyType}) -> Rigidbody");
                return true;
            }

            RigidbodyType2D bodyType = source.bodyType;
            float mass = source.mass;
            float gravityScale = source.gravityScale;
            float linearDamping = source.linearDamping;
            float angularDamping = source.angularDamping;
            PhysicsMaterial2D material2D = source.sharedMaterial;
            RigidbodyConstraints2D constraints2D = source.constraints;
            RigidbodyInterpolation2D interpolation2D = source.interpolation;
            CollisionDetectionMode2D detection2D = source.collisionDetectionMode;

            if (!TryRemove(go, source, "Rigidbody2D", log)) return false;

            // Rigidbody is [DisallowMultipleComponent]: AddComponent returns null when one is
            // already present (e.g. a partially converted object from an aborted run).
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            if (rb == null)
            {
                log.AppendLine($"  '{go.name}': ERROR - could not add a Rigidbody.");
                return false;
            }

            rb.mass = mass;
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;
            // 2D Static has no 3D counterpart (a static body is simply "no Rigidbody"),
            // so it maps to kinematic - scripts still find it with GetComponent<Rigidbody>().
            rb.isKinematic = bodyType != RigidbodyType2D.Dynamic;
            rb.useGravity = bodyType == RigidbodyType2D.Dynamic && !Mathf.Approximately(gravityScale, 0f);

            RigidbodyConstraints c = RigidbodyConstraints.None;
            if ((constraints2D & RigidbodyConstraints2D.FreezePositionX) != 0) c |= RigidbodyConstraints.FreezePositionX;
            if ((constraints2D & RigidbodyConstraints2D.FreezePositionY) != 0) c |= RigidbodyConstraints.FreezePositionY;
            // 2.5D: Z is the ONLY axis a 2D body could ever spin on, so X/Y rotation is a new
            // degree of freedom that did not exist before - and a sprite rotated about X or Y
            // goes edge-on and visually disappears. Lock them unconditionally.
            c |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            if ((constraints2D & RigidbodyConstraints2D.FreezeRotation) != 0) c |= RigidbodyConstraints.FreezeRotationZ;
            if (freezeZPosition) c |= RigidbodyConstraints.FreezePositionZ;
            rb.constraints = c;

            rb.interpolation = interpolation2D switch
            {
                RigidbodyInterpolation2D.Interpolate => RigidbodyInterpolation.Interpolate,
                RigidbodyInterpolation2D.Extrapolate => RigidbodyInterpolation.Extrapolate,
                _ => RigidbodyInterpolation.None
            };

            // ContinuousSpeculative rather than ContinuousDynamic: it is far cheaper and,
            // unlike Continuous, is not limited to sweeping against static colliders.
            rb.collisionDetectionMode = detection2D == CollisionDetectionMode2D.Continuous
                ? CollisionDetectionMode.ContinuousSpeculative
                : CollisionDetectionMode.Discrete;

            // Rigidbody2D.sharedMaterial supplies the material to every attached collider;
            // 3D has no equivalent, so push it down to the colliders explicitly.
            if (material2D != null)
            {
                PhysicsMaterial mat3D = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Material3DPath);
                if (mat3D != null)
                {
                    foreach (Collider col in go.GetComponents<Collider>()) col.sharedMaterial = mat3D;
                    log.AppendLine($"  '{go.name}': body material '{material2D.name}' -> {Material3DPath}");
                }
                else
                {
                    log.AppendLine($"  '{go.name}': body material '{material2D.name}' LOST - " +
                                   "run step 2 (Create 3D NoFriction material) first, then re-run.");
                }
            }

            if (!Mathf.Approximately(gravityScale, 0f) && !Mathf.Approximately(gravityScale, 1f))
            {
                log.AppendLine($"  '{go.name}': gravityScale was {gravityScale} - 3D has no per-body " +
                               "gravity scale. Apply extra force by hand if this mattered.");
            }

            log.AppendLine($"  '{go.name}': Rigidbody2D ({bodyType}) -> Rigidbody " +
                           $"(kinematic: {rb.isKinematic}, gravity: {rb.useGravity})");
            return true;
        }

        /// <summary>
        /// DestroyImmediate refuses to remove a component another component depends on via
        /// [RequireComponent] - it logs and leaves it alive. NetworkRigidbody2D requires
        /// Rigidbody2D and sits on Player, PlayerHand and Cargo, so without this check the
        /// tool would add a second body next to the surviving 2D one and report success.
        /// </summary>
        private static bool TryRemove(GameObject go, Component component, string label, StringBuilder log)
        {
            DestroyImmediate(component, true);
            if (component == null) return true;

            log.AppendLine($"  '{go.name}': ERROR - {label} could not be removed; another component " +
                           "requires it (NetworkRigidbody2D requires Rigidbody2D - swap it for " +
                           "NetworkRigidbody first). Object left UNCONVERTED.");
            return false;
        }
    }
}
