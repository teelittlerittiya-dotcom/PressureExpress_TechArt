using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PressureExpress.Tests.PlayMode
{
    public sealed class CargoPrototypePlayModeTests
    {
        [UnityTest]
        [Timeout(20000)]
        public IEnumerator MainLevelCargo_InitializesFallsAndRemainsOnGameplayPlane()
        {
            // MainLevel expects persistent services created by the real bootstrap scene.
            // Loading it directly produces unrelated singleton errors before Cargo starts.
            yield return LoadScene("Bootstrap");
            float bootstrapDeadline = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup < bootstrapDeadline)
            {
                yield return null;
            }
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));

            yield return LoadScene("MainLevel");
            Scene scene = SceneManager.GetActiveScene();
            Assert.That(scene.name, Is.EqualTo("MainLevel"));

            Type cargoType = FindType("CargoController");
            Component cargo = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.GetComponent(cargoType))
                .FirstOrDefault(component => component != null);
            Assert.That(cargo, Is.Not.Null);

            Transform cargoTransform = cargo.transform;
            float authoredZ = cargoTransform.position.z;
            Rigidbody body = cargo.GetComponent<Rigidbody>();
            Assert.That(body, Is.Not.Null);

            yield return new WaitForSeconds(3f);
            yield return new WaitForFixedUpdate();

            bool initialized = (bool)cargoType.GetProperty("IsInitialized").GetValue(cargo);
            object state = cargoType.GetProperty("CurrentRuntimeState").GetValue(cargo);
            Type stateType = state.GetType();

            Assert.That(initialized, Is.True);
            Assert.That((bool)stateType.GetField("Initialized").GetValue(state), Is.True);
            Assert.That(Convert.ToByte(stateType.GetField("ModuleMask").GetValue(state)), Is.EqualTo(15));
            Assert.That(Convert.ToSingle(stateType.GetField("Impact").GetValue(state)), Is.InRange(0f, 100f));
            Assert.That(Convert.ToSingle(stateType.GetField("Temperature").GetValue(state)), Is.InRange(-20f, 80f));
            Assert.That(Convert.ToSingle(stateType.GetField("Freshness").GetValue(state)), Is.InRange(0f, 100f));
            Assert.That(Convert.ToSingle(stateType.GetField("Pressure").GetValue(state)), Is.InRange(0f, 200f));

            Assert.That(cargoTransform.position.x, Is.EqualTo(0f).Within(0.05f));
            Assert.That(cargoTransform.position.y, Is.InRange(-2.1f, -1.9f));
            Assert.That(cargoTransform.position.z, Is.EqualTo(authoredZ).Within(0.0001f));
            Assert.That(body.linearVelocity.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Mathf.DeltaAngle(cargoTransform.eulerAngles.x, 0f), Is.EqualTo(0f).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(cargoTransform.eulerAngles.y, 0f), Is.EqualTo(0f).Within(0.01f));
            Assert.That(cargo.GetComponentsInChildren<MeshCollider>(true).Length, Is.EqualTo(3));
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
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
