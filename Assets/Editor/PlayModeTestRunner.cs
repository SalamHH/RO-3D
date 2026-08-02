using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";
        private const string SentinelLog = "PLAY_MODE_TEST_COMPLETE";

        private static readonly int WaitFrames = SessionState.GetInt("PlayModeTest.WaitFrames", 10);

        private static List<string> _capturedLogs = new List<string>();
        private const int MaxCapturedLogs = 50;

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");

            switch (state)
            {
                case "Idle":
                    break;

                case "WaitingForCompile":
                    Debug.Log("[PlayModeTest] Bootstrap compiled. Scheduling Play Mode entry.");
                    EditorApplication.delayCall += () =>
                    {
                        SessionState.SetString(StateKey, "EnteringPlayMode");
                        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                        EditorApplication.isPlaying = true;
                    };
                    break;

                case "EnteringPlayMode":
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                        SessionState.SetString(StateKey, "InPlayMode");
                        EditorApplication.update += WaitFramesThenRun;
                    }
                    break;

                case "InPlayMode":
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.update += WaitFramesThenRun;
                    }
                    break;

                case "Done":
                    Debug.Log(SentinelLog);
                    EditorApplication.delayCall += SelfDestruct;
                    break;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                SessionState.SetString(StateKey, "InPlayMode");
                EditorApplication.update += WaitFramesThenRun;
            }
        }

        private static int _frameCount = 0;
        private static bool _hasRun = false;

        private static void WaitFramesThenRun()
        {
            _frameCount++;
            if (_frameCount < WaitFrames) return;

            if (_hasRun) return;
            _hasRun = true;
            EditorApplication.update -= WaitFramesThenRun;

            Application.logMessageReceived += OnLogMessage;
            string resultJson;
            try
            {
                resultJson = RunTestLogic();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayModeTest] Test threw exception: " + e);
                resultJson = JsonUtility.ToJson(new TestResult
                {
                    success = false,
                    error = e.Message,
                    logs = _capturedLogs.ToArray()
                });
            }
            finally
            {
                Application.logMessageReceived -= OnLogMessage;
            }

            SessionState.SetString(ResultKey, resultJson);
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static void SelfDestruct()
        {
            string scriptPath = SessionState.GetString(ScriptPathKey, "");
            if (!string.IsNullOrEmpty(scriptPath) && AssetDatabase.AssetPathExists(scriptPath))
            {
                AssetDatabase.DeleteAsset(scriptPath);
            }
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ScriptPathKey);
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_capturedLogs.Count >= MaxCapturedLogs) return;
            if (type == LogType.Error || type == LogType.Exception ||
                message.Contains("[Test]") || message.Contains("TEST_RESULT"))
            {
                _capturedLogs.Add("[" + type + "] " + message);
            }
        }

        [System.Serializable]
        private class TestResult
        {
            public bool success;
            public string error;
            public string[] logs;
            public bool gameManagerFound;
            public bool playerFound;
            public bool envManagerFound;
            public bool spawnerFound;
            public bool uiManagerFound;
            public bool canvasCreated;
            public string initialGameState;
            public string stateAfterStart;
        }

        private static string RunTestLogic()
        {
            TestResult res = new TestResult();
            res.success = true;

            // 1. Check GameManager
            var gmObj = GameObject.Find("GameManager");
            res.gameManagerFound = (gmObj != null);
            if (gmObj != null)
            {
                var gmComponent = gmObj.GetComponent("GameManager");
                if (gmComponent != null)
                {
                    var stateProp = gmComponent.GetType().GetProperty("CurrentState");
                    if (stateProp != null)
                    {
                        res.initialGameState = stateProp.GetValue(gmComponent).ToString();
                    }

                    // Try calling StartGame()
                    var startMethod = gmComponent.GetType().GetMethod("StartGame");
                    if (startMethod != null)
                    {
                        startMethod.Invoke(gmComponent, null);
                        if (stateProp != null)
                        {
                            res.stateAfterStart = stateProp.GetValue(gmComponent).ToString();
                        }
                    }
                }
            }

            // 2. Check Player / Longship
            var shipObj = GameObject.Find("VikingLongship(Clone)");
            if (shipObj == null) shipObj = GameObject.Find("VikingLongship");
            res.playerFound = (shipObj != null);

            // 3. Check EnvironmentManager
            var envObj = GameObject.Find("EnvironmentManager");
            res.envManagerFound = (envObj != null);

            // 4. Check Spawner
            var spawnerObj = GameObject.Find("ObstacleSpawner");
            res.spawnerFound = (spawnerObj != null);

            // 5. Check UIManager and Canvas
            var uiObj = GameObject.Find("UIManager");
            res.uiManagerFound = (uiObj != null);

            var canvasObj = GameObject.Find("UICanvas");
            res.canvasCreated = (canvasObj != null);

            res.logs = _capturedLogs.ToArray();
            
            // Overall success depends on finding major components
            res.success = res.gameManagerFound && res.playerFound && res.envManagerFound && res.spawnerFound && res.uiManagerFound;

            return JsonUtility.ToJson(res);
        }
    }
}
