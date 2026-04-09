using BepInEx.Configuration;
using UnityEngine;
using static GClass2175;

namespace EFTBallisticCalculator.Core
{
    public static class HotKeyManager
    {
        public static ConfigEntry<KeyboardShortcut> KeyFcsPannel;
        public static ConfigEntry<KeyboardShortcut> KeyEnvPannel;
        public static ConfigEntry<KeyboardShortcut> KeyFcsClear;

        public static ConfigEntry<KeyboardShortcut> KeyDistUp100;
        public static ConfigEntry<KeyboardShortcut> KeyDistDown100;
        public static ConfigEntry<KeyboardShortcut> KeyDistUp10;
        public static ConfigEntry<KeyboardShortcut> KeyDistDown10;
        public static ConfigEntry<KeyboardShortcut> KeyDistUp1;
        public static ConfigEntry<KeyboardShortcut> KeyDistDown1;

        public static void Init(ConfigFile config)
        {
            KeyFcsPannel = config.Bind("1. Controls", "Toggle FCS HUD", new KeyboardShortcut(KeyCode.KeypadDivide), "开启/关闭火控面板");
            KeyEnvPannel = config.Bind("1. Controls", "Toggle Env HUD", new KeyboardShortcut(KeyCode.KeypadPlus), "开启/关闭环境面板");
            KeyFcsClear = config.Bind("1. Controls", "Clear Target (Unlock)", new KeyboardShortcut(KeyCode.Backspace), "脱锁并清除距离数据");

            KeyDistUp100 = config.Bind("2. Manual Dial", "Distance +100m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftShift), "手动增加距离 100m");
            KeyDistDown100 = config.Bind("2. Manual Dial", "Distance -100m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftShift), "手动减少距离 100m");

            KeyDistUp10 = config.Bind("2. Manual Dial", "Distance +10m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftAlt), "手动增加距离 10m");
            KeyDistDown10 = config.Bind("2. Manual Dial", "Distance -10m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftAlt), "手动减少距离 10m");

            KeyDistUp1 = config.Bind("2. Manual Dial", "Distance +1m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftControl), "手动增加距离 1m");
            KeyDistDown1 = config.Bind("2. Manual Dial", "Distance -1m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftControl), "手动减少距离 1m");
        }

        // 返回当前帧玩家手动按键输入的距离增量
        public static float GetManualDistanceDelta()
        {
            float deltaDist = 0f;
            if (KeyDistUp100.Value.IsDown()) deltaDist += 100f;
            if (KeyDistDown100.Value.IsDown()) deltaDist -= 100f;
            if (KeyDistUp10.Value.IsDown()) deltaDist += 10f;
            if (KeyDistDown10.Value.IsDown()) deltaDist -= 10f;
            if (KeyDistUp1.Value.IsDown()) deltaDist += 1f;
            if (KeyDistDown1.Value.IsDown()) deltaDist -= 1f;
            return deltaDist;
        }
    }
}