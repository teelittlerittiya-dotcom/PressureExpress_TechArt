using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PressureExpress.Tests.EditMode
{
    public sealed class WeightedHoldingEditModeTests
    {
        private const string ConfigurationPath = "Assets/Data/Holding/Default Grip Configuration.asset";
        private const string PlayerPrefabPath = "Assets/Prefab/Player/Player.prefab";
        private const string HandPrefabPath = "Assets/Prefab/Player/PlayerHand.prefab";
        private const string CargoPrefabPath = "Assets/Prefab/Cargo/CargoController (new).prefab";
        private const string PrototypeCargoDataPath = "Assets/Data/Cargo/Prototype/Cargo Prototype.asset";

        [Test]
        public void ForceModel_ZeroErrorAndVelocityProducesNoForce()
        {
            object result = CalculateForce(Vector2.zero, Vector2.zero, Vector2.zero, 3f, 20f, 3f, 60f, 1.75f);

            Assert.That(GetProperty<Vector2>(result, "Force"), Is.EqualTo(Vector2.zero));
            Assert.That(GetProperty<bool>(result, "ForceClamped"), Is.False);
            Assert.That(GetProperty<bool>(result, "ReachClamped"), Is.False);
        }

        [Test]
        public void ForceModel_DampingOpposesPointVelocityAndClampsForce()
        {
            object damping = CalculateForce(
                Vector2.zero, Vector2.zero, new Vector2(2f, 0f),
                3f, 20f, 3f, 60f, 1.75f);
            object clamped = CalculateForce(
                new Vector2(100f, 0f), Vector2.zero, Vector2.zero,
                3f, 20f, 3f, 30f, 1.75f);

            Vector2 dampingForce = GetProperty<Vector2>(damping, "Force");
            Vector2 clampedForce = GetProperty<Vector2>(clamped, "Force");
            Assert.That(dampingForce.x, Is.LessThan(0f));
            Assert.That(dampingForce.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(clampedForce.magnitude, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(GetProperty<bool>(clamped, "ForceClamped"), Is.True);
            Assert.That(GetProperty<bool>(clamped, "ReachClamped"), Is.True);
        }

        [Test]
        public void EqualGripForce_AcceleratesHeavyCargoLessWithoutMassScalingForce()
        {
            object result = CalculateForce(
                Vector2.right, Vector2.zero, Vector2.zero,
                3f, 20f, 3f, 60f, 1.75f);
            Vector2 force = GetProperty<Vector2>(result, "Force");
            Type model = FindType("GripForceModel");
            MethodInfo acceleration = model.GetMethod("CalculateAcceleration", BindingFlags.Public | BindingFlags.Static);
            Vector2 light = (Vector2)acceleration.Invoke(null, new object[] { force, 1f });
            Vector2 heavy = (Vector2)acceleration.Invoke(null, new object[] { force, 10f });

            Assert.That(force.magnitude, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(light.x, Is.EqualTo(heavy.x * 10f).Within(0.0001f));
        }

        [Test]
        public void PrototypeCargo_OneHolderHasComfortableUpwardForceMargin()
        {
            UnityEngine.Object configuration = AssetDatabase.LoadMainAssetAtPath(ConfigurationPath);
            UnityEngine.Object cargoData = AssetDatabase.LoadMainAssetAtPath(PrototypeCargoDataPath);
            Assert.That(configuration, Is.Not.Null);
            Assert.That(cargoData, Is.Not.Null);

            float mass = new SerializedObject(cargoData).FindProperty("mass").floatValue;
            object result = CalculateForce(
                Vector2.up,
                Vector2.zero,
                Vector2.zero,
                GetProperty<float>(configuration, "PositionGain"),
                GetProperty<float>(configuration, "VelocityGain"),
                GetProperty<float>(configuration, "MaximumIntentSpeed"),
                GetProperty<float>(configuration, "MaximumGripForce"),
                GetProperty<float>(configuration, "HardReach"));

            float upwardForce = GetProperty<Vector2>(result, "Force").y;
            float cargoWeight = mass * Mathf.Abs(Physics.gravity.y);
            Assert.That(mass, Is.GreaterThan(0f));
            Assert.That(upwardForce, Is.GreaterThan(cargoWeight * 2f),
                "The in-ship prototype should have enough one-holder margin to be easy to lift during testing.");
        }

        [Test]
        public void IndependentHolderForces_AddCancelAndCreateExpectedTorque()
        {
            Vector2 right = new Vector2(30f, 0f);
            Vector2 left = -right;
            MethodInfo torqueMethod = FindType("GripForceModel").GetMethod(
                "CalculateTorqueZ", BindingFlags.Public | BindingFlags.Static);
            float torque = (float)torqueMethod.Invoke(
                null,
                new object[] { new Vector2(0f, 1f), Vector2.zero, right });

            Assert.That((right + right).x, Is.EqualTo(60f));
            Assert.That(right + left, Is.EqualTo(Vector2.zero));
            Assert.That(torque, Is.LessThan(0f));
        }

        [Test]
        public void PlanarProjection_DropsZAndCreatesZeroZForce()
        {
            Type model = FindType("GripForceModel");
            Vector2 projected = (Vector2)model.GetMethod("ProjectXY", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { new Vector3(2f, -3f, 99f) });
            Vector3 force = (Vector3)model.GetMethod("ToWorld", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { projected, 0f });

            Assert.That(projected, Is.EqualTo(new Vector2(2f, -3f)));
            Assert.That(force.z, Is.EqualTo(0f));
        }

        [Test]
        public void ContactRule_AcceptsTouchingHandAndRejectsDistantHand()
        {
            GameObject hand = new GameObject("HandContactTest");
            GameObject cargo = new GameObject("CargoContactTest");
            try
            {
                BoxCollider handCollider = hand.AddComponent<BoxCollider>();
                handCollider.isTrigger = true;
                BoxCollider cargoCollider = cargo.AddComponent<BoxCollider>();
                MethodInfo contact = FindType("GripContactUtility").GetMethod(
                    "TryFindContact", BindingFlags.Public | BindingFlags.Static);

                hand.transform.position = Vector3.zero;
                cargo.transform.position = new Vector3(1.02f, 0f, 0f);
                Physics.SyncTransforms();
                object[] touchingArguments =
                {
                    new Collider[] { handCollider }, new Collider[] { cargoCollider }, 0.035f, Vector3.zero
                };
                Assert.That((bool)contact.Invoke(null, touchingArguments), Is.True);

                cargo.transform.position = new Vector3(1.2f, 0f, 0f);
                Physics.SyncTransforms();
                object[] distantArguments =
                {
                    new Collider[] { handCollider }, new Collider[] { cargoCollider }, 0.035f, Vector3.zero
                };
                Assert.That((bool)contact.Invoke(null, distantArguments), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hand);
                UnityEngine.Object.DestroyImmediate(cargo);
            }
        }

        [Test]
        public void HandVisibility_FreeIsOwnerOnlyAndHeldIsVisibleToEveryPeer()
        {
            MethodInfo visibility = FindType("PlayerHand").GetMethod(
                "ShouldRenderForPeer", BindingFlags.Public | BindingFlags.Static);

            Assert.That((bool)visibility.Invoke(null, new object[] { true, false }), Is.True,
                "The owner must see their free Hand.");
            Assert.That((bool)visibility.Invoke(null, new object[] { false, false }), Is.False,
                "Other peers must not see a free Hand.");
            Assert.That((bool)visibility.Invoke(null, new object[] { true, true }), Is.True);
            Assert.That((bool)visibility.Invoke(null, new object[] { false, true }), Is.True,
                "Every peer must see a Hand while it is holding Cargo.");
        }

        [Test]
        public void Eyeballs_TrackRegisteredHandAndRecenterWhenItUnregisters()
        {
            GameObject player = new GameObject("EyeHandTargetPlayer");
            GameObject eyeSurface = new GameObject("EyeSurface");
            GameObject eyeCenter = new GameObject("EyeCenter");
            GameObject pupil = new GameObject("Pupil");
            GameObject handObject = new GameObject("VisualHand");
            try
            {
                eyeSurface.transform.SetParent(player.transform);
                eyeCenter.transform.SetParent(eyeSurface.transform);
                pupil.transform.SetParent(player.transform);

                Component grabController = player.AddComponent(FindType("CargoGrabController"));
                Component eyeballs = pupil.AddComponent(FindType("PlayerEyeballs"));
                Component hand = handObject.AddComponent(FindType("PlayerHand"));

                Type eyeType = eyeballs.GetType();
                eyeType.GetField("centerTransform", BindingFlags.Public | BindingFlags.Instance)
                    .SetValue(eyeballs, eyeCenter.transform);
                eyeType.GetField("maxRadius", BindingFlags.Public | BindingFlags.Instance)
                    .SetValue(eyeballs, 0.1f);

                eyeType.GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(eyeballs, null);
                MethodInfo registerHand = grabController.GetType().GetMethod(
                    "RegisterHand", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo unregisterHand = grabController.GetType().GetMethod(
                    "UnregisterHand", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo lateUpdate = eyeType.GetMethod(
                    "LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);

                handObject.transform.position = new Vector3(5f, 0f, 0f);
                registerHand.Invoke(grabController, new object[] { hand });
                lateUpdate.Invoke(eyeballs, null);
                Assert.That(pupil.transform.position.x, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(pupil.transform.position.y, Is.EqualTo(0f).Within(0.0001f));

                handObject.transform.position = new Vector3(0f, -5f, 0f);
                lateUpdate.Invoke(eyeballs, null);
                Assert.That(pupil.transform.position.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pupil.transform.position.y, Is.EqualTo(-0.1f).Within(0.0001f));

                unregisterHand.Invoke(grabController, new object[] { hand });
                lateUpdate.Invoke(eyeballs, null);
                Assert.That((Vector2)pupil.transform.position, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(handObject);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Eyeballs_RunAfterPlayerHandVisualUpdate()
        {
            Type eyeType = FindType("PlayerEyeballs");
            Type handType = FindType("PlayerHand");
            DefaultExecutionOrder eyeOrder = eyeType.GetCustomAttribute<DefaultExecutionOrder>();
            DefaultExecutionOrder handOrder = handType.GetCustomAttribute<DefaultExecutionOrder>();

            Assert.That(eyeOrder, Is.Not.Null);
            Assert.That(handOrder, Is.Not.Null);
            Assert.That(eyeOrder.order, Is.GreaterThan(handOrder.order));
        }

        [Test]
        public void SharedConfigurationAndPrefabs_PassHoldingValidator()
        {
            UnityEngine.Object configuration = AssetDatabase.LoadMainAssetAtPath(ConfigurationPath);
            Assert.That(configuration, Is.Not.Null);
            MethodInfo validateConfiguration = configuration.GetType().GetMethod(
                "ValidateConfiguration", BindingFlags.Public | BindingFlags.Instance);
            object[] configurationArguments = { null };
            Assert.That((bool)validateConfiguration.Invoke(configuration, configurationArguments),
                Is.True, configurationArguments[0] as string);

            Type validator = FindType("PressureExpress.EditorTools.WeightedHoldingValidator");
            MethodInfo validate = validator.GetMethod("ValidateAll", BindingFlags.Public | BindingFlags.Static);
            object[] arguments = { null };
            bool passed = (bool)validate.Invoke(null, arguments);
            Assert.That(passed, Is.True, arguments[0] as string);
        }

        [Test]
        public void Prefabs_HaveCoordinatorRigidHandSolverAndNoJoint()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject hand = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
            GameObject cargo = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPrefabPath);

            Assert.That(player.GetComponent(FindType("CargoGrabController")), Is.Not.Null);
            Assert.That(player.GetComponent(FindType("CursorIntentProvider")), Is.Not.Null);
            Assert.That(hand.GetComponent(FindType("PlayerHand")), Is.Not.Null);
            Assert.That(cargo.GetComponent(FindType("CargoHoldSolver")), Is.Not.Null);
            Assert.That(hand.GetComponentsInChildren<Joint>(true), Is.Empty);
            Assert.That(cargo.GetComponentsInChildren<Joint>(true), Is.Empty);
            Assert.That(hand.transform.localScale, Is.EqualTo(Vector3.one),
                "PlayerHand root must remain unit scale because the legacy controller forced unit scale at runtime.");

            SpriteRenderer handRenderer = hand.GetComponentInChildren<SpriteRenderer>(true);
            BoxCollider handCollider = hand.GetComponent<BoxCollider>();
            Rigidbody handBody = hand.GetComponent<Rigidbody>();
            Assert.That(handRenderer, Is.Not.Null);
            Assert.That(handCollider, Is.Not.Null);
            Assert.That(handBody, Is.Not.Null);
            Assert.That(hand.GetComponentsInChildren<Collider>(true).All(collider => collider.isTrigger), Is.True,
                "Hand colliders must stay query-only in both free and holding states.");
            Assert.That(handBody.interpolation, Is.EqualTo(RigidbodyInterpolation.None));
            Assert.That(hand.GetComponent(FindType("Unity.Netcode.Components.NetworkRigidbody")), Is.Null,
                "NetworkRigidbody must not toggle authority/kinematic state on the cursor Hand.");
            Assert.That(handRenderer.sprite.bounds.size.x * handRenderer.transform.lossyScale.x,
                Is.EqualTo(0.96f).Within(0.01f));
            Assert.That(handCollider.size.x * hand.transform.lossyScale.x,
                Is.EqualTo(0.99127316f).Within(0.001f));
        }

        private static object CalculateForce(
            Vector2 cursor,
            Vector2 grip,
            Vector2 velocity,
            float positionGain,
            float velocityGain,
            float maximumSpeed,
            float maximumForce,
            float hardReach)
        {
            MethodInfo calculate = FindType("GripForceModel").GetMethod(
                "Calculate", BindingFlags.Public | BindingFlags.Static);
            return calculate.Invoke(null, new object[]
            {
                cursor, grip, velocity, positionGain, velocityGain, maximumSpeed, maximumForce, hardReach
            });
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                .GetValue(target);
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Unable to resolve type {fullName}");
            return type;
        }
    }
}
