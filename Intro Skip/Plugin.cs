﻿using IllusionPlugin;
using IllusionInjector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;
using System.Media;
using TMPro;
using UnityEngine.XR;
namespace Intro_Skip
{
    public class Plugin : IPlugin
    {
        public string Name => "Intro Skip";
        public string Version => "1.4.1";
        private readonly string[] env = { "DefaultEnvironment", "BigMirrorEnvironment", "TriangleEnvironment", "NiceEnvironment" };

        public static bool skipIntro = false;
        public static bool isLevel = false;
        public static bool skipLongIntro = false;
        public static bool promptPlayer = false;
        public static bool hasSkipped = false;
        public static bool allowedToSkip = false;
        public static float firstObjectTime = 0;
        public static float introSkipTime = 0;
        private static StandardLevelSceneSetupDataSO _mainGameSceneSetupData = null;
        private static AudioSource _songAudio;
        GameObject promptObject;
        TextMeshPro _skipPrompt;
        public static AudioTimeSyncController AudioTimeSync { get; private set; }

        VRController leftController;
        VRController rightController;

        //Special Event stuffs
        public static bool specialEvent = true;
        PlatformLeaderboardsModel obj;
        string playerID;
        bool soundIsPlaying = false;

        public static bool multiActive = false;
        public void OnApplicationStart()
        {
            SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
            skipLongIntro = ModPrefs.GetBool("IntroSkip", "skipLongIntro", false, true);
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode arg1)
        {
            skipLongIntro = ModPrefs.GetBool("IntroSkip", "skipLongIntro", false, true);
            if (scene.name == "GameCore")
            {
                if (_mainGameSceneSetupData == null)
                {
                    _mainGameSceneSetupData = Resources.FindObjectsOfTypeAll<StandardLevelSceneSetupDataSO>().FirstOrDefault();
                }
                Log("Game scene");
                skipIntro = false;
                isLevel = true;
                allowedToSkip = false;
                if (_mainGameSceneSetupData != null)
                {
                    if (PluginManager.Plugins.Any(x => x.Name == "Beat Saber Multiplayer"))
                    {
                        GameObject client = GameObject.Find("MultiplayerClient");
                        if (client != null)
                        {
                            multiActive = true;
                            Log("Found MultiplayerClient game object!");

                        }
                        else
                        {
                            multiActive = false;
                            Log(" MultiplayerClient game object not found!");
                        }
                    }
                    if (multiActive == false)
                        if (skipLongIntro == true)
                            Init();

                }
            else
            {
                isLevel = false;
            }

                SharedCoroutineStarter.instance.StartCoroutine(DelayedSetSkip()); ;
            }
        }

        private void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
        {   //Handle quitting/restarting song mid special event


            promptPlayer = false;
            isLevel = false;


            if (scene.name == "Menu")
            {

                firstObjectTime = 1000000;
                introSkipTime = 0;


            }

        }






        public void OnApplicationQuit()
        {
            SceneManager.activeSceneChanged -= SceneManagerOnActiveSceneChanged;
        }

