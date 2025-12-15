using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.UI.Phone;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using System.Reflection;
using System;
using System.Drawing;
using static MelonLoader.MelonLogger;
using System.Linq;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using static MelonLoader.Modules.MelonModule;
using System.Diagnostics.Tracing;

[assembly: MelonInfo(typeof(BetterCustomerList.Core), "BetterCustomerList", "1.0.2", "Pyrex", null)]
[assembly: MelonGame("TVGS", "Schedule I")]
namespace BetterCustomerList;

public class Core : MelonMod
{
    private static HarmonyLib.Harmony harmony;
    private MelonPreferences_Category category;
    private static MelonPreferences_Entry<float> fontsize;
    private static MelonPreferences_Entry<bool> showregion;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Better Customer List Loaded.");
        category = MelonPreferences.CreateCategory("BetterCustomerList", "Better Customer List");
        fontsize = category.CreateEntry<float>("TextSize", 22.0f, "Text size", "Set the size of the font used to make each customer entry, to fit more info on screen. changes show after save quit.");
        showregion = category.CreateEntry<bool>("ShowRegion", true, "Show Region", "Add the region the customer is from at the end of their listing. changes show after save quit.");
    }
    private static bool CreateEntry_Hook(Customer customer, CustomerSelector __instance)
    {
        RectTransform component = UnityEngine.Object.Instantiate<GameObject>(__instance.ButtonPrefab, __instance.EntriesContainer).GetComponent<RectTransform>();
        component.Find("Mugshot").GetComponent<UnityEngine.UI.Image>().sprite = customer.NPC.MugshotSprite;
        float relation_percent = (customer.NPC.RelationData.RelationDelta / 5) * 100;
        string full_string = $"<size={fontsize.Value}>{customer.NPC.fullName}    ";
        if (relation_percent == 100.0f)
            full_string += $"<color=#ffcc00>{relation_percent}%</color>";
        else
            full_string += $"{relation_percent}%";
        if (showregion.Value == true)
        {
            full_string += $" ({customer.NPC.Region})</size>";
        }
        else
        {
            full_string += "</size>";
        }
        component.Find("Name").GetComponent<UnityEngine.UI.Text>().text = full_string;
        component.GetComponent<Button>().onClick.AddListener((UnityEngine.Events.UnityAction)(() => __instance.CustomerSelected(customer)));
        __instance.customerEntries.Add(component);
        __instance.entryToCustomer.Add(component, customer);

        Msg("Created entry for " + customer.NPC.fullName);

        SortEntries(__instance);

        return false;
    }

    private static void SortEntries(CustomerSelector instance)
    {
        var entryList = new System.Collections.Generic.List<(RectTransform entry, string name)>();
        
        foreach (var entry in instance.customerEntries)
        {
            if (instance.entryToCustomer.TryGetValue(entry, out Customer customer))
            {
                string customerName = customer.NPC?.fullName?.ToString() ?? "";
                entryList.Add((entry, customerName));
            }
        }

        entryList.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.CurrentCultureIgnoreCase));

        instance.customerEntries.Clear();
        for (int i = 0; i < entryList.Count; i++)
        {
            instance.customerEntries.Add(entryList[i].entry);
            entryList[i].entry.SetSiblingIndex(i);
        }
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