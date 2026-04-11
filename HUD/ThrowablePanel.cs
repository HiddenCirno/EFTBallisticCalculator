using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using System.Collections.Generic;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class ThrowablePanel
    {
        public enum TextAlign { Left, Center, Right }

        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<TextAlign> Alignment;
        public static ConfigEntry<Color> Color;

        private class ThrowableData
        {
            public string Name;
            public int Count;
        }

        private static readonly List<ThrowableData> _throwablesCache = new List<ThrowableData>();
        private static float _lastScanTime = 0f;

        public static void InitCfg(ConfigFile config)
        {
            OffsetY = config.Bind("Top Panel / 投掷装备", "投掷板 Y轴偏移", -5f, new ConfigDescription("面板的纵向微调"));
            Scale = config.Bind("Top Panel / 投掷装备", "投掷板 缩放比例", 1.2f, new ConfigDescription("面板整体缩放"));
            Active = config.Bind("Top Panel / 投掷装备", "显示投掷物", true, new ConfigDescription("是否启用可用投掷物显示"));
            Alignment = config.Bind("Top Panel / 投掷装备", "对齐方式", TextAlign.Center, new ConfigDescription("投掷物列表在屏幕上的横向对齐方式"));
            Color = config.Bind("Top Panel / 投掷装备", "颜色设置", new Color(0.9f, 0.7f, 0.2f, 0.85f), new ConfigDescription("警告/投掷物颜色"));
        }

        // 精准扫描口袋和弹挂
        private static void UpdateThrowables(Player player)
        {
            if (Time.time - _lastScanTime < 1f) return;
            _lastScanTime = Time.time;

            _throwablesCache.Clear();
            var equipment = player.Inventory?.Equipment;
            if (equipment == null) return;

            Dictionary<string, int> grenadeCounts = new Dictionary<string, int>();

            // 局部方法：专门扫描指定容器内的物品
            void ScanContainerForGrenades(Item containerItem)
            {
                if (containerItem == null) return;
                foreach (var item in containerItem.GetAllItems())
                {
                    if (item is ThrowWeapItemClass grenade)
                    {
                        string gName = grenade.Name.Localized(); // 使用全名
                        if (grenadeCounts.ContainsKey(gName)) grenadeCounts[gName]++;
                        else grenadeCounts[gName] = 1;
                    }
                }
            }

            // 【核心修正】：直接精确锁定战术弹挂和口袋槽位！绝对不会扫到背包深处的雷
            ScanContainerForGrenades(equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem);
            ScanContainerForGrenades(equipment.GetSlot(EquipmentSlot.Pockets).ContainedItem);

            foreach (var kvp in grenadeCounts)
            {
                _throwablesCache.Add(new ThrowableData { Name = kvp.Key, Count = kvp.Value });
            }
        }

        public static float Draw(float startY, float globalScale)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return startY;

            UpdateThrowables(PluginsCore.CorrectPlayer);
            if (_throwablesCache.Count == 0) return startY;

            float finalScale = globalScale * Scale.Value;
            float currentY = startY + OffsetY.Value * finalScale;
            float lh = 18f * finalScale;

            // 将我们配置的枚举转换为 Unity GUI 的 TextAnchor
            TextAnchor anchor = TextAnchor.MiddleCenter;
            if (Alignment.Value == TextAlign.Left) anchor = TextAnchor.MiddleLeft;
            else if (Alignment.Value == TextAlign.Right) anchor = TextAnchor.MiddleRight;

            GUIStyle nadeStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = (int)(13 * finalScale), alignment = anchor };
            UnityEngine.Color mainColor = Color.Value;

            float screenW = Screen.width;

            // 为了防止左右对齐时贴住屏幕边缘，我们预留一点内边距（如果是居中则无所谓）
            float padding = 20f * finalScale;
            Rect drawRect = new Rect(padding, currentY, screenW - (padding * 2), lh);

            HUDManager.DrawShadowLabel(drawRect, "--- ORDNANCE ---", new UnityEngine.Color(0.5f, 0.5f, 0.5f, 0.5f), nadeStyle);
            currentY += lh;
            drawRect.y = currentY;

            foreach (var nade in _throwablesCache)
            {
                string line = $"{nade.Name}  <color=#ffffff>x{nade.Count}</color>";
                HUDManager.DrawShadowLabel(drawRect, line, mainColor, nadeStyle);

                currentY += lh;
                drawRect.y = currentY;
            }

            return currentY + (5f * finalScale);
        }
    }
}