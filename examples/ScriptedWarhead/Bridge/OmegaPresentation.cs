using System;
using System.Globalization;
using LabApi.Features.Wrappers;

namespace ScriptedWarheadEPLab.Bridge;

internal static class OmegaPresentation
{
    private const string FallbackSpeech = "OMEGA WARHEAD IN 180 SECONDS";

    private const string EnglishSubtitle =
        "<b><color=red>SECURITY ALERT</color><color=orange>·</color><color=#00B7EB>OMEGA WARHEAD DETONATION SEQUENCE INITIATED</color></b>\n" +
        "<size=15>Due to a major containment failure, the <color=red>Omega</color> Warhead has been forcibly activated and cannot be stopped. This facility will self-destruct in 180 seconds.</size>";

    private const string ChineseSubtitle =
        "<b><color=red>安全警报</color><color=orange>·</color><color=#00B7EB>Omega核弹引爆程序已启动</color></b>\n" +
        "<size=15>由于站点发生重大收容失效事件,<color=red>Omega</color>核弹头被迫启动且无法终止,本设施将在倒计时:180秒后自毁</size>";

    public static void PlaySubtitle(bool chinese, float configuredDuration)
    {
        float duration = Math.Max(1f, configuredDuration);
        string silentCarrier = string.Format(
            CultureInfo.InvariantCulture,
            "$VOL_0 $SPAC_{0:0.###} OMEGA",
            duration);
        Announcer.Message(silentCarrier, chinese ? ChineseSubtitle : EnglishSubtitle, playBackground: false, priority: 100f, glitchScale: 0f);
    }

    public static void PlayFallback(bool chinese)
        => Announcer.Message(FallbackSpeech, chinese ? ChineseSubtitle : EnglishSubtitle, playBackground: true, priority: 100f, glitchScale: 0f);
}
