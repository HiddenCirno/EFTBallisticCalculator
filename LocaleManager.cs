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
            CurrentLanguage = config.Bind("HUD Global", "Language / 语言", AppLanguage.English, "选择 HUD 的显示语言");
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
}
