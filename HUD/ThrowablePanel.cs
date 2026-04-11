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
            OffsetY = config.Bind("Top Panel / 投掷装备", "投掷板 Y轴偏移", 12f, new ConfigDescription("面板的纵向微调"));
            Scale = config.Bind("Top Panel / 投掷装备", "投掷板 缩放比例", 1.25f, new ConfigDescription("面板整体缩放"));
            Active = config.Bind("Top Panel / 投掷装备", "显示投掷物", true, new ConfigDescription("是否启用可用投掷物显示"));
            Alignment = config.Bind("Top Panel / 投掷装备", "内部对齐方式", TextAlign.Center, new ConfigDescription("面板文字的对齐方式"));
            Color = config.Bind("Top Panel / 投掷装备", "颜色设置", new Color(0.9f, 0.7f, 0.2f, 0.85f), new ConfigDescription("警告/投掷物颜色"));
        }

        private static void UpdateThrowables(Player player)
        {
            if (Time.time - _lastScanTime < 1f) return;
            _lastScanTime = Time.time;

            _throwablesCache.Clear();
            var equipment = player.Inventory?.Equipment;
            if (equipment == null) return;

            Dictionary<string, int> grenadeCounts = new Dictionary<string, int>();

            void ScanContainerForGrenades(Item containerItem)
            {
                if (containerItem == null) return;
                foreach (var item in containerItem.GetAllItems())
                {
                    if (item is ThrowWeapItemClass grenade)
                    {
                        string gName = grenade.Name.Localized();
                        if (grenadeCounts.ContainsKey(gName)) grenadeCounts[gName]++;
                        else grenadeCounts[gName] = 1;
                    }
                }
            }

            ScanContainerForGrenades(equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem);
            ScanContainerForGrenades(equipment.GetSlot(EquipmentSlot.Pockets).ContainedItem);

            foreach (var kvp in grenadeCounts)
            {
                _throwablesCache.Add(new ThrowableData { Name = kvp.Key, Count = kvp.Value });
            }
        }

        // 接收武器面板的左侧锚点 X，以及是否持枪的状态
        public static float Draw(float anchorRightX, float startY, float globalScale, bool hasWeapon)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return startY;

            UpdateThrowables(PluginsCore.CorrectPlayer);
            if (_throwablesCache.Count == 0) return startY;

            float finalScale = globalScale * Scale.Value;
            float currentY = startY + OffsetY.Value * finalScale;
            float lh = 18f * finalScale;

            TextAnchor anchor;
            Rect drawRect;

            // 【动态包围盒逻辑】：根据是否有武器，改变面板大小和渲染位置
            if (hasWeapon)
            {
                // 持枪时：面板躲到左边，贴紧武器板，默认向右对齐
                float panelWidth = 250f * finalScale;
                float spacing = 25f * finalScale; // 距离武器板留点呼吸空间

                anchor = Alignment.Value == TextAlign.Left ? TextAnchor.MiddleLeft :
                         (Alignment.Value == TextAlign.Center ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight);

                // 起点在 锚点X - 宽度 - 间距
                drawRect = new Rect(anchorRightX - panelWidth - spacing, currentY, panelWidth, lh);
            }
            else
            {
                // 没枪时：嚣张地占满全屏居中显示
                anchor = Alignment.Value == TextAlign.Left ? TextAnchor.MiddleLeft :
                         (Alignment.Value == TextAlign.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter);

                float padding = 20f * finalScale;
                drawRect = new Rect(padding, currentY, Screen.width - (padding * 2), lh);
            }

            GUIStyle nadeStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = (int)(13 * finalScale), alignment = anchor };
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

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