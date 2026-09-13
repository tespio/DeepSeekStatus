using System.Globalization;

namespace DeepSeekStatus.Support;

public static class Strings
{
    private static readonly Dictionary<string, (string En, string Zh)> Table = new()
    {
        ["period.peak.title"] = ("Peak hours", "高峰时段"),
        ["period.peak.shortTitle"] = ("Peak", "高峰"),
        ["period.peak.priceText"] = ("Full price (100%)", "高峰价（100%）"),
        ["period.peak.summary"] = ("Peak hours now — billed at the full rate.", "当前为高峰时段，按全额价格计费。"),
        ["period.offPeak.title"] = ("Off-peak hours", "空闲时段"),
        ["period.offPeak.shortTitle"] = ("Off-peak", "空闲"),
        ["period.offPeak.priceText"] = ("Half the peak price (50%)", "高峰价的一半（50%）"),
        ["period.offPeak.summary"] = ("Off-peak hours now — priced at half the peak rate.", "当前为空闲时段，价格是高峰时段的一半。"),

        ["aquarium.beijingTime"] = ("Beijing time", "北京时间"),
        ["aquarium.localTime"] = ("Local time", "本地时间"),

        ["popover.preview.banner"] = ("Previewing: showing “{0}”", "预览中：正在显示「{0}」的样子"),
        ["popover.preview.resume"] = ("Resume live", "恢复实时"),
        ["popover.currentRate"] = ("Current rate", "当前单价"),
        ["popover.countdown.title"] = ("Time until “{0}”", "距离「{0}」还有"),
        ["popover.countdown.detail"] = ("Switches to {1} {0}", "{0} 起转为{1}"),
        ["popover.schedule.title"] = ("Schedule rules", "时段规则"),
        ["popover.schedule.peak.detail"] = ("Mon–Fri 09:00 – 12:00, 14:00 – 18:00 (Beijing time)", "周一至周五 09:00 – 12:00、14:00 – 18:00（北京时间）"),
        ["popover.schedule.offPeak.detail"] = ("All other times (including weekends)", "其余时间（含周末全天）"),
        ["popover.footnote.beijing"] = ("All times are Beijing time (UTC+8).", "所有时间均为北京时间（UTC+8）。"),
        ["popover.footnote.localZone"] = ("All times are your local time ({0}). The peak schedule is defined in Beijing time (UTC+8).", "所有时间均为你本机时间（{0}）。高峰时段按北京时间（UTC+8）计算。"),
        ["popover.option.countdown"] = ("Show countdown near the tray", "在托盘旁显示倒计时"),
        ["popover.option.launchAtLogin"] = ("Launch at login", "开机自动启动"),
        ["popover.preview.label"] = ("Preview", "预览"),
        ["popover.preview.auto"] = ("Auto", "自动"),
        ["popover.quit"] = ("Quit", "退出"),

        ["balance.title"] = ("Account balance", "账户余额"),
        ["balance.refresh"] = ("Refresh", "刷新"),
        ["balance.retry"] = ("Retry", "重试"),
        ["balance.enterKey"] = ("Enter API Key", "输入 API Key"),
        ["balance.key.change"] = ("Change API Key", "更换 API Key"),
        ["balance.key.changeHelp"] = ("Change the saved API key", "更换已保存的 API Key"),
        ["balance.noKey.hint"] = ("Add an API key to see your balance here.", "填写 API Key 后即可在这里看到余额。"),
        ["balance.loading"] = ("Refreshing…", "正在刷新…"),
        ["balance.empty"] = ("No balance information.", "暂无余额信息。"),
        ["balance.granted"] = ("Granted {0}", "赠送 {0}"),
        ["balance.toppedUp"] = ("Topped up {0}", "充值 {0}"),
        ["balance.unavailable"] = ("Not enough balance — API calls are paused.", "余额不足，API 调用已暂停。"),
        ["balance.updated"] = ("Updated {0}", "更新于 {0}"),
        ["balance.key.placeholder"] = ("sk-…", "sk-…"),
        ["balance.key.hint"] = ("Create a key at platform.deepseek.com. It is stored in Windows Credential Manager.", "在 platform.deepseek.com 创建，Key 会保存到 Windows 凭据管理器。"),
        ["balance.key.save"] = ("Save", "保存"),
        ["balance.key.cancel"] = ("Cancel", "取消"),
        ["balance.key.remove"] = ("Remove", "移除"),
        ["balance.key.empty"] = ("Please enter an API key.", "请输入 API Key。"),
        ["balance.key.saveFailed"] = ("Couldn’t save the API key: {0}", "无法保存 API Key：{0}"),
        ["balance.error.unauthorized"] = ("The API key is invalid or expired. Replace it?", "API Key 无效或已过期，是否更换？"),
        ["balance.error.server"] = ("DeepSeek returned an error (HTTP {0}).", "DeepSeek 返回错误（HTTP {0}）。"),
        ["balance.error.network"] = ("Couldn’t reach DeepSeek: {0}", "无法连接 DeepSeek：{0}"),
        ["balance.error.decoding"] = ("DeepSeek returned an unexpected response.", "DeepSeek 返回了无法解析的数据。"),

        ["schedule.legend.peak"] = ("Peak · Full price", "高峰 · 全价"),
        ["schedule.legend.offPeak"] = ("Off-peak · Half price", "空闲 · 半价"),

        ["transition.today"] = ("today {0}", "今天 {0}"),
        ["transition.tomorrow"] = ("tomorrow {0}", "明天 {0}"),
        ["transition.dayAfterTomorrow"] = ("the day after tomorrow at {0}", "后天 {0}"),
        ["transition.weekday"] = ("{0} {1}", "{0} {1}"),

        ["duration.day.one"] = ("1 day", "1 天"),
        ["duration.day.other"] = ("{0} days", "{0} 天"),
        ["duration.hour.one"] = ("1 hour", "1 小时"),
        ["duration.hour.other"] = ("{0} hours", "{0} 小时"),
        ["duration.minute.one"] = ("1 min", "1 分"),
        ["duration.minute.other"] = ("{0} min", "{0} 分"),
        ["duration.second.one"] = ("1 sec", "1 秒"),
        ["duration.second.other"] = ("{0} sec", "{0} 秒"),

        ["menu.preview.peak"] = ("Preview: Peak hours", "预览：高峰时段"),
        ["menu.preview.offPeak"] = ("Preview: Off-peak hours", "预览：空闲时段"),
        ["menu.preview.live"] = ("Follow current time", "跟随当前时间"),
        ["menu.showCountdown"] = ("Show countdown near the tray", "在托盘旁显示倒计时"),
        ["menu.launchAtLogin"] = ("Launch at login", "开机自动启动"),
        ["menu.projectPage"] = ("Project page…", "项目主页…"),
        ["menu.quit"] = ("Quit DeepSeek Status", "退出 DeepSeek Status"),

        ["launchAtLogin.needsApproval"] = ("Allow it in Windows startup settings.", "需要在 Windows 启动设置中允许。"),
        ["launchAtLogin.failed"] = ("Couldn’t change the setting: {0}", "设置失败：{0}"),

        ["tray.tooltip"] = ("DeepSeek Status · {0} · ×{1}", "DeepSeek Status · {0} · ×{1}"),
        ["overlay.tooltip"] = ("Time until {0}", "距离「{0}」还有"),
    };

    public static bool IsChinese { get; } = DetectChinese();

    public static CultureInfo Culture { get; } = IsChinese
        ? CultureInfo.GetCultureInfo("zh-CN")
        : CultureInfo.GetCultureInfo("en-US");

    public static string Get(string key)
    {
        if (!Table.TryGetValue(key, out var entry))
        {
            return key;
        }

        return IsChinese ? entry.Zh : entry.En;
    }

    private static bool DetectChinese()
    {
        var overrideLanguage = Environment.GetEnvironmentVariable("DEEPSEEK_STATUS_LANG");
        if (!string.IsNullOrEmpty(overrideLanguage))
        {
            return overrideLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }

        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase);
    }
}
