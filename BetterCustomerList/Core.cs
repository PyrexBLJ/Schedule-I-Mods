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

    // Tracking instances manually to maintain high FPS by avoiding FindObjectsOfType.
    private static List<CustomerSelector> _trackedSelectors = new List<CustomerSelector>();
    private static Dictionary<int, float> _activationTimes = new Dictionary<int, float>();
    private static Dictionary<int, float> _lastRefreshTimes = new Dictionary<int, float>();

    private float _nextUpdateTick = 0f;

    private class SortData
    {
        public RectTransform Entry;
        public string Name;
    }

    public override void OnInitializeMelon()
    {
        category = MelonPreferences.CreateCategory("BetterCustomerList", "Better Customer List");
        fontsize = category.CreateEntry<float>("TextSize", 22.0f, "Text size", "Font size for entries.");
        showregion = category.CreateEntry<bool>("ShowRegion", true, "Show Region", "Show region at end of listing.");
        MelonLogger.Msg("Better Customer List [v1.0.4] Initialized.");
    }

    private static void RefreshEntryText(UnityEngine.UI.Text nameText, Customer customer)
    {
        if (customer == null || customer.NPC == null || customer.NPC.RelationData == null) return;

        // RELATIONSHIP LOGIC: Maps internal 0.0 - 5.0 range to 0-100% display.
        float delta = customer.NPC.RelationData.RelationDelta;
        float relation_percent = UnityEngine.Mathf.Clamp((delta / 5.0f) * 100f, 0f, 100f);

        // STAGE COLORS: Visual feedback based on relationship tiers.
        string stageColor;
        if (relation_percent <= 20f) stageColor = "#A63536";      // Red
        else if (relation_percent <= 40f) stageColor = "#D58949"; // Orange
        else if (relation_percent <= 60f) stageColor = "#B1B3B1"; // Gray
        else if (relation_percent <= 80f) stageColor = "#58A2D8"; // Cyan
        else if (relation_percent < 100f) stageColor = "#5DD547"; // Green
        else stageColor = "#00FF00";                             // Max Green

        string full_string = $"<size={fontsize.Value}>{customer.NPC.fullName}    ";
        full_string += $"<color={stageColor}>{relation_percent.ToString("0")}%</color>";

        if (showregion.Value) full_string += $" ({customer.NPC.Region})</size>";
        else full_string += "</size>";

        if (nameText.text != full_string)
        {
            nameText.text = full_string;
        }
    }

    private void RefreshSelector(CustomerSelector selector, string context)
    {
        if (selector.customerEntries == null || selector.entryToCustomer == null) return;

        // Log maintenance only, as requested.
        if (context == "Maintenance")
        {
            MelonLogger.Msg($"Refreshing {selector.name} ({context})");
        }

        foreach (var entry in selector.customerEntries)
        {
            if (selector.entryToCustomer.TryGetValue(entry, out Customer customer))
            {
                var nameText = entry.Find("Name")?.GetComponent<UnityEngine.UI.Text>();
                if (nameText != null) RefreshEntryText(nameText, customer);
            }
        }
    }

    public override void OnUpdate()
    {
        if (Time.time < _nextUpdateTick) return;
        _nextUpdateTick = Time.time + 0.1f;

        for (int i = _trackedSelectors.Count - 1; i >= 0; i--)
        {
            var selector = _trackedSelectors[i];
            if (selector == null || selector.gameObject == null)
            {
                _trackedSelectors.RemoveAt(i);
                continue;
            }

            int id = selector.GetHashCode();

            // VISIBILITY CHECK: Only updates if active and Y-position is within view.
            RectTransform rt = selector.GetComponent<RectTransform>();
            bool isActuallyOnScreen = selector.gameObject.activeInHierarchy && (rt != null && rt.position.y > -200f);

            if (isActuallyOnScreen)
            {
                if (!_activationTimes.ContainsKey(id))
                {
                    _activationTimes[id] = Time.time;
                    _lastRefreshTimes[id] = 0f;
                    MelonLogger.Msg($"[BetterCustomerList] Visibility DETECTED for {selector.name}. Starting 3s update burst.");
                }

                float timeSinceActive = Time.time - _activationTimes[id];

                if (timeSinceActive < 3.0f)
                {
                    if (Time.time > _lastRefreshTimes[id] + 0.1f)
                    {
                        _lastRefreshTimes[id] = Time.time;
                        RefreshSelector(selector, "Burst");
                    }
                }
                else
                {
                    if (Time.time > _lastRefreshTimes[id] + 2.0f)
                    {
                        _lastRefreshTimes[id] = Time.time;
                        RefreshSelector(selector, "Maintenance");
                    }
                }
            }
            else
            {
                if (_activationTimes.ContainsKey(id))
                {
                    _activationTimes.Remove(id);
                    _lastRefreshTimes.Remove(id);
                    MelonLogger.Msg($"[BetterCustomerList] Visibility LOST for {selector.name}. Update cycle stopped.");
                }
            }
        }
    }

    private static bool CreateEntry_Hook(Customer customer, CustomerSelector __instance)
    {
        if (!_trackedSelectors.Contains(__instance))
        {
            _trackedSelectors.Add(__instance);
        }

        GameObject entryObj = UnityEngine.Object.Instantiate<GameObject>(__instance.ButtonPrefab, __instance.EntriesContainer);
        RectTransform component = entryObj.GetComponent<RectTransform>();

        component.Find("Mugshot").GetComponent<UnityEngine.UI.Image>().sprite = customer.NPC.MugshotSprite;
        UnityEngine.UI.Text nameText = component.Find("Name").GetComponent<UnityEngine.UI.Text>();

        RefreshEntryText(nameText, customer);

        component.GetComponent<Button>().onClick.AddListener(new Action(() => __instance.CustomerSelected(customer)));
        __instance.customerEntries.Add(component);
        __instance.entryToCustomer.Add(component, customer);

        SortEntriesLocally(__instance);
        return false;
    }

    private static void SortEntriesLocally(CustomerSelector instance)
    {
        List<SortData> entryList = new List<SortData>();
        foreach (var entry in instance.customerEntries)
        {
            if (instance.entryToCustomer.TryGetValue(entry, out Customer customer))
            {
                entryList.Add(new SortData { Entry = entry, Name = customer.NPC?.fullName?.ToString() ?? "" });
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

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Main" || sceneName == "Tutorial")
        {
            _trackedSelectors.Clear();
            harmony = new HarmonyLib.Harmony("BetterCustomerList.SyncFix");
            MethodInfo createMethod = typeof(CustomerSelector).GetMethod("CreateEntry", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo createHook = typeof(Core).GetMethod("CreateEntry_Hook", BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(createMethod, prefix: new HarmonyMethod(createHook));
            MelonLogger.Msg("Better Customer List hooks injected.");
        }
        base.OnSceneWasLoaded(buildIndex, sceneName);
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if ((sceneName == "Main" || sceneName == "Tutorial") && harmony != null)
        {
            harmony.UnpatchSelf();
            _trackedSelectors.Clear();
            _activationTimes.Clear();
            _lastRefreshTimes.Clear();
        }
        base.OnSceneWasUnloaded(buildIndex, sceneName);
    }
}
