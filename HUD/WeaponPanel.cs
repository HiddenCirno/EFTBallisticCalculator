using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic; // 塔科夫物品/库存核心命名空间
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class WeaponPanel
    {
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;

        // 投掷物缓存系统（避免每帧遍历背包产生严重卡顿）
        private class ThrowableData
        {
            public string Name;
            public int Count;
        }
        private static readonly List<ThrowableData> _throwablesCache = new List<ThrowableData>();
        private static float _lastScanTime = 0f;

        public static void InitCfg(ConfigFile config)
        {
            OffsetY = config.Bind("Weapon Panel / 武器火控", "Y轴偏移", 15f, new ConfigDescription("面板距离屏幕顶部的距离"));
            Scale = config.Bind("Weapon Panel / 武器火控", "缩放比例", 1.0f, new ConfigDescription("武器面板整体缩放"));
            Active = config.Bind("Weapon Panel / 武器火控", "显示面板", true, new ConfigDescription("是否启用武器火控显示"));
            Color = config.Bind("Weapon Panel / 武器火控", "颜色设置", new Color(0.6f, 0.9f, 1f, 0.9f), new ConfigDescription("默认战术UI颜色"));
        }

        // 定期扫描口袋和弹挂中的投掷物
        private static void UpdateThrowables(Player player)
        {
            if (Time.time - _lastScanTime < 1f) return; // 每1秒扫描一次库存足够了
            _lastScanTime = Time.time;

            _throwablesCache.Clear();
            if (player.Inventory?.Equipment == null) return;

            Dictionary<string, int> grenadeCounts = new Dictionary<string, int>();

            // 遍历玩家身上的所有物品，寻找投掷物 (ThrowWeapItemClass 是所有手雷的基类)
            var allItems = player.Inventory.Equipment.GetAllItems();
            foreach (var item in allItems)
            {
                if (item is ThrowWeapItemClass grenade)
                {
                    // 过滤掉放在保险箱或背包深处的手雷，只统计能快速按G扔出的（口袋和弹挂）
                    // 塔科夫底层逻辑：如果手雷的顶级父容器是 Pockets 或 TacticalVest
                    var topParent = item.Parent?.Container?.ParentItem;
                    if (topParent != null && (topParent.TemplateId == "557ffd194bdc2d28148b457f" /* Pockets */ || topParent is SearchableItemItemClass /* 弹挂等 */))
                    {
                        string gName = grenade.ShortName.Localized();
                        if (grenadeCounts.ContainsKey(gName)) grenadeCounts[gName]++;
                        else grenadeCounts[gName] = 1;
                    }
                }
            }

            foreach (var kvp in grenadeCounts)
            {
                _throwablesCache.Add(new ThrowableData { Name = kvp.Key, Count = kvp.Value });
            }
        }

        public static void Draw(float globalScale)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return;

            var player = PluginsCore.CorrectPlayer;

            // 如果玩家双手空空，或者手里拿的不是武器（比如拿着手雷、医疗包、指南针），则不显示武器条
            if (player.HandsController == null || !(player.HandsController.Item is Weapon weapon))
            {
                // 即使没拿枪，也更新并显示一下手雷列表
                UpdateThrowables(player);
                DrawThrowables(Screen.width / 2f, OffsetY.Value * globalScale * Scale.Value, globalScale * Scale.Value);
                return;
            }

            UpdateThrowables(player);

            float finalScale = globalScale * Scale.Value;
            int textSize = (int)(14 * finalScale);
            int smallTextSize = (int)(11 * finalScale);
            float currentY = OffsetY.Value * finalScale;

            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize, alignment = TextAnchor.MiddleCenter };
            GUIStyle smallStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = smallTextSize, alignment = TextAnchor.MiddleCenter };
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : Color.Value;

            // ==========================================
            // 核心数据提取 (Weapon Data Extraction)
            // ==========================================
            string weaponName = weapon.ShortName.Localized();
            string fireMode = weapon.SelectedFireMode.ToString().ToUpper(); // FULLAUTO, SINGLE, BURST

            // 耐久度
            float dur = weapon.Repairable?.Durability ?? 100f;
            float maxDur = weapon.Repairable?.MaxDurability ?? 100f;
            string durColor = (dur / maxDur) < 0.5f ? "#ff4444" : "#ffffff";

            // MOA精度 (塔科夫里通常使用 CenterOfImpact)
            float moa = weapon.TotalAccuracy;

            // 弹药逻辑 (Ammon Logic)
            string chamberAmmoName = "EMPTY";
            string nextAmmoName = "EMPTY";
            int currentMagCount = 0;
            int maxMagCount = 0;

            // 1. 抓取膛内子弹
            var chamber = weapon.Chambers?.FirstOrDefault();
            if (chamber?.ContainedItem is AmmoItemClass chamberBullet)
            {
                chamberAmmoName = chamberBullet.ShortName.Localized();
            }

            // 2. 抓取弹匣内子弹
            var mag = weapon.GetCurrentMagazine();
            if (mag != null)
            {
                currentMagCount = mag.Count;
                maxMagCount = mag.MaxCount;

                // 塔科夫弹匣的 Cartridges.Items 是个堆栈，通常 LastOrDefault() 就是最上面的那发子弹
                if (mag.Cartridges?.Items?.LastOrDefault() is AmmoItemClass topBullet)
                {
                    nextAmmoName = topBullet.ShortName.Localized();
                }
            }
            else
            {
                // 如果是没弹匣的枪（比如栓狙、霰弹枪的内置弹仓，甚至是双管喷）
                // 它们的子弹装在武器的特定 Slot 里（通常叫 magazine 也是内置的，用同样逻辑抓取即可）
            }

            // ==========================================
            // UI 渲染 (顶部居中横向排布)
            // ==========================================

            // 组装横向战术字符串，使用 "|" 分隔
            string durStr = $"DUR: <color={durColor}>{dur:F1}</color>";
            string moaStr = $"MOA: {moa:F2}";
            string fireModeStr = $"<color=#ffaa00>[ {fireMode} ]</color>";
            string ammoCountStr = mag != null ? $"AMMO: {currentMagCount}/{maxMagCount}" : "AMMO: --/--";

            string weaponMainLine = $"<b>{weaponName}</b>   |   {durStr}   |   {moaStr}   |   {fireModeStr}   |   {ammoCountStr}";

            // 弹药预测子行
            string chamberColor = chamberAmmoName == "EMPTY" ? "#ff4444" : "#55ff55";
            string ammoPredictLine = $"CHAMBER: <color={chamberColor}>{chamberAmmoName}</color>   >>   NEXT IN MAG: <color=#ffffff>{nextAmmoName}</color>";

            // 计算居中坐标 (由于使用 TextAnchor.MiddleCenter，我们需要给出一个屏幕宽度的 Rect，Unity 会自动在里面居中文字)
            float screenW = Screen.width;
            float blockHeight = 22f * finalScale;

            HUDManager.DrawShadowLabel(new Rect(0, currentY, screenW, blockHeight), weaponMainLine, mainColor, textStyle);
            currentY += blockHeight;

            HUDManager.DrawShadowLabel(new Rect(0, currentY, screenW, blockHeight), ammoPredictLine, new Color(0.8f, 0.8f, 0.8f, 0.8f), smallStyle);
            currentY += blockHeight + (10f * finalScale);

            // ==========================================
            // 绘制下方纵向投掷物列表
            // ==========================================
            DrawThrowables(screenW / 2f, currentY, finalScale);
        }

        private static void DrawThrowables(float centerX, float startY, float finalScale)
        {
            if (_throwablesCache.Count == 0) return;

            int smallTextSize = (int)(12 * finalScale);
            float lh = 18f * finalScale;
            GUIStyle nadeStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = smallTextSize, alignment = TextAnchor.MiddleCenter };
            Color nadeColor = new Color(0.9f, 0.7f, 0.2f, 0.85f); // 橘黄色，符合爆炸物警告感

            float currentY = startY;

            // 画一个小小的分隔符或者标题
            HUDManager.DrawShadowLabel(new Rect(0, currentY, Screen.width, lh), "--- ORDNANCE ---", new Color(0.5f, 0.5f, 0.5f, 0.5f), nadeStyle);
            currentY += lh;

            foreach (var nade in _throwablesCache)
            {
                string line = $"{nade.Name}  <color=#ffffff>x{nade.Count}</color>";
                HUDManager.DrawShadowLabel(new Rect(0, currentY, Screen.width, lh), line, nadeColor, nadeStyle);
                currentY += lh;
            }
        }
    }
}