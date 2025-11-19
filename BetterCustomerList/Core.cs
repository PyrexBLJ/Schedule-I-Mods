using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.UI.Phone;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using System.Reflection;
using System;

[assembly: MelonInfo(typeof(BetterCustomerList.Core), "BetterCustomerList", "1.0.0", "Pyrex", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace BetterCustomerList
{
    public class Core : MelonMod
    {
        private static HarmonyLib.Harmony harmony;
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Better Customer List Loaded.");
        }
        private static bool CreateEntry_Hook(Customer customer, CustomerSelector __instance)
        {
            RectTransform component = UnityEngine.Object.Instantiate<GameObject>(__instance.ButtonPrefab, __instance.EntriesContainer).GetComponent<RectTransform>();
            component.Find("Mugshot").GetComponent<UnityEngine.UI.Image>().sprite = customer.NPC.MugshotSprite;
            float relation_percent = (customer.NPC.RelationData.RelationDelta / 5) * 100;
            string full_string;
            if (relation_percent == 100.0f)
                full_string = $"{customer.NPC.fullName}    <color=#ffcc00>{relation_percent}%</color>";
            else
                full_string = $"{customer.NPC.fullName}    {relation_percent}%";
            component.Find("Name").GetComponent<UnityEngine.UI.Text>().text = full_string;
            component.GetComponent<Button>().onClick.AddListener((UnityEngine.Events.UnityAction)(() => __instance.CustomerSelected(customer)));
            __instance.customerEntries.Add(component);
            __instance.entryToCustomer.Add(component, customer);
            return false;
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
            if (sceneName == "Main" || sceneName == "Tutorial")
            {
                harmony = new HarmonyLib.Harmony("BetterCustoimerList.Hooks");
                Hook(harmony, typeof(CustomerSelector), "CreateEntry", nameof(CreateEntry_Hook), postHook:false);

            }
                base.OnSceneWasLoaded(buildIndex, sceneName);
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if(sceneName == "Main" || sceneName == "Tutorial")
            {
                harmony.UnpatchSelf();
                MelonLogger.Msg("Unloaded Hooks.");
            }
            base.OnSceneWasUnloaded(buildIndex, sceneName);
        }

        public override void OnDeinitializeMelon()
        {
            LoggerInstance.Msg("Better Customer List Unloaded.");
            base.OnDeinitializeMelon();
        }

    }
}