        public void OnLevelWasLoaded(int level)
        {

        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnUpdate()
        {

            if (multiActive == false)
            {
                if (soundIsPlaying == true && _songAudio != null)
                {
                    Log("Pausing");
                    _songAudio.pitch = 0f;
                    AudioTimeSync.forcedAudioSync = true;
                }

                if (isLevel == true && skipLongIntro == true && _songAudio != null)
                {

                    if (skipIntro == true && _songAudio.time < introSkipTime && hasSkipped == false && allowedToSkip == true)
                    {
                        if (leftController.triggerValue >= .8 || rightController.triggerValue >= .8)
                        {
                            Skip();
                            DestroyPrompt();
                            hasSkipped = true;
                            Log("Attempting Haptics");
                            SharedCoroutineStarter.instance.StartCoroutine(OneShotRumbleCoroutine(leftController, 0.2f, 1));
                            SharedCoroutineStarter.instance.StartCoroutine(OneShotRumbleCoroutine(rightController, 0.2f, 1));
                        }


                    }
                    if (_songAudio.time > introSkipTime && skipIntro == true)
                    {
                        DestroyPrompt();
                    }
                }
            }


        }

        public void OnFixedUpdate()
        {
        }
        public static void Log(string message)
        {
            Console.WriteLine("[{0}] {1}", "IntroSkip", message);
        }

        public void DestroyPrompt()
        {
            var obj = GameObject.Find("Prompt");
            if (obj != null)
                GameObject.Destroy(obj);
        }
        public void Init()
        {

            obj = Resources.FindObjectsOfTypeAll<PlatformLeaderboardsModel>().FirstOrDefault();
            if (obj != null)
                playerID = ReflectionUtil.GetField<string>(obj, "_playerId");

            specialEvent = ModPrefs.GetBool("IntroSkip", "specialEvent", true, true);
            AudioTimeSync = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault();
            if (AudioTimeSync != null)
            {
                _songAudio = AudioTimeSync.GetField<AudioSource>("_audioSource");
                if (_songAudio != null)
                {
                    Log("Audio not null");
                    Log("Object Found");
                }
            }
            else
                Log("Object is null");
            Log("Level Found");
            CheckSkip();

        }

        public void CheckSkip()
        {
            foreach (BeatmapLineData lineData in _mainGameSceneSetupData.difficultyBeatmap.beatmapData.beatmapLinesData)
            {
                Log("Parsing Line");
                foreach (BeatmapObjectData objectData in lineData.beatmapObjectsData)
                {

                    if (objectData.beatmapObjectType == BeatmapObjectType.Note)
                    {
                        //   Console.WriteLine("Note or Bomb found");
                        //  Console.WriteLine(objectData.time);
                        if (objectData.time < firstObjectTime)
                            firstObjectTime = objectData.time;


                    }
                    else if (objectData.beatmapObjectType == BeatmapObjectType.Obstacle)
                    {
                        ObstacleData obstacle = (ObstacleData)objectData;
                        if (obstacle.lineIndex == 0 && obstacle.width == 1)
                        {
                            //  Console.WriteLine("skipping side wall");

                        }
                        else if (obstacle.lineIndex == 3 && obstacle.width == 1)
                        {
                            //  Console.WriteLine("skipping side wall");

                        }
                        else if (obstacle.duration <= 0 || obstacle.width <= 0)
                        {
                            //   Console.WriteLine("fake wall Found");
                            //   Console.WriteLine(objectData.time);
                            if (objectData.time < firstObjectTime)
                                firstObjectTime = objectData.time;

                        }
                        else
                        {
                            //  Console.WriteLine("Significant wall Found");
                            //    Console.WriteLine(objectData.time);
                            if (objectData.time < firstObjectTime)
                                firstObjectTime = objectData.time;
                        }
                    }
                }
            }
            if (firstObjectTime > 5)
                skipIntro = true;
            Log("First note is at " + firstObjectTime.ToString());

        }

        public void CreateSkipPrompt()
        {
            Log("Creating Prompt");
            promptObject = new GameObject("Prompt");
            _skipPrompt = promptObject.AddComponent<TextMeshPro>();
            _skipPrompt.text = "Press Trigger To Skip Intro";
            _skipPrompt.fontSize = 4;
            _skipPrompt.color = Color.white;
            _skipPrompt.font = Resources.Load<TMP_FontAsset>("Teko-Medium SDF No Glow");
            _skipPrompt.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 5f);
            _skipPrompt.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1f);
            _skipPrompt.rectTransform.position = new Vector3(-2.5f, 2.1f, 7.0f);
        }




        public void Skip()
        {
            SharedCoroutineStarter.instance.StartCoroutine(SkipToTime());
            Log("Attempting to Skip Intro");

            
        }

        private IEnumerator SkipToTime()
        {
            yield return new WaitForSecondsRealtime(0.1f);
            _songAudio.time = introSkipTime;
            Log("Intro Skipped");


        }

        private IEnumerator DelayedSetSkip()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (skipIntro == true)
            {
                Log("Skippable Intro");
                introSkipTime = firstObjectTime - 2;
                promptPlayer = true;
                CreateSkipPrompt();
                allowedToSkip = true;

            }
            else
                Log("Will Not Skip Intro");

            
        }


        public IEnumerator OneShotRumbleCoroutine(VRController controller, float duration, float impulseStrength, float intervalTime = 0f)
        {
            VRPlatformHelper vr = VRPlatformHelper.instance;
            YieldInstruction waitForIntervalTime = new WaitForSeconds(intervalTime);
            float time = Time.time + 0.1f;
            while (Time.time < time && isLevel == true)
            {
                vr.TriggerHapticPulse(controller.node, impulseStrength);
                yield return intervalTime > 0 ? waitForIntervalTime : null;
            }
        }

    }
}
