using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.UI.Phone;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using System.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(BetterCustomerList.Core), "BetterCustomerList", "1.0.4", "Pyrex", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace BetterCustomerList;

public class Core : MelonMod
{
    private static HarmonyLib.Harmony harmony;
    private MelonPreferences_Category category;
    private static MelonPreferences_Entry<float> fontsize;
    private static MelonPreferences_Entry<bool> showregion;

    private class SortData {
        public RectTransform Entry;
        public string Name;
    }

    public override void OnInitializeMelon()
    {
        category = MelonPreferences.CreateCategory("BetterCustomerList", "Better Customer List");
        fontsize = category.CreateEntry<float>("TextSize", 22.0f, "Text size", "Font size for entries.");
        showregion = category.CreateEntry<bool>("ShowRegion", true, "Show Region", "Show region at end of listing.");
    }

    private static void RefreshEntryText(UnityEngine.UI.Text nameText, Customer customer)
    {
        if (customer == null || customer.NPC == null || customer.NPC.RelationData == null) return;

        float delta = customer.NPC.RelationData.RelationDelta;
        
        // LOGIC FIX: Maps the internal 0.0 - 5.0 relationship range to a 0-100 percentage.
        // This ensures the list accurately reflects high-level relationship progress 
        // instead of capping early or displaying incorrect fractional values.
        float relation_percent = (delta / 5.0f) * 100f;
        relation_percent = UnityEngine.Mathf.Clamp(relation_percent, 0f, 100f);

        // STAGE COLORS: Maps relationship percentages to specific UI colors 
        // to provide better visual feedback for relationship stages.
        string stageColor;
        if (relation_percent <= 20f) stageColor = "#A63536";      // Red (Low)
        else if (relation_percent <= 40f) stageColor = "#D58949"; // Orange
        else if (relation_percent <= 60f) stageColor = "#B1B3B1"; // Gray (Neutral)
        else if (relation_percent <= 80f) stageColor = "#58A2D8"; // Cyan
        else if (relation_percent < 100f) stageColor = "#5DD547"; // Green (High)
        else stageColor = "#00FF00";                             // Intense Green (Max)

        string full_string = $"<size={fontsize.Value}>{customer.NPC.fullName}    ";
        
        // Render percentage with color-coded stage feedback
        full_string += $"<color={stageColor}>{relation_percent.ToString("0")}%</color>";

        if (showregion.Value)
            full_string += $" ({customer.NPC.Region})</size>";
        else
            full_string += "</size>";

        nameText.text = full_string;
    }

    // SYNC FIX: Relationship data often hasn't synchronized when the selector list 
    // is first initialized. This Coroutine triggers a refresh after a short 
    // delay to ensure the UI reflects the actual loaded save data.
    private static IEnumerator DelayedRefresh(UnityEngine.UI.Text nameText, Customer customer)
    {
        yield return new WaitForSeconds(0.2f);
        RefreshEntryText(nameText, customer);
        yield return new WaitForSeconds(1.0f);
        RefreshEntryText(nameText, customer);
    }

    // REFRESH FIX: Iterates through the existing UI entries to force a recalculation
    // of all relationship percentages using current game data.
    private static void OnEnable_Postfix(CustomerSelector __instance)
    {
        if (__instance.customerEntries == null) return;

        foreach (var entry in __instance.customerEntries)
        {
            if (__instance.entryToCustomer.TryGetValue(entry, out Customer customer))
            {
                UnityEngine.UI.Text nameText = entry.Find("Name")?.GetComponent<UnityEngine.UI.Text>();
                if (nameText != null)
                {
                    RefreshEntryText(nameText, customer);
                }
            }
        }
    }

    private static bool CreateEntry_Hook(Customer customer, CustomerSelector __instance)
    {
        GameObject entryObj = UnityEngine.Object.Instantiate<GameObject>(__instance.ButtonPrefab, __instance.EntriesContainer);
        RectTransform component = entryObj.GetComponent<RectTransform>();
        
        component.Find("Mugshot").GetComponent<UnityEngine.UI.Image>().sprite = customer.NPC.MugshotSprite;
        UnityEngine.UI.Text nameText = component.Find("Name").GetComponent<UnityEngine.UI.Text>();
        
        // Initial setup
        RefreshEntryText(nameText, customer);

        // Handle initial data synchronization delay
        MelonCoroutines.Start(DelayedRefresh(nameText, customer));

        component.GetComponent<Button>().onClick.AddListener(new Action(() => __instance.CustomerSelected(customer)));
        
        __instance.customerEntries.Add(component);
        __instance.entryToCustomer.Add(component, customer);

        SortEntries(__instance);
        return false;
    }

    private static void SortEntries(CustomerSelector instance)
    {
        List<SortData> entryList = new List<SortData>();
        foreach (var entry in instance.customerEntries)
        {
            if (instance.entryToCustomer.TryGetValue(entry, out Customer customer))
            {
                entryList.Add(new SortData { 
                    Entry = entry, 
                    Name = customer.NPC?.fullName?.ToString() ?? "" 
                });
            }
        }

        entryList.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        instance.customerEntries.Clear();
        for (int i = 0; i < entryList.Count; i++)
        {
            instance.customerEntries.Add(entryList[i].Entry);
            entryList[i].Entry.SetSiblingIndex(i);
        }
    }

    private static void Hook(HarmonyLib.Harmony harmony, Type targetType, string methodName, string hookMethod, Type[] arguments = null, bool postHook = true)
    {
        MethodInfo method = (arguments == null) 
            ? targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            : targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, arguments);

        MethodInfo hook = typeof(Core).GetMethod(hookMethod, BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null || hook == null) return;

        if (postHook)
            harmony.Patch(method, postfix: new HarmonyMethod(hook));
        else
            harmony.Patch(method, prefix: new HarmonyMethod(hook));
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Main" || sceneName == "Tutorial")
        {
            harmony = new HarmonyLib.Harmony("BetterCustomerList.Hooks");
            
            // Hook for initial entry creation
            Hook(harmony, typeof(CustomerSelector), "CreateEntry", nameof(CreateEntry_Hook), postHook: false);
            
            // FIX: Refresh the list every time the app is opened to ensure data is current.
            Hook(harmony, typeof(CustomerSelector), "OnEnable", nameof(OnEnable_Postfix), postHook: true);
        }
        base.OnSceneWasLoaded(buildIndex, sceneName);
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if((sceneName == "Main" || sceneName == "Tutorial") && harmony != null)
        {
            harmony.UnpatchSelf();
        }
        base.OnSceneWasUnloaded(buildIndex, sceneName);
    }
}
