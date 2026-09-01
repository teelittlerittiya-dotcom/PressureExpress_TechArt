using System;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace PressureExpress.Framework
{
    /// <summary>
    /// Single owner of UnityServices initialisation and anonymous sign-in.
    ///
    /// Previously both AnalyticManager and VivoxManager called
    /// <c>UnityServices.InitializeAsync()</c> concurrently, which is a documented source of
    /// intermittent failures. Everything now funnels through here: the first caller performs the
    /// work, later callers wait for that same attempt instead of starting a second one.
    ///
    /// Deliberately never throws. UGS being unavailable must never be able to take down the
    /// network session, so failures are reported as <c>false</c> and logged as warnings.
    /// </summary>
    public static class UnityServicesBootstrap
    {
        private static bool _initStarted;
        private static bool _initFinished;
        private static bool _signInStarted;
        private static bool _signInFinished;

        public static bool IsInitialized { get; private set; }
        public static bool IsSignedIn { get; private set; }

        /// <summary>
        /// Statics survive scene loads and, when Enter Play Mode Options disables domain reload,
        /// they survive leaving play mode too. Reset explicitly so a second play session does not
        /// short circuit on stale state.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _initStarted = false;
            _initFinished = false;
            _signInStarted = false;
            _signInFinished = false;
            IsInitialized = false;
            IsSignedIn = false;
        }

        public static async UniTask<bool> EnsureInitializedAsync()
        {
            if (_initFinished) return IsInitialized;

            if (_initStarted)
            {
                await UniTask.WaitUntil(() => _initFinished);
                return IsInitialized;
            }

            _initStarted = true;
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }
                IsInitialized = UnityServices.State == ServicesInitializationState.Initialized;
            }
            catch (Exception e)
            {
                IsInitialized = false;
                Debug.LogWarning($"[UGS] UnityServices initialisation failed: {e.Message}");
            }
            finally
            {
                _initFinished = true;
            }

            return IsInitialized;
        }

        public static async UniTask<bool> EnsureSignedInAsync()
        {
            if (_signInFinished) return IsSignedIn;

            if (_signInStarted)
            {
                await UniTask.WaitUntil(() => _signInFinished);
                return IsSignedIn;
            }

            _signInStarted = true;
            try
            {
                if (!await EnsureInitializedAsync())
                {
                    IsSignedIn = false;
                    return false;
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                IsSignedIn = AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception e)
            {
                IsSignedIn = false;
                Debug.LogWarning($"[UGS] Anonymous sign-in failed: {e.Message}");
            }
            finally
            {
                _signInFinished = true;
            }

            return IsSignedIn;
        }
    }
}
