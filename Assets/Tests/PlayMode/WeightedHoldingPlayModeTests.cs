using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PressureExpress.Tests.PlayMode
{
    public sealed class WeightedHoldingPlayModeTests
    {
        [UnityTest]
        public IEnumerator EqualForce_AcceleratesLightBodyMoreThanHeavyBody()
        {
            Rigidbody light = CreateBody("LightCargo", new Vector3(-10f, 10f, 0f), 1f);
            Rigidbody heavy = CreateBody("HeavyCargo", new Vector3(10f, 10f, 0f), 10f);
            try
            {
                Vector2 force2D = CalculateForce(Vector2.right, Vector2.zero, Vector2.zero);
                Vector3 force = new Vector3(force2D.x, force2D.y, 0f);
                light.AddForce(force, ForceMode.Force);
                heavy.AddForce(force, ForceMode.Force);

                yield return new WaitForFixedUpdate();

                Assert.That(light.linearVelocity.x, Is.GreaterThan(heavy.linearVelocity.x * 9f));
                Assert.That(light.linearVelocity.z, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(heavy.linearVelocity.z, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.Destroy(light.gameObject);
                UnityEngine.Object.Destroy(heavy.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator OpposingForcesCancelWhileOffCenterForceCreatesOnlyZTorque()
        {
            Rigidbody cancelled = CreateBody("CancelledCargo", new Vector3(-10f, 10f, 0f), 5f);
            Rigidbody torque = CreateBody("TorqueCargo", new Vector3(10f, 10f, 0f), 5f);
            cancelled.gameObject.AddComponent<BoxCollider>();
            torque.gameObject.AddComponent<BoxCollider>();

            try
            {
                Vector3 right = new Vector3(30f, 0f, 0f);
                cancelled.AddForceAtPosition(right, cancelled.worldCenterOfMass, ForceMode.Force);
                cancelled.AddForceAtPosition(-right, cancelled.worldCenterOfMass, ForceMode.Force);
                torque.AddForceAtPosition(right, torque.worldCenterOfMass + Vector3.up, ForceMode.Force);

                yield return new WaitForFixedUpdate();

                Assert.That(cancelled.linearVelocity.magnitude, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(torque.angularVelocity.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(torque.angularVelocity.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(Mathf.Abs(torque.angularVelocity.z), Is.GreaterThan(0.0001f));
            }
            finally
            {
                UnityEngine.Object.Destroy(cancelled.gameObject);
                UnityEngine.Object.Destroy(torque.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator RigidGripPoint_ResolvesToExactlyTheCargoLocalPoint()
        {
            GameObject cargo = new GameObject("GripPointCargo");
            GameObject hand = new GameObject("ActualHand");
            try
            {
                cargo.transform.SetPositionAndRotation(
                    new Vector3(3f, -2f, -3.24f),
                    Quaternion.Euler(0f, 0f, 37f));
                Vector3 localGrip = new Vector3(0.42f, -0.18f, 0f);
                Vector3 worldGrip = cargo.transform.TransformPoint(localGrip);
                hand.transform.position = worldGrip;

                yield return null;

                Assert.That(Vector3.Distance(hand.transform.position, cargo.transform.TransformPoint(localGrip)),
                    Is.EqualTo(0f).Within(0.000001f));
                Assert.That(hand.transform.position.z, Is.EqualTo(cargo.transform.position.z).Within(0.000001f));
            }
            finally
            {
                UnityEngine.Object.Destroy(cargo);
                UnityEngine.Object.Destroy(hand);
            }
        }

        [UnityTest]
        public IEnumerator QueryOnlyHand_DetectsCargoWithoutApplyingCollisionImpulse()
        {
            Rigidbody cargo = CreateBody("QueryCargo", new Vector3(0f, 10f, 0f), 1f);
            GameObject hand = new GameObject("QueryHand");
            hand.transform.position = cargo.position;
            Rigidbody handBody = hand.AddComponent<Rigidbody>();
            handBody.isKinematic = true;
            handBody.useGravity = false;
            handBody.interpolation = RigidbodyInterpolation.None;
            BoxCollider handCollider = hand.AddComponent<BoxCollider>();
            handCollider.isTrigger = true;
            BoxCollider cargoCollider = cargo.gameObject.AddComponent<BoxCollider>();

            try
            {
                Physics.SyncTransforms();
                MethodInfo contact = FindType("GripContactUtility").GetMethod(
                    "TryFindContact", BindingFlags.Public | BindingFlags.Static);
                object[] arguments =
                {
                    new Collider[] { handCollider }, new Collider[] { cargoCollider }, 0.035f, Vector3.zero
                };

                Assert.That((bool)contact.Invoke(null, arguments), Is.True,
                    "A trigger Hand must still satisfy the physical hit gate.");

                yield return new WaitForFixedUpdate();

                Assert.That(cargo.linearVelocity.magnitude, Is.EqualTo(0f).Within(0.0001f),
                    "An idle Hand trigger must not push Cargo.");
            }
            finally
            {
                UnityEngine.Object.Destroy(cargo.gameObject);
                UnityEngine.Object.Destroy(hand);
            }
        }

        private static Vector2 CalculateForce(Vector2 cursor, Vector2 grip, Vector2 velocity)
        {
            Type model = FindType("GripForceModel");
            object result = model.GetMethod("Calculate", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { cursor, grip, velocity, 3f, 20f, 3f, 60f, 1.75f });
            return (Vector2)result.GetType().GetProperty("Force", BindingFlags.Public | BindingFlags.Instance)
                .GetValue(result);
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Unable to resolve type {fullName}");
            return type;
        }

        private static Rigidbody CreateBody(string name, Vector3 position, float mass)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.position = position;
            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezePositionZ
                               | RigidbodyConstraints.FreezeRotationX
                               | RigidbodyConstraints.FreezeRotationY;
            return body;
        }
    }
}
