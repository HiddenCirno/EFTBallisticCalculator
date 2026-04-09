using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace EFTBallisticCalculator
{
    public enum AppLanguage
    {
        English,
        简体中文
    }
    public enum CfgLanguage
    {
        English,
        简体中文
    }

    public static class LocaleManager
    {
        public static ConfigEntry<AppLanguage> CurrentLanguage;

        // 核心翻译字典：[语言类型 -> [Key -> 翻译文本]]
        private static readonly Dictionary<AppLanguage, Dictionary<string, string>> _translations = new Dictionary<AppLanguage, Dictionary<string, string>>()
        {
            {
                AppLanguage.English, new Dictionary<string, string>
                {
                    // --- 通用与状态 ---
                    { "no_data", "---" },
                    { "fcs_no_lock", "NO LOCK" },
                    { "fcs_status_offline", "OFFLINE" },
                    { "fcs_status_no_ammo", "NO_AMMO" },
                    { "fcs_status_standby", "STANDBY" },
                    { "fcs_status_tracked", "TRACKED" },
                    { "fcs_status_optic_sync", "OPTIC_SYNC" },
                    
                    // --- FCS 标题 ---
                    { "fcs_title_no_weapon", "[ DIRECTOR FCS: NO WEAPON ]" },
                    { "fcs_title_no_ammo", "[ DIRECTOR FCS: NO AMMO ]" },
                    { "fcs_title_locked", "[ DIRECTOR FCS: TARGET LOCKED ]" },
                    { "fcs_title_standby", "[ DIRECTOR FCS: STANDBY ]" },

                    // --- FCS 标签 (Label) : 仅接受拼装好的字符串 ---
                    { "fcs_lbl_heading", "HEADING   : {0}" },
                    { "fcs_lbl_range", "TGT RANGE : {0}" },
                    { "fcs_lbl_incline", "INCLINE   : {0}" },
                    { "fcs_lbl_cant", "CANT ANGL : {0}" },
                    { "fcs_lbl_tof", "TIME FLGT : {0}" },
                    { "fcs_lbl_vel", "MUZZLE VEL: {0}" },
                    { "fcs_lbl_mass", "PROJ MASS : {0}" },
                    { "fcs_lbl_sys", "SYSTEM    : {0} | {1}" },

                    // --- FCS 数值格式化 (Value) ---
                    { "fcs_val_heading", "{0:000}° [{1}]" },
                    { "fcs_val_range", "{0:F1} M" },
                    { "fcs_val_angle", "{0:-0.0;+0.0;0.0}°" },
                    { "fcs_val_tof", "{0:F3} SEC" },
                    { "fcs_val_speed", "{0:F1} M/S" },
                    { "fcs_val_mass", "{0:F1} G" },
                    { "fcs_val_bc", "(BC: {0:F3})" },

                    // --- Env 标签 (Label) ---
                    { "env_title", "<b>[ ENVIRONMENT SENSORS ACTIVE ]</b>" },
                    { "env_lbl_loc", "LOCATION  : {0}" },
                    { "env_lbl_gps", "GPS COORD : {0} | {1}" },
                    { "env_lbl_time", "LOCAL TIME: {0} | REAL: {1}" },
                    { "env_lbl_wind_dir", "WIND DIR  : {0}" },
                    { "env_lbl_cross", "CROSSWIND : {0}" },
                    { "env_lbl_vect", "VECT WIND : {0}" },
                    { "env_lbl_alt", "ALT (MSL) : {0}" },
                    { "env_lbl_press", "PRESSURE  : {0}" },
                    { "env_lbl_hum", "HUMIDITY  : {0}" },
                    { "env_lbl_temp", "TEMP      : {0}" },

                    // --- Env 数值格式化 (Value) ---
                    { "env_val_lat", "{0:F5}° N" },
                    { "env_val_lon", "{0:F5}° E" },
                    { "env_val_wind_dir", "{0:000}° [{1}] | {2:F1} M/S" },
                    { "env_val_wind_spd", "{0:F1} M/S [{1}]" },
                    { "env_val_alt", "{0:F1} M" },
                    { "env_val_press", "{0:F1} HPA" },
                    { "env_val_hum", "{0:F1} %" },
                    { "env_val_temp", "{0:F1} °C | {1:F1} °F" },

                    { "env_dir_left", "◄ L" },
                    { "env_dir_right", "R ►" },
                    { "env_dir_head", "HEAD" },
                    { "env_dir_tail", "TAIL" }
                }
            },
            {
                AppLanguage.简体中文, new Dictionary<string, string>
                {
                    // --- 通用与状态 ---
                    { "no_data", "---" },
                    { "fcs_no_lock", "无目标" },
                    { "fcs_status_offline", "离线" },
                    { "fcs_status_no_ammo", "缺少弹药" },
                    { "fcs_status_standby", "待命" },
                    { "fcs_status_tracked", "已锁定" },
                    { "fcs_status_optic_sync", "瞄准中" },
                    
                    // --- FCS 标题 ---
                    { "fcs_title_no_weapon", "[ 火控计算机 : 无武器 ]" },
                    { "fcs_title_no_ammo",   "[ 火控计算机 : 缺少弹药 ]" },
                    { "fcs_title_locked",    "[ 火控计算机 : 目标锁定 ]" },
                    { "fcs_title_standby",   "[ 火控计算机 : 待机中 ]" },

                    // --- FCS 标签 (Label) : 仅接受拼装好的字符串 ---
                    { "fcs_lbl_heading", "目视方向 : {0}" },
                    { "fcs_lbl_range",   "目标距离 : {0}" },
                    { "fcs_lbl_incline", "俯仰角   : {0}" },
                    { "fcs_lbl_cant",    "水平倾角 : {0}" },
                    { "fcs_lbl_tof",     "飞行时间 : {0}" },
                    { "fcs_lbl_vel",     "离膛速度 : {0}" },
                    { "fcs_lbl_mass",    "弹头重量 : {0}" },
                    { "fcs_lbl_sys",     "系统状态 : {0} | {1}" },

                    // --- FCS 数值格式化 (Value) ---
                    { "fcs_val_heading", "{0:000}° [{1}]" },
                    { "fcs_val_range",   "{0:F1} 米" },
                    { "fcs_val_angle",   "{0:-0.0;+0.0;0.0}°" },
                    { "fcs_val_tof",     "{0:F3} 秒" },
                    { "fcs_val_speed",   "{0:F1} 米/秒" },
                    { "fcs_val_mass",    "{0:F1} 克" },
                    { "fcs_val_bc",      "(弹道系数: {0:F3})" },

                    // --- Env 标签 (Label) ---
                    { "env_title", "<b>[ 环境数据 ]</b>" },
                    { "env_lbl_loc",  "当前位置 : {0}" },
                    { "env_lbl_gps",  "坐标信息 : {1} | {0}" },
                    { "env_lbl_time", "当地时间 : {0} | 现实时间: {1}" },
                    { "env_lbl_wind_dir", "风向     : {0}" },
                    { "env_lbl_cross",    "水平风速 : {0}" },
                    { "env_lbl_vect",     "纵直风速 : {0}" },
                    { "env_lbl_alt",      "海拔高度 : {0}" },
                    { "env_lbl_press",    "气压    : {0}" },
                    { "env_lbl_hum",      "湿度    : {0}" },
                    { "env_lbl_temp",     "温度    : {0}" },

                    // --- Env 数值格式化 (Value) ---
                    { "env_val_lat", "北纬 {0:F5}°" },
                    { "env_val_lon", "东经 {0:F5}°" },
                    { "env_val_wind_dir", "{0:000}° [{1}] | {2:F1} 米/秒" },
                    { "env_val_wind_spd", "{0:F1} 米/秒 [{1}]" },
                    { "env_val_alt", "{0:F1} 米" },
                    { "env_val_press", "{0:F1} HPA" },
                    { "env_val_hum", "{0:F1} %" },
                    { "env_val_temp", "{0:F1} °C | {1:F1} °F" },

                    { "env_dir_left", "◄ 左" },
                    { "env_dir_right", "右 ►" },
                    { "env_dir_head", "逆风" },
                    { "env_dir_tail", "顺风" }
                }
            }
        };

        // 供 PluginsCore.Awake() 调用
        public static void Init(ConfigFile config)
        {
            // 绑定 F12 设置项
            CurrentLanguage = config.Bind(
                "Language / 语言", 
                "HUD Language", 
                AppLanguage.English,
                new ConfigDescription(
                        CfgLocaleManager.Get("cfg_lang_ui_desc"), // 翻译后的描述
                        null, // 可接受的值范围 (AcceptableValues)，填 null 即可
                        new ConfigurationManagerAttributes
                            {
                                DispName = CfgLocaleManager.Get("cfg_lang_ui_name") // 翻译后的显示名称！
                            }
                    )
                );
            }

        // 核心获取方法：根据当前选择的语言返回文本
        public static string Get(string key)
        {
            if (_translations.TryGetValue(CurrentLanguage.Value, out var langDict))
            {
                if (langDict.TryGetValue(key, out var text))
                {
                    return text;
                }
            }
            // 防呆机制：如果字典里忘了写这个翻译，直接把 Key 打印出来，提醒你补上
            return $"[{key}]";
        }
    }
    public static class CfgLocaleManager
    {
        public static ConfigEntry<CfgLanguage> CurrentLanguage;

        // 核心翻译字典：[语言类型 -> [Key -> 翻译文本]]
        private static readonly Dictionary<CfgLanguage, Dictionary<string, string>> _translations = new Dictionary<CfgLanguage, Dictionary<string, string>>()
        {
            {
                CfgLanguage.English, new Dictionary<string, string>
                {
                    // --- General ---
                    { "cfg_lang_ui_name", "HUD Language" },
                    { "cfg_lang_ui_desc", "Change HUD UI's display language." },

                    // --- 1. Controls ---
                    { "cfg_hotkey_fcs_name", "Toggle FCS HUD" },
                    { "cfg_hotkey_fcs_desc", "Toggle the FCS panel display on/off." },
                    { "cfg_hotkey_env_name", "Toggle Env HUD" },
                    { "cfg_hotkey_env_desc", "Toggle the Environment panel display on/off." },
                    { "cfg_hotkey_clear_name", "Clear Target (Unlock)" },
                    { "cfg_hotkey_clear_desc", "Unlock the target and clear distance data." },

                    // --- 2. Manual Dial ---
                    { "cfg_dial_up_100_name", "Distance +100m" },
                    { "cfg_dial_up_100_desc", "Manually increase locked distance by 100m." },
                    { "cfg_dial_down_100_name", "Distance -100m" },
                    { "cfg_dial_down_100_desc", "Manually decrease locked distance by 100m." },
                    { "cfg_dial_up_10_name", "Distance +10m" },
                    { "cfg_dial_up_10_desc", "Manually increase locked distance by 10m." },
                    { "cfg_dial_down_10_name", "Distance -10m" },
                    { "cfg_dial_down_10_desc", "Manually decrease locked distance by 10m." },
                    { "cfg_dial_up_1_name", "Distance +1m" },
                    { "cfg_dial_up_1_desc", "Manually increase locked distance by 1m." },
                    { "cfg_dial_down_1_name", "Distance -1m" },
                    { "cfg_dial_down_1_desc", "Manually decrease locked distance by 1m." },

                    // --- Ballistics Calculator ---
                    { "cfg_calc_scale_name", "Impact Marker Scale" },
                    { "cfg_calc_scale_desc", "Determines the visual size of the 3D impact marker." },

                    // --- Left HUD Pannel Global ---
                    { "cfg_hud_x_name", "Global X Offset" },
                    { "cfg_hud_x_desc", "Absolute distance of the entire HUD from the left side of the screen." },
                    { "cfg_hud_y_name", "Global Y Offset" },
                    { "cfg_hud_y_desc", "Y-axis offset of the entire HUD relative to the center of the screen." },
                    { "cfg_hud_scale_name", "Global Scale" },
                    { "cfg_hud_scale_desc", "Global UI scaling factor for all panels." },
                    { "cfg_hud_space_name", "Panel Spacing" },
                    { "cfg_hud_space_desc", "Vertical spacing between stacked panels." },
                    { "cfg_hud_rb_ui_name", "Enable Rainbow UI" },
                    { "cfg_hud_rb_ui_desc", "Make your HUD panels look cool like a rainbow!" },
                    { "cfg_hud_rb_spd_name", "Rainbow UI Speed" },
                    { "cfg_hud_rb_spd_desc", "Controls the color cycling speed of the Rainbow UI." },

                    // --- FCS Panel ---
                    { "cfg_fcs_x_name", "FCS X Offset" },
                    { "cfg_fcs_x_desc", "Independent X-axis offset for the FCS panel." },
                    { "cfg_fcs_y_name", "FCS Y Offset" },
                    { "cfg_fcs_y_desc", "Independent Y-axis offset for the FCS panel." },
                    { "cfg_fcs_scale_name", "FCS Scale" },
                    { "cfg_fcs_scale_desc", "Independent scaling factor for the FCS panel." },
                    { "cfg_fcs_active_name", "Show FCS Panel" },
                    { "cfg_fcs_active_desc", "Enable or disable the FCS panel rendering." },
                    { "cfg_fcs_color_name", "FCS Color" },
                    { "cfg_fcs_color_desc", "Customize the UI color of the FCS panel." },

                    // --- Environment Panel ---
                    { "cfg_env_x_name", "Env X Offset" },
                    { "cfg_env_x_desc", "Independent X-axis offset for the Environment panel." },
                    { "cfg_env_y_name", "Env Y Offset" },
                    { "cfg_env_y_desc", "Independent Y-axis offset for the Environment panel." },
                    { "cfg_env_scale_name", "Env Scale" },
                    { "cfg_env_scale_desc", "Independent scaling factor for the Environment panel." },
                    { "cfg_env_active_name", "Show Env Panel" },
                    { "cfg_env_active_desc", "Enable or disable the Environment panel rendering." },
                    { "cfg_env_color_name", "Env Color" },
                    { "cfg_env_color_desc", "Customize the UI color of the Environment panel." }
                }
            },
            {
                CfgLanguage.简体中文, new Dictionary<string, string>
                {
                    // --- General ---
                    { "cfg_lang_cfg_name", "配置菜单语言" },
                    { "cfg_lang_cfg_desc", "更改 F12 配置菜单的显示语言（需要重启游戏生效）。" },
                    { "cfg_lang_ui_name", "HUD 界面语言" },
                    { "cfg_lang_ui_desc", "更改游戏内 HUD 界面的显示语言（即时生效）。" },

                    // --- 1. Controls ---
                    { "cfg_hotkey_fcs_name", "切换火控面板" },
                    { "cfg_hotkey_fcs_desc", "开启/关闭左侧的火控计算机面板。" },
                    { "cfg_hotkey_env_name", "切换环境面板" },
                    { "cfg_hotkey_env_desc", "开启/关闭左侧的环境数据面板。" },
                    { "cfg_hotkey_clear_name", "脱锁并清除数据" },
                    { "cfg_hotkey_clear_desc", "强制解除锁定，并清空当前测距数据。" },

                    // --- 2. Manual Dial ---
                    { "cfg_dial_up_100_name", "距离 +100m" },
                    { "cfg_dial_up_100_desc", "手动校准距离：增加 100 米目标距离。" },
                    { "cfg_dial_down_100_name", "距离 -100m" },
                    { "cfg_dial_down_100_desc", "手动校准距离：减少 100 米目标距离。" },
                    { "cfg_dial_up_10_name", "距离 +10m" },
                    { "cfg_dial_up_10_desc", "手动校准距离：增加 10 米目标距离。" },
                    { "cfg_dial_down_10_name", "距离 -10m" },
                    { "cfg_dial_down_10_desc", "手动校准距离：减少 10 米目标距离。" },
                    { "cfg_dial_up_1_name", "距离 +1m" },
                    { "cfg_dial_up_1_desc", "手动校准距离：增加 1 米目标距离。" },
                    { "cfg_dial_down_1_name", "距离 -1m" },
                    { "cfg_dial_down_1_desc", "手动校准距离：减少 1 米目标距离。" },

                    // --- Ballistics Calculator ---
                    { "cfg_calc_scale_name", "着弹点标记比例" },
                    { "cfg_calc_scale_desc", "视锥等距算法参数：决定 3D 物理预测球的标记大小。" },

                    // --- Left HUD Pannel Global ---
                    { "cfg_hud_x_name", "全局 X 轴偏移" },
                    { "cfg_hud_x_desc", "HUD 整体距离屏幕左侧的绝对像素距离。" },
                    { "cfg_hud_y_name", "全局 Y 轴偏移" },
                    { "cfg_hud_y_desc", "HUD 整体相对屏幕垂直中心的 Y 轴偏移量。" },
                    { "cfg_hud_scale_name", "全局缩放比例" },
                    { "cfg_hud_scale_desc", "左侧所有 HUD 模块的全局 UI 缩放乘数。" },
                    { "cfg_hud_space_name", "面板垂直间距" },
                    { "cfg_hud_space_desc", "各个 UI 模块上下堆叠时的垂直间隔空间。" },
                    { "cfg_hud_rb_ui_name", "启用彩虹 UI" },
                    { "cfg_hud_rb_ui_desc", "让你的UI面板变得非常Coooool~" },
                    { "cfg_hud_rb_spd_name", "彩虹 UI 闪烁速度" },
                    { "cfg_hud_rb_spd_desc", "控制彩虹霓虹灯色彩的平滑循环速度。" },

                    // --- FCS Panel ---
                    { "cfg_fcs_x_name", "火控面板 X 偏移" },
                    { "cfg_fcs_x_desc", "火控面板独立的 X 轴横向偏移量。" },
                    { "cfg_fcs_y_name", "火控面板 Y 偏移" },
                    { "cfg_fcs_y_desc", "火控面板独立的 Y 轴纵向偏移量。" },
                    { "cfg_fcs_scale_name", "火控面板缩放" },
                    { "cfg_fcs_scale_desc", "火控面板独立的 UI 缩放乘数。" },
                    { "cfg_fcs_active_name", "显示火控面板" },
                    { "cfg_fcs_active_desc", "是否在屏幕上渲染火控计算机系统数据。" },
                    { "cfg_fcs_color_name", "火控面板颜色" },
                    { "cfg_fcs_color_desc", "自定义火控计算机面板的静态基础颜色。" },

                    // --- Environment Panel ---
                    { "cfg_env_x_name", "环境面板 X 偏移" },
                    { "cfg_env_x_desc", "环境数据面板独立的 X 轴横向偏移量。" },
                    { "cfg_env_y_name", "环境面板 Y 偏移" },
                    { "cfg_env_y_desc", "环境数据面板独立的 Y 轴纵向偏移量。" },
                    { "cfg_env_scale_name", "环境面板缩放" },
                    { "cfg_env_scale_desc", "环境数据面板独立的 UI 缩放乘数。" },
                    { "cfg_env_active_name", "显示环境面板" },
                    { "cfg_env_active_desc", "是否在屏幕上渲染环境数据。" },
                    { "cfg_env_color_name", "环境面板颜色" },
                    { "cfg_env_color_desc", "自定义环境数据面板的静态基础颜色。" }
                }
            }
        };

        // 供 PluginsCore.Awake() 调用
        public static void Init(ConfigFile config)
        {
            // 绑定 F12 设置项
            CurrentLanguage = config.Bind(
                "Language / 语言",
                "Menu Language / 配置菜单语言",
                CfgLanguage.English,
                "Change Configuration menu's language (Requires game restart). / 更改 F12 配置菜单的显示语言（需要重启游戏生效）。"
                );
        }

        // 核心获取方法：根据当前选择的语言返回文本
        public static string Get(string key)
        {
            if (_translations.TryGetValue(CurrentLanguage.Value, out var langDict))
            {
                if (langDict.TryGetValue(key, out var text))
                {
                    return text;
                }
            }
            // 防呆机制：如果字典里忘了写这个翻译，直接把 Key 打印出来，提醒你补上
            return $"[{key}]";
        }
    }
    internal sealed class ConfigurationManagerAttributes
    {
        // 用于覆盖 F12 菜单中显示的配置项名称 (Key)
        public string DispName;

        // 甚至可以用来排序，数字越小越靠上
        public int? Order;

        // 如果设置为 true，这个设置项在高级设置里才显示
        public bool? Advanced;
    }
}
