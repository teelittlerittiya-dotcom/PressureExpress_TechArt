using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace PressureExpress.EditorTools
{
    /// <summary>
    /// Validates the rigid-hand, server-authoritative weighted holding setup without entering Play Mode.
    /// </summary>
    public static class WeightedHoldingValidator
    {
        public const string ConfigurationPath = "Assets/Data/Holding/Default Grip Configuration.asset";
        public const string PlayerPrefabPath = "Assets/Prefab/Player/Player.prefab";
        public const string HandPrefabPath = "Assets/Prefab/Player/PlayerHand.prefab";
        public const string CargoPrefabPath = "Assets/Prefab/Cargo/CargoController (new).prefab";
        public const string GrabControllerPath = "Assets/Script/Player/CargoGrabController.cs";

        [MenuItem("Tools/Cargo/Validate Weighted Holding")]
        public static void ValidateFromMenu()
        {
            bool valid = ValidateAll(out string report);
            if (valid) Debug.Log(report);
            else Debug.LogError(report);
        }

        public static bool ValidateAll(out string report)
        {
            List<string> errors = new List<string>();
            List<string> notes = new List<string>();

            GripConfiguration configuration = AssetDatabase.LoadAssetAtPath<GripConfiguration>(ConfigurationPath);
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject hand = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
            GameObject cargo = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPrefabPath);

            if (configuration == null) errors.Add($"configuration missing at {ConfigurationPath}");
            else if (!configuration.ValidateConfiguration(out string configurationError))
                errors.Add($"configuration invalid: {configurationError}");
            else
                notes.Add("shared GripConfiguration is valid");

            ValidatePlayer(player, configuration, errors, notes);
            ValidateHand(hand, errors, notes);
            ValidateCargo(cargo, configuration, errors, notes);
            ValidateCollisionMatrix(hand, cargo, errors, notes);
            ValidateLegacyJointRemoval(errors, notes);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Weighted holding validator: {(errors.Count == 0 ? "PASS" : "FAIL")}");
            foreach (string note in notes) builder.AppendLine($"  OK: {note}");
            foreach (string error in errors) builder.AppendLine($"  ERROR: {error}");
            report = builder.ToString();
            return errors.Count == 0;
        }

        private static void ValidatePlayer(
            GameObject player,
            GripConfiguration configuration,
            List<string> errors,
            List<string> notes)
        {
            if (player == null)
            {
                errors.Add($"player prefab missing at {PlayerPrefabPath}");
                return;
            }

            CargoGrabController coordinator = player.GetComponent<CargoGrabController>();
            CursorIntentProvider intent = player.GetComponent<CursorIntentProvider>();
            if (coordinator == null) errors.Add("Player is missing CargoGrabController");
            else if (!coordinator.ValidateConfiguration(out string coordinatorError))
                errors.Add($"CargoGrabController invalid: {coordinatorError}");
            if (intent == null) errors.Add("Player is missing CursorIntentProvider");

            if (coordinator != null && coordinator.Configuration != configuration)
                errors.Add("Player coordinator does not reference the shared GripConfiguration");
            if (intent != null && intent.Configuration != configuration)
                errors.Add("CursorIntentProvider does not reference the shared GripConfiguration");

            if (player.GetComponent<NetworkObject>() == null)
                errors.Add("Player has no NetworkObject for replicated holder state");
            else
                notes.Add("Player owns cursor input and a replicated server-write holder record");
        }

        private static void ValidateHand(GameObject hand, List<string> errors, List<string> notes)
        {
            if (hand == null)
            {
                errors.Add($"hand prefab missing at {HandPrefabPath}");
                return;
            }

            if (hand.GetComponent<PlayerHand>() == null) errors.Add("Hand is missing PlayerHand");
            if (hand.GetComponent<NetworkObject>() == null) errors.Add("Hand is missing NetworkObject");
            if (hand.GetComponentsInChildren<Joint>(true).Length > 0
                || hand.GetComponentsInChildren<Joint2D>(true).Length > 0)
                errors.Add("Hand hierarchy still contains a physics Joint");

            if ((hand.transform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                errors.Add("Hand root scale must stay at (1,1,1); the old controller applied unit scale at runtime, so prefab scale 2 doubles both sprite and hit collider");

            Rigidbody body = hand.GetComponent<Rigidbody>();
            if (body == null)
            {
                errors.Add("Hand is missing Rigidbody");
            }
            else
            {
                RigidbodyConstraints required = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
                if (!body.isKinematic || body.useGravity || (body.constraints & required) != required)
                    errors.Add("Hand Rigidbody must be kinematic, gravity-free, Position-Z/rotation frozen");
                if (body.interpolation != RigidbodyInterpolation.None)
                    errors.Add("Hand Rigidbody interpolation must be None so it cannot lag behind the render-frame cursor writer");
            }

            Collider[] colliders = hand.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
                errors.Add("Hand needs at least one trigger collider for the hit-to-grab query");
            else if (System.Array.Exists(colliders, collider => !collider.isTrigger))
                errors.Add("Every Hand collider must remain a trigger so an idle Hand cannot physically push Cargo");
            else if (System.Array.Exists(colliders, collider => collider.enabled))
                errors.Add("Hand colliders must start disabled; PlayerHand arms them only for an in-range press, an active hold, or server-side remote validation");

            if (hand.GetComponent<NetworkRigidbody>() != null)
                errors.Add("Hand must not use NetworkRigidbody; it fights the always-kinematic cursor presentation");

            NetworkTransform transformSync = hand.GetComponent<NetworkTransform>();
            if (transformSync == null)
            {
                errors.Add("Hand is missing NetworkTransform");
            }
            else
            {
                if (transformSync.AuthorityMode != NetworkTransform.AuthorityModes.Owner)
                    errors.Add("Free Hand NetworkTransform must be owner authoritative");
                if (!transformSync.SyncPositionX || !transformSync.SyncPositionY || transformSync.SyncPositionZ)
                    errors.Add("Hand NetworkTransform must sync Position X/Y only");
                if (transformSync.SyncRotAngleX || transformSync.SyncRotAngleY || transformSync.SyncRotAngleZ)
                    errors.Add("Hand NetworkTransform must not sync rotation");
                if (transformSync.SyncScaleX || transformSync.SyncScaleY || transformSync.SyncScaleZ)
                    errors.Add("Hand NetworkTransform must not sync scale");
            }

            if (errors.Count == 0)
                notes.Add("Hand keeps its legacy runtime size; Cursor/Preview/Holding presentation is separate from trigger-only collider arming");
        }

        private static void ValidateCargo(
            GameObject cargo,
            GripConfiguration configuration,
            List<string> errors,
            List<string> notes)
        {
            if (cargo == null)
            {
                errors.Add($"cargo prefab missing at {CargoPrefabPath}");
                return;
            }

            CargoHoldSolver solver = cargo.GetComponent<CargoHoldSolver>();
            if (solver == null) errors.Add("Cargo is missing CargoHoldSolver");
            else if (!solver.ValidateConfiguration(out string solverError))
                errors.Add($"CargoHoldSolver invalid: {solverError}");
            if (solver != null && solver.Configuration != configuration)
                errors.Add("CargoHoldSolver does not reference the shared GripConfiguration");
            if (cargo.GetComponentsInChildren<Joint>(true).Length > 0
                || cargo.GetComponentsInChildren<Joint2D>(true).Length > 0)
                errors.Add("Cargo prefab contains a legacy physics Joint");

            NetworkTransform networkTransform = cargo.GetComponent<NetworkTransform>();
            if (networkTransform == null
                || networkTransform.AuthorityMode != NetworkTransform.AuthorityModes.Server)
                errors.Add("Cargo transform must remain server authoritative");
            else
                notes.Add("Cargo has a force-at-point solver and server-authoritative transform");
        }

        private static void ValidateCollisionMatrix(
            GameObject hand,
            GameObject cargo,
            List<string> errors,
            List<string> notes)
        {
            if (hand == null || cargo == null) return;
            if (Physics.GetIgnoreLayerCollision(hand.layer, cargo.layer))
                errors.Add("Hand/Cargo layers cannot ignore collision globally: initial grab requires a real contact");
            else
                notes.Add("Hand trigger can query Cargo overlap while producing no collision response");
        }

        private static void ValidateLegacyJointRemoval(List<string> errors, List<string> notes)
        {
            if (!File.Exists(GrabControllerPath))
            {
                errors.Add($"grab controller source missing at {GrabControllerPath}");
                return;
            }

            string source = File.ReadAllText(GrabControllerPath);
            if (source.Contains("SpringJoint") || source.Contains("mouseFollowerRigidbody"))
                errors.Add("CargoGrabController source still contains legacy joint/anchor code");
            else
                notes.Add("legacy SpringJoint creation and mouse-follower anchor fields are removed");
        }
    }
}
