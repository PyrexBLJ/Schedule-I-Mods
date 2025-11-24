using System;
using HarmonyLib;
using System.Reflection;
using MelonLoader;
using System.Collections.Generic;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs;
using Object = UnityEngine.Object;
using Il2CppScheduleOne.Vision;
using UnityEngine;

[assembly: MelonInfo(typeof(CartelDealerAlert.Core), "CartelDealerAlert", "1.0.0", "Pyrex", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CartelDealerAlert
{
    public class Core : MelonMod
    {

        private MelonPreferences_Category category;
        private MelonPreferences_Entry<float> x;
        private MelonPreferences_Entry<float> y;
        private MelonPreferences_Entry<int> size;
        private MelonPreferences_Entry<string> color;
        private static MelonPreferences_Entry<bool> playsound;
        private static List<string> activeDealers = [];
        private static HarmonyLib.Harmony harmony;

        private static void NPCEnteredBuilding_Hook(string buildingGUID, int doorIndex, NPC __instance)
        {
            if(__instance.fullName == "Benzies Dealer" && activeDealers.Contains(__instance.Region.ToString()))
            {
                activeDealers.Remove(__instance.Region.ToString());
            }
        }

        private static void NPCExitedBuilding_Hook(NPCEnterableBuilding building, NPC __instance)
        {
            if(__instance.fullName == "Benzies Dealer")
            {
                activeDealers.Add(__instance.Region.ToString());
                if (playsound.Value)
                    Object.FindObjectsOfType<VisionCone>()[25].ExclamationSound.AudioSource.Play();
            }
        }

        private static void CartelDealerDiedOrKnockedOut_Hook(CartelDealer __instance)
        {
            if(activeDealers.Contains(__instance.Region.ToString()))
            {
                activeDealers.Remove(__instance.Region.ToString());
            }
        }

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Cartel Dealer Alerts Initialized.");
            category = MelonPreferences.CreateCategory("CartelDealerAlert", "Cartel Dealer Alerts");
            x = category.CreateEntry<float>("HorizontalPosition", 2.0f, "Horizontal (x) Position", "How far into the screen the message shows up horizontally, dividing your screen width by this number to get its position.");
            y = category.CreateEntry<float>("VerticalPosition", 6.0f, "Vertical (y) Position", "How far down the screen the message shows up vertically, dividing your screen height by this number to get its position.");
            size = category.CreateEntry<int>("TextSize", 18, "Text Size", "How big the font for the on screen message is.");
            color = category.CreateEntry<string>("TextColor", "ffcc00", "Text Color", "The color of the on screen message text.");
            playsound = category.CreateEntry<bool>("PlaySound", true, "Play Sound", "Play a sound to notify when a dealer spawns along side the on screen message.");
        }


        public override void OnGUI()
        {
            if(activeDealers.Count > 0)
            {
                int i = 0;
                foreach (string location in activeDealers)
                {
                    GUIStyle dealStyle = GUI.skin.box;
                    dealStyle.alignment = TextAnchor.UpperLeft;
                    Vector2 dealSize = dealStyle.CalcSize(new GUIContent($"<size={size.Value}>Active Cartel Dealer in {location}</size>"));
                    GUI.Label(new Rect((Screen.width / x.Value) - (dealSize.x / 2), (float)(Screen.height / y.Value) + (25 * i), dealSize.x, dealSize.y), $"<size={size.Value}><color=#{color.Value}>Active Cartel Dealer in {location}</color></size>", dealStyle);
                    i += 1;
                }
            }
            base.OnGUI();
        }


        private static void Hook(HarmonyLib.Harmony harmony, Type targetType, string methodName, string hookMethod, Type[] arguments = null, bool postHook = true)
        {
            MethodInfo method;
            if(arguments == null)
                method = targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            else
                method = targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, arguments);

            MethodInfo hook = typeof(Core).GetMethod(hookMethod, BindingFlags.Static | BindingFlags.NonPublic);


            if (method == null || hook == null)
            {
                MelonLogger.Error("Failed to hook " + targetType.Name + " to " + methodName);
                return;
            }

            if (postHook)
                harmony.Patch(method, postfix: new HarmonyMethod(hook));
            else
                harmony.Patch(method, prefix: new HarmonyMethod(hook));

            MelonLogger.Msg("Hooked " + targetType.Name + "." + methodName + " to " + hookMethod);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Main")
            {
                harmony = new HarmonyLib.Harmony("CartelDealerAlert.Hooks");
                Hook(harmony, typeof(NPC), "EnterBuilding", nameof(NPCEnteredBuilding_Hook), arguments:[typeof(string), typeof(int)]);
                Hook(harmony, typeof(NPC), "ExitBuilding", nameof(NPCExitedBuilding_Hook), arguments:[typeof(NPCEnterableBuilding)]);
                Hook(harmony, typeof(CartelDealer), "DiedOrKnockedOut", nameof(CartelDealerDiedOrKnockedOut_Hook));
            }
                base.OnSceneWasLoaded(buildIndex, sceneName);
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if(sceneName == "Main")
            {
                harmony.UnpatchSelf();
                activeDealers.Clear();
                MelonLogger.Msg("Unloaded Hooks.");
            }
            base.OnSceneWasUnloaded(buildIndex, sceneName);
        }

        public override void OnDeinitializeMelon()
        {
            LoggerInstance.Msg("Cartel Dealer Alerts Unloaded.");
            base.OnDeinitializeMelon();
        }
    }
}