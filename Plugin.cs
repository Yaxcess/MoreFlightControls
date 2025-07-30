using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using System;
using UnityEngine;
using BepInEx.Configuration;

namespace MoreFlightControls;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[HarmonyPatch]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private readonly Harmony _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

    private static ConfigEntry<KeyCode> RollLeft { get; set; }
    private static ConfigEntry<KeyCode> RollRight { get; set; }
    private static ConfigEntry<KeyCode> ResetRotation { get; set; }
    private static ConfigEntry<float> RollValue { get; set; }
    private static ConfigEntry<float> ResetValue { get; set; }

    private static AvAvatarSubState _prevSubState;
    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} is loaded!");
        RollLeft = Config.Bind("Hotkeys", "Roll left (counter-clockwise)", KeyCode.Q);
        RollRight = Config.Bind("Hotkeys", "Roll right (clockwise)", KeyCode.C);
        ResetRotation = Config.Bind("Hotkeys", "Reset rotation", KeyCode.LeftAlt);
        RollValue = Config.Bind("Multipliers", "Roll speed", 50f);
        ResetValue = Config.Bind("Multipliers", "Reset speed", 1f);
        try
        {
            _harmony.PatchAll();
            Logger.LogInfo("Patched!");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to apply reflection hooks: {ex}");
        }
    }
    
    [HarmonyPatch(typeof(AvAvatarController), "KeyboardUpdate")]
    [HarmonyPostfix]
    private static void KeyboardUpdate_Postfix(AvAvatarController __instance)
    {
        if (_prevSubState == AvAvatarSubState.FLYING && __instance.pSubState != AvAvatarSubState.FLYING)
        {
            Quaternion targetRotation = new Quaternion(0f, __instance.transform.rotation.y, 0f, __instance.transform.rotation.w);
            __instance.transform.rotation = targetRotation;
        }

        if (__instance.pSubState == AvAvatarSubState.FLYING)
        {
            if (KAInput.GetKey(RollLeft.Value))
            {
                Quaternion deltaRotation = Quaternion.AngleAxis(RollValue.Value * Time.deltaTime, Vector3.forward);
                __instance.transform.rotation *= deltaRotation;
            }
            if (KAInput.GetKey(RollRight.Value))
            {
                Quaternion deltaRotation = Quaternion.AngleAxis(-RollValue.Value * Time.deltaTime, Vector3.forward);
                __instance.transform.rotation *= deltaRotation;
            }
        }
        if (KAInput.GetKey(ResetRotation.Value) || __instance.pVelocity.magnitude < 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(__instance.transform.forward, Vector3.up);
            targetRotation.x = 0f;
            __instance.transform.rotation = Quaternion.Slerp(__instance.transform.rotation,
                targetRotation, ResetValue.Value * Time.deltaTime);
        }
        _prevSubState = __instance.pSubState;
    }

    [HarmonyPatch(typeof(CaAvatarCam), "LateUpdate")]
    [HarmonyPrefix]
    private static bool CalcWorldPos_Prefix(CaAvatarCam __instance)
    {
        var mCamData = typeof(CaAvatarCam).GetField("mCamData", BindingFlags.Instance | BindingFlags.NonPublic);
        var cam = (CaAvatarCam.CamInterpData[])mCamData.GetValue(__instance);
        for (int i = 0; i < cam.Length; ++i)
        {
            cam[i].lookQuat = new Quaternion(cam[i].lookQuat.x * 0.5f, cam[i].lookQuat.y, 0f, cam[i].lookQuat.w);
        }
        return true;
    }
}
