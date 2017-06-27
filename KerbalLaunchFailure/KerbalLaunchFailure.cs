﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.UI.Screens;

namespace KerbalLaunchFailure
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KerbalLaunchFailureController : MonoBehaviour
    {
        /// <summary>
        /// The plugin's name.
        /// </summary>
        public const string PluginName = "Kerbal Launch Failure";

        /// <summary>
        /// The plugin's abbreviated name.
        /// </summary>
        public const string PluginAbbreviation = "KLF";

        /// <summary>
        /// The plugin's PluginData directory.
        /// </summary>
        public const string LocalPluginDataPath = "GameData/KerbalLaunchFailure/PluginData/";

        /// <summary>
        /// Is the failure script active?
        /// </summary>
        private bool isFailureScriptActive = false;

        /// <summary>
        /// Is the game paused?
        /// </summary>
        private bool isGamePaused = false;

        /// <summary>
        /// The failure that runs.
        /// </summary>
        private Failure failure;

        public static bool soundPlaying = false;
        public static FXGroup alarmSound = null;
        public static string LocalAlarmSoundDir = "KerbalLaunchFailure/Sounds/";

        private int myWindowId;
        private Rect windowRect;
        const float WINDOW_WIDTH = 225;
        const float WINDOW_HEIGHT = 200;

        internal ApplicationLauncherButton launcherButton = null;
        bool abortButtonYellow = false;
        public Texture2D GetImage(String path, int width, int height)
        {
            Texture2D img = new Texture2D(width, height, TextureFormat.ARGB32, false);
            img = GameDatabase.Instance.GetTexture(path, false);
            return img;
        }
        private void OnGUIApplicationLauncherReady()
        {
            if (launcherButton == null)
            {
                launcherButton = ApplicationLauncher.Instance.AddModApplication(
                    abortWindow,
                    abortWindow,
                    null, null, null, null,
                    ApplicationLauncher.AppScenes.FLIGHT,
                    GetImage("KerbalLaunchFailure/Textures/allSystemsGo", 38, 38));
            }
        }

        void abortWindow()
        {
            Log.Info("abortWindow  isFailureScriptActive: " + isFailureScriptActive.ToString() + "   KerbalLaunchFailureController.soundPlaying: " + KerbalLaunchFailureController.soundPlaying.ToString());
            if (!isFailureScriptActive || !KerbalLaunchFailureController.soundPlaying)
                return;
            failure.disableAlarm();
        }

        double lastUpdate = 0f;
        void updateToolbarButton()
        {
            if (launcherButton == null || failure == null)
                return;
            if (failure.failurecomplete)
            {
                launcherButton.SetTexture(GetImage("KerbalLaunchFailure/Textures/failed", 38, 38));
                failure.failurecomplete = false;
                return;
            }
            if (!isFailureScriptActive || (!failure.alarmDisabled && !KerbalLaunchFailureController.soundPlaying))
                return;
            if (Planetarium.GetUniversalTime() - lastUpdate > 1)
            {
                lastUpdate = Planetarium.GetUniversalTime();

                abortButtonYellow = !abortButtonYellow;
                if (abortButtonYellow)
                    launcherButton.SetTexture(GetImage("KerbalLaunchFailure/Textures/warning", 38, 38));
                else
                    launcherButton.SetTexture(GetImage("KerbalLaunchFailure/Textures/warning2", 38, 38));
            }
        }

        public void removeLauncherButtons()
        {
            if (launcherButton != null)
            {
               // GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIApplicationLauncherReady);
                ApplicationLauncher.Instance.RemoveModApplication(launcherButton);
                launcherButton = null;
            }
        }


        /// <summary>
        /// Called when script instance is being loaded.
        /// </summary>
        public void Awake()
        {
            if (!HighLogic.CurrentGame.Parameters.CustomParams<KLFCustomParams>().enabled)
                return;
            // When a launch occurs, start failure script.
            GameEvents.onLaunch.Add(FailureStartHandler);

            // Need to keep track of when the game pauses or unpauses.
            GameEvents.onGamePause.Add(FailureGamePauseHandler);
            GameEvents.onGameUnpause.Add(FailureGameUnpauseHandler);
            GameEvents.OnGameSettingsApplied.Add(ReloadSettings);

            myWindowId = GetInstanceID();
            windowRect = new Rect(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT);
            windowRect.center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            if (launcherButton == null)
            {
                OnGUIApplicationLauncherReady();
            }
        }

        void OnGUI()
        {
            //Log.Info("OnGUI");
            if (Failure.Instance == null || FlightDriver.Pause)
                return;
            
            if (Failure.Instance.failureTime == 0 || Failure.Instance.failureTime == Double.MaxValue)
                return;

            GUI.skin = HighLogic.Skin;
            windowRect = GUILayout.Window(myWindowId, windowRect, Window, "AutoAbort Cancel/Reset");
        }

        public void Window(int windowID)
        {
            GUI.skin = HighLogic.Skin;
            var oldColor = GUI.backgroundColor;
            var bstyle = new GUIStyle(GUI.skin.button);
            bstyle.normal.textColor = Color.yellow;
            //bstyle.normal.background = new Texture2D(2,2);
           

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("AutoAbort in: " + ((Failure.Instance.failureTime + KLFSettings.Instance.AutoAbortDelay) - Planetarium.GetUniversalTime()).ToString("n2") + " seconds");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Cancel AutoAbort", bstyle, GUILayout.Width(170.0f), GUILayout.Height(40.0f)))
            {
                Failure.Instance.failureTime = Double.MaxValue;
            }
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = oldColor;
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Reset AutoAbort Timer", bstyle, GUILayout.Width(170.0f), GUILayout.Height(35.0f)))
            {
                Failure.Instance.failureTime = Planetarium.GetUniversalTime();
            }
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = oldColor;
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            // GUI.backgroundColor = Color.blue;
            //bstyle.normal.textColor = Color.red;
            if (GUILayout.Button("Abort Immediately", bstyle, GUILayout.Width(170.0f), GUILayout.Height(35.0f)))
            {
                Failure.Instance.AddAndReportScience();
                Failure.Instance.activeVessel.ActionGroups.SetGroup(KSPActionGroup.Abort, true);
            }
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = oldColor;
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        
        /// <summary>
        /// Reload settings if necessary.
        /// </summary>

        void ReloadSettings()
        {
            if (KLFSettings.Instance != null)
                KLFSettings.Instance.LoadSettings();
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        public void FixedUpdate()
        {
            if (!HighLogic.CurrentGame.Parameters.CustomParams<KLFCustomParams>().enabled)
                return;

            // float throttle = FlightGlobals.ActiveVessel.ctrlState.mainThrottle;
            // Log.Info("FixedUpdate throttle: " + throttle.ToString());

            // Only run if the failure script is active and the game is not paused.
            if ((failure != null && failure.failurecomplete) || (isFailureScriptActive && !isGamePaused))
            {
                updateToolbarButton();
                RunFailure();
            }
        }

        /// <summary>
        /// Called when destroyed.
        /// </summary>
        public void OnDestroy()
        {
            removeLauncherButtons();
            DestroyFailureRun();
            Failure.Instance = null;
        }

        /// <summary>
        /// To be called when the failure script is to start it's work.
        /// </summary>
        /// <param name="eventReport">Event Report from action.</param>
        private void FailureStartHandler(EventReport eventReport)
        {
            if (Failure.Occurs())
            {
                
                SoundManager.LoadSound(LocalAlarmSoundDir + KLFSettings.Instance.AlarmSoundFile, "AlarmSound");
                alarmSound = new FXGroup("AlarmSound");
//                SoundManager.CreateFXSound(FlightGlobals.ActiveVessel.rootPart, alarmSound, "AlarmSound", true, 50f);
                SoundManager.CreateFXSound(null, alarmSound, "AlarmSound", true, 50f);

                ActivateFailureRun();
            }
            else
            {
                // Since there will be no need to run the script, destroy it.
                DestroyFailureRun();
            }
        }

        /// <summary>
        /// To be called when the failure script is to stop.
        /// </summary>
        /// <param name="vessel">The vessel that performed the action.</param>
        private void FailureEndHandler(Vessel vessel)
        {
            DestroyFailureRun();
        }

        /// <summary>
        /// To be called when the game is paused.
        /// </summary>
        private void FailureGamePauseHandler()
        {
            isGamePaused = true;
            if (alarmSound != null)
            {
                alarmSound.audio.Stop();
            }
        }

        /// <summary>
        /// To be called when the game is unpaused.
        /// </summary>
        private void FailureGameUnpauseHandler()
        {
            isGamePaused = false;
            if (alarmSound != null && KerbalLaunchFailureController.soundPlaying)
                alarmSound.audio.Play();
        }

        /// <summary>
        /// Activates the failure script.
        /// </summary>
        private void ActivateFailureRun()
        {
            Log.Info("ActivateFailureRun");
            isFailureScriptActive = true;
            isGamePaused = false;
            failure = new Failure();
            if (HighLogic.CurrentGame.Parameters.CustomParams<KLFCustomParams2>().allowEngineUnderthrust)
            {
                failure.overThrust = KLFUtils.RNG.NextDouble() >= HighLogic.CurrentGame.Parameters.CustomParams<KLFCustomParams2>().engineUnderthrustProb;
            }
            else
                failure.overThrust =true;
            failure.launchTime = Planetarium.GetUniversalTime();
            GameEvents.VesselSituation.onReachSpace.Add(FailureEndHandler);
        }

        /// <summary>
        /// Cleanup to remove all event handlers when the failure script is completed.
        /// </summary>
        private void DestroyFailureRun()
        {
            updateToolbarButton();
            isFailureScriptActive = false;
            isGamePaused = false;
            failure = null;
            if (alarmSound != null)
                alarmSound.audio.Stop();
            KerbalLaunchFailureController.soundPlaying = false;
            GameEvents.onLaunch.Remove(FailureStartHandler);
            GameEvents.onGamePause.Remove(FailureGamePauseHandler);
            GameEvents.onGameUnpause.Remove(FailureGameUnpauseHandler);
            GameEvents.VesselSituation.onReachSpace.Remove(FailureEndHandler);
            GameEvents.OnGameSettingsApplied.Remove(ReloadSettings);

        }

        /// <summary>
        /// Runs the failure.
        /// </summary>
        private void RunFailure()
        {
            if (!failure.Run())
            {
                DestroyFailureRun();
            }
        }
    }
}
