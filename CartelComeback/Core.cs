using MelonLoader;
using HarmonyLib;
using System;
using Il2CppScheduleOne.Cartel;
using Il2Cpp;
using Il2CppScheduleOne.DevUtilities;
using Il2CppFishNet;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Graffiti;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.PlayerScripts;
using System.Linq;
using Il2CppSystem.Collections.Generic;
using Il2CppGameKit.Utilities;
using UnityEngine;

[assembly: MelonInfo(typeof(CartelComeback.Core), "CartelComeback", "1.0.0", "Pyrex", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CartelComeback
{
    public class Core : MelonMod
    {

        private static HarmonyLib.Harmony harmony;

        private MelonPreferences_Category category;
        private static MelonPreferences_Entry<float> fakeInfluence_Northtown;
        private static MelonPreferences_Entry<float> fakeInfluence_Westville;
        private static MelonPreferences_Entry<float> fakeInfluence_Downtown;
        private static MelonPreferences_Entry<float> fakeInfluence_Docks;
        private static MelonPreferences_Entry<float> fakeInfluence_Suburbia;
        private static MelonPreferences_Entry<float> fakeInfluence_Uptown;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Cartel Comeback Loaded.");
            category = MelonPreferences.CreateCategory("CartelComeback", "Cartel Comeback");
            fakeInfluence_Northtown = category.CreateEntry("fake_northtown_influence", 0.0f, "Fake Northtown Influence", "0.0 - 1.0 What to spoof the cartel influence to in this region. A lower value will decrease cartel activity in the region.");
            fakeInfluence_Westville = category.CreateEntry("fake_westville_influence", 0.5f, "Fake Westville Influence", "0.0 - 1.0 What to spoof the cartel influence to in this region. A lower value will decrease cartel activity in the region.");
            fakeInfluence_Downtown = category.CreateEntry("fake_downtown_influence", 0.5f, "Fake Downtown Influence", "0.0 - 1.0 What to spoof the cartel influence to in this region. A lower value will decrease cartel activity in the region.");
            fakeInfluence_Docks = category.CreateEntry("fake_docks_influence", 0.5f, "Fake Docks Influence", "0.0 - 1.0 What to spoof the cartel influence to in this region. A lower value will decrease cartel activity in the region.");
            fakeInfluence_Suburbia = category.CreateEntry("fake_suburbia_influence", 0.5f, "Fake Suburbia Influence", "0.0 - 1.0 What to spoof the cartel influence to in this region. A lower value will decrease cartel activity in the region.");
            fakeInfluence_Uptown = category.CreateEntry("fake_uptown_influence", 0.5f, "Fake Uptown Influence", "0.0 - 1.0 What to spoof the cartel influence to in this region. A lower value will decrease cartel activity in the region.");
            base.OnInitializeMelon();
        }

        private static float GetFakeRegionInfluence(EMapRegion region)
        {
            switch (region)
            {
                case EMapRegion.Northtown:
                    return Mathf.Clamp(fakeInfluence_Northtown.Value, 0.0f, 1.0f);

                case EMapRegion.Westville:
                    return Mathf.Clamp(fakeInfluence_Westville.Value, 0.0f, 1.0f);
                
                case EMapRegion.Downtown:
                    return Mathf.Clamp(fakeInfluence_Downtown.Value, 0.0f, 1.0f);

                case EMapRegion.Docks:
                    return Mathf.Clamp(fakeInfluence_Docks.Value, 0.0f, 1.0f);

                case EMapRegion.Suburbia:
                    return Mathf.Clamp(fakeInfluence_Suburbia.Value, 0.0f, 1.0f);
                
                case EMapRegion.Uptown:
                    return Mathf.Clamp(fakeInfluence_Uptown.Value, 0.0f, 1.0f);
                
                default:
                    return 0.0f;
            }
        }

        [HarmonyPatch(typeof(CartelActivities), nameof(CartelActivities.HourPass))]
        class CartelActivitiesHourPassPatch
        {
            static bool Prefix(CartelActivities __instance)
            {
                if (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Defeated)
                {
                    if (InstanceFinder.IsServer && __instance.CurrentGlobalActivity == null)
                    {
                        if (__instance.HoursUntilNextGlobalActivity > 0)
                        {
                            __instance.HoursUntilNextGlobalActivity--;
                        }
                        if (__instance.HoursUntilNextGlobalActivity <= 0)
                        {
                            MelonLogger.Msg("[Cartel Comeback] [CartelActivities] trying to start new activity");
                            __instance.TryStartActivity();
                        }
                    }
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(CartelRegionActivities), nameof(CartelRegionActivities.HourPass))]
        class CartelRegionActivitiesHourPassPatch
        {
            static bool Prefix(CartelRegionActivities __instance)
            {
                if (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Defeated)
                {
                    if (InstanceFinder.IsServer && __instance.Active && Singleton<Map>.Instance.GetRegionData(__instance.Region).IsUnlocked && GetFakeRegionInfluence(__instance.Region) > 0.0f)
                    {
                        __instance.HoursUntilNextActivity--;
                        if (__instance.HoursUntilNextActivity <= 0)
                        {
                            __instance.TryStartActivity();
                        }
                    }
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
        

        [HarmonyPatch(typeof(CartelActivity), nameof(CartelActivity.IsRegionValidForActivity))]
        class CartelActivityIsRegionValidForActivityPatch
        {
            private static bool Prefix(EMapRegion region, CartelActivity __instance, ref bool __result)
            {
                if (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Defeated)
                {
                    if (!__instance.enabled)
                    {
                        __result = false;
                        return false;
                    }
                    __result = GetFakeRegionInfluence(region) >= __instance.InfluenceRequirement;
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(CartelActivities), nameof(CartelActivities.GetValidRegionsForActivity))]
        class CartelActivitiesGetValidRegionsForActivityPatch
        {
            private static bool Prefix(ref List<EMapRegion> __result)
            {
                if (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Defeated)
                {
                    List<EMapRegion> validRegionList = new List<EMapRegion>();
                    EMapRegion[] arrayOfRegions = (EMapRegion[])Enum.GetValues(typeof(EMapRegion));
                    foreach (EMapRegion mapRegion in arrayOfRegions)
                    {
                        if (GetFakeRegionInfluence(mapRegion) <= 0.001f)
                            continue;
                        EMapRegion[] adjacentRegions = Singleton<Map>.Instance.GetRegionData(mapRegion).GetAdjacentRegions().ToArray();
                        for (int p = 0; p < Player.PlayerList.Count; p++)
                        {
                            EMapRegion currentRegion = Player.PlayerList[p].CurrentRegion;
                            if (currentRegion == mapRegion || Enumerable.Contains(adjacentRegions, currentRegion))
                            {
                                validRegionList.Add(mapRegion);
                                break;
                            }
                        }
                    }
                    __result = validRegionList;
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(CartelActivities), nameof(CartelActivities.GetInfluenceFraction))]
        class CartelActivitiesGetInfluenceFractionPatch
        {
            private static bool Prefix(CartelActivities __instance, ref float __result)
            {
                if (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Defeated)
                {
                    List<EMapRegion> list = new List<EMapRegion>();
                    for (int i = 0; i < Singleton<Map>.Instance.Regions.Length; i++)
                    {
                        if (Singleton<Map>.Instance.Regions[i].IsUnlocked)
                        {
                            list.Add(Singleton<Map>.Instance.Regions[i].Region);
                        }
                    }
                    float fraction = 0f;
                    for (int j = 0; j < list.Count; j++)
                    {
                        fraction += GetFakeRegionInfluence(list[j]);
                    }
                    __result = fraction / list.Count;
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(CartelActivities), nameof(CartelActivities.TryStartActivity))]
        class CartelActivitiesTryStartActivityPatch
        {
            public static bool Prefix(CartelActivities __instance)
            {
                if (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Defeated)
                {
                    __instance.HoursUntilNextGlobalActivity = CartelActivities.GetNewCooldown();
                    //check if a new activity can begin but it always returns true so im skippin it
                    List<CartelActivity> readyToStartActivities = __instance.GetActivitiesReadyToStart();
                    System.Collections.Generic.List<EMapRegion> validRegionsForActivity = [.. __instance.GetValidRegionsForActivity()];
                    if (readyToStartActivities.Count == 0 || validRegionsForActivity.Count == 0)
                    {
                        MelonLogger.Msg("[Cartel Comeback] [CartelActivities] No available activities or regions to start a global activity.");
                    }
                    validRegionsForActivity.Sort((EMapRegion a, EMapRegion b) => GetFakeRegionInfluence(b).CompareTo(GetFakeRegionInfluence(a)));
                    EMapRegion region = EMapRegion.Northtown;
                    bool flag = false;
                    foreach (EMapRegion item in validRegionsForActivity)
                    {
                        if (UnityEngine.Random.Range(0f, 1f) < GetFakeRegionInfluence(item) * 0.8f)
                        {
                            region = item;
                            flag = true;
                            break;
                        }
                    }
                    if (!flag)
                    {
                        MelonLogger.Msg("[Cartel Comeback] [CartelActivities] No region selected for global activity after influence check.");
                    }
                    readyToStartActivities.Shuffle();
                    for (int num = 0; num < readyToStartActivities.Count; num++)
                    {
                        if (readyToStartActivities[num].IsRegionValidForActivity(region))
                        {
                            __instance.StartGlobalActivity(null, region, num);
                            break;
                        }
                    }
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
  
        [HarmonyPatch(typeof(SpraySurfaceInteraction), nameof(SpraySurfaceInteraction.Interacted))]
        class CleanGraffitiPatch
        {
            private static void Prefix(SpraySurfaceInteraction __instance)
            {
                MelonLogger.Msg($"[Cartel Comeback] Cleaning up Graffiti.");
                if (__instance.SpraySurface.ContainsCartelGraffiti && NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Defeated && PlayerSingleton<PlayerInventory>.Instance.equippedSlot.ItemInstance.ID == "graffiticleaner")
                {
                    NetworkSingleton<LevelManager>.Instance.AddXP(25);
                }
            }
        }

        [HarmonyPatch(typeof(GraffitiBehaviour), nameof(GraffitiBehaviour.Disable))]
        class GriffitiDisablePatch
        {
            private static bool Prefix(GraffitiBehaviour __instance)
            {
                if (!__instance._graffitiCompleted && InstanceFinder.IsServer && __instance._spraySurface != null && NetworkSingleton<Cartel>.InstanceExists && NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Defeated)
                {
                    MelonLogger.Msg($"[Cartel Comeback] [NPC Behaviour][Graffiti] Graffiti behaviour interrupted, awarding {50} XP");
                    NetworkSingleton<LevelManager>.Instance.AddXP(50);
                }
                return true;
            }
        }
        

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Main")
            {
                harmony = new HarmonyLib.Harmony("CartelComeback.Hooks");
                harmony.PatchAll();
            }
            base.OnSceneWasLoaded(buildIndex, sceneName);
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if(sceneName == "Main")
            {
                harmony.UnpatchSelf();
                LoggerInstance.Msg("Unloaded Hooks.");
            }
            base.OnSceneWasUnloaded(buildIndex, sceneName);
        }

        public override void OnDeinitializeMelon()
        {
            base.OnDeinitializeMelon();
            LoggerInstance.Msg("Cartel Comeback Unloaded.");
        }
    }
}