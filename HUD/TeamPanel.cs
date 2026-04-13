using BepInEx.Configuration;
using EFT;
using EFTBallisticCalculator.Locale;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class TeamPanel
    {
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> RectWidth;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;

        // 花名册数据结构：用于持久化保存队友信息，即使 Player 实体被销毁也不丢失
        // 花名册数据结构
        private class TeammateRecord
        {
            public string AccountId;
            public string Name;
            public Player PlayerRef;
            public bool IsDead;
        }
        private static bool _isDebugMode = true; // 测试完毕后改成 false 即可
        private static int _debugTargetCount = 4; // 抓几个倒霉蛋当队友？
        private static HashSet<string> _debugFakeTeammates = new HashSet<string>();
        private static readonly Dictionary<string, TeammateRecord> _roster = new Dictionary<string, TeammateRecord>();
        private static float _lastScanTime = 0f;

        public static void InitCfg(ConfigFile config)
        {
            OffsetX = config.Bind("Team Panel / 队伍数据", "X轴偏移", -50f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_x_desc"),
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_x_name"), IsAdvanced = true }));

            OffsetY = config.Bind("Team Panel / 队伍数据", "Y轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_y_name"), IsAdvanced = true }));

            Scale = config.Bind("Team Panel / 队伍数据", "缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_scale_name"), IsAdvanced = true }));

            Active = config.Bind("Team Panel / 队伍数据", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_active_name") }));

            Color = config.Bind("Team Panel / 队伍数据", "颜色设置", new Color(0.8f, 0.9f, 1f, 0.85f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_color_name") }));

            RectWidth = config.Bind("Team Panel / 队伍数据", "面板宽度", 340f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_rect_desc"),
                new AcceptableValueRange<float>(0f, 800f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_rect_name"), IsAdvanced = true }));
        }

        // 维护队伍花名册 (每 2 秒扫描一次以节省性能)
        private static void UpdateRoster()
        {
            if (Time.time - _lastScanTime < 2f) return;
            _lastScanTime = Time.time;

            var gw = PluginsCore.CorrectGameWorld;
            string myGroupId = PluginsCore.CorrectGroupId;

            if (gw == null || string.IsNullOrEmpty(myGroupId)) return;

            HashSet<string> aliveProfileIds = new HashSet<string>();

            foreach (var player in gw.AllAlivePlayersList)
            {
                if (player == null || player == PluginsCore.CorrectPlayer) continue;

                bool isTeammate = false;

                // 核心劫持逻辑
                if (_isDebugMode)
                {
                    string profileId = player.Profile?.Id;
                    if (!string.IsNullOrEmpty(profileId))
                    {
                        if (_debugFakeTeammates.Contains(profileId)) isTeammate = true;
                        else if (_debugFakeTeammates.Count < _debugTargetCount)
                        {
                            _debugFakeTeammates.Add(profileId);
                            isTeammate = true;
                        }
                    }
                }
                else
                {
                    string targetGroupId = player.Profile?.Info?.GroupId ?? "";
                    isTeammate = !string.IsNullOrEmpty(myGroupId) && targetGroupId == myGroupId;
                }

                if (isTeammate && player.Profile != null)
                {
                    string profileId = player.Profile.Id;
                    aliveProfileIds.Add(profileId);

                    if (!_roster.TryGetValue(profileId, out var record))
                    {
                        // 【引入西里尔字母转换】：获取拼音化的干净名字
                        string safeName = GetLatinName(player.Profile.Info.Nickname);

                        record = new TeammateRecord
                        {
                            AccountId = profileId,
                            Name = _isDebugMode ? $"[TEST] {safeName}" : safeName
                        };
                        _roster[profileId] = record;
                    }

                    record.PlayerRef = player;
                    record.IsDead = player.ActiveHealthController == null || !player.ActiveHealthController.IsAlive;
                }
            }

            foreach (var kvp in _roster)
            {
                if (!aliveProfileIds.Contains(kvp.Key))
                {
                    kvp.Value.IsDead = true;
                }
            }
        }

        // 注意：这里的 anchorRightX 接收的是 HealthPanel 传出的最左侧边界
        // 参数改为接收起点的 startX 和 startY
        public static float Draw(float startX, float startY, float globalScale)
        {
            // 如果没开启，或者没有队伍，直接原样返回 startY，高度无变化
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return startY;

            UpdateRoster();

            if (string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && _roster.Count == 0) return startY;

            float finalScale = globalScale * Scale.Value;

            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = RectWidth.Value * finalScale;

            // 动态向左靠拢，直接从 startX 加上自身的独立偏移
            float finalX = startX + OffsetX.Value;
            float finalY = startY + OffsetY.Value;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize };
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

            float currentY = finalY;

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), LocaleManager.Get("team_title"), mainColor, titleStyle);
            currentY += lh;

            // 1. 绘制自己 (You)
            DrawPlayerLine(PluginsCore.CorrectPlayer.Profile?.Info?.Nickname ?? "YOU", PluginsCore.CorrectPlayer, true, finalX, ref currentY, rectWidth, lh, textStyle, mainColor);

            // 2. 绘制花名册里的所有队友 (Teammates)
            foreach (var record in _roster.Values)
            {
                DrawPlayerLine(record.Name, record.IsDead ? null : record.PlayerRef, false, finalX, ref currentY, rectWidth, lh, textStyle, mainColor);
            }

            // 返回最终的底部 Y 坐标
            return currentY; 
        }

        private static void DrawPlayerLine(string name, Player player, bool isSelf, float x, ref float y, float width, float lh, GUIStyle style, Color defaultColor)
        {
            string prefix = isSelf ? LocaleManager.Get("team_you") : LocaleManager.Get("team_ally");

            // 如果 player 引用丢失或已被标记为死亡
            if (player == null || player.ActiveHealthController == null || !player.ActiveHealthController.IsAlive)
            {
                string deadLine = string.Format(LocaleManager.Get("team_teammate_dead"),
                    LocaleManager.Get("team_teammate_color_dead"),
                    prefix,
                    name,
                    LocaleManager.Get("team_teammate_dead_tag"));//$"<b>{prefix} {name}</b> - <color=#ff4444><b>[DEAD/LOST]</b></color>";
                HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), deadLine, defaultColor, style);
                y += lh;
                return;
            }

            var healthCtrl = player.ActiveHealthController;
            var physCtrl = player.Physical;

            // 存活状态：计算总血量
            float totalHp = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Current;
            float maxHp = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Maximum;

            var level = player?.Profile?.Info?.Level ?? 0;

            // 【网络同步兜底】：如果其他玩家没有这些数据，显示 ?? 而不是报错
            float hydr = healthCtrl != null ? healthCtrl.Hydration.Current : 0f;
            float hydrmax = healthCtrl != null ? healthCtrl.Hydration.Maximum : 100f;
            float energy = healthCtrl != null ? healthCtrl.Energy.Current : 0f;
            float energymax = healthCtrl != null ? healthCtrl.Energy.Maximum : 100f;
            float weight = physCtrl?.IobserverToPlayerBridge_0 != null ? physCtrl.IobserverToPlayerBridge_0.TotalWeight : 0f;
            float overWeight = physCtrl?.BaseOverweightLimits.x ?? 1f;
            float maxWeight = physCtrl?.BaseOverweightLimits.y ?? 2f;
            float weightLimit = weight >= overWeight ? maxWeight : overWeight;
            string weightColor = weight < overWeight ? LocaleManager.Get("health_endur_normal_weight_color") : weight >= overWeight ? LocaleManager.Get("health_endur_over_weight_color") : LocaleManager.Get("health_endur_critical_weight_color");
            // 状态颜色判定
            //string hpColor = (maxHp > 0 && (totalHp / maxHp) < 0.3f) ? "#ffaa00" : "#ffffff";

            var side = GetPlayerSide(player);

            // 第一行：[ALLY] Name (350/440) Alive
            string mainLine = string.Format(LocaleManager.Get("team_teammate_alive"),
                LocaleManager.Get($"team_teammate_color_{side}"),
                prefix,
                name,
                side !=0 ? string.Format(LocaleManager.Get("team_teammate_level"), level) : "",
                LocaleManager.Get($"health_bio_hp_color_{HealthPanel.HealthStatusLow(totalHp, maxHp)}"),
                totalHp,
                maxHp,
                LocaleManager.Get("team_teammate_alive_tag"));//$"<b>{prefix} {name}</b> - <color={hpColor}>({totalHp:F0}/{maxHp:F0})</color> <color=#55ff55>[ALIVE]</color>";
            HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), mainLine, defaultColor, style);
            y += lh;

            //过滤自己
            if (!player.IsYourPlayer)
            {
                // 第二行：缩进显示吃喝与负重
                string subLine = string.Format(LocaleManager.Get("team_teammate_status"),
                    weightColor,
                    weight,
                    LocaleManager.Get($"health_bio_hydr_color_{HealthPanel.HealthStatusLow(hydr, hydrmax)}"),
                    hydr,
                    LocaleManager.Get($"health_bio_energy_color_{HealthPanel.HealthStatusLow(energy, energymax)}"),
                    energy);//$"  └ W: {weight}kg | H: {hydr} | E: {energy}";
                HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), subLine, defaultColor, style);
                y += lh;
            }

            y += 4f; // 队员间隙
        }
        private static int GetPlayerSide(Player player)
        {
            if (player == null) return 0;
            switch (player?.Profile?.Info?.Side.ToString() ?? "")
            {
                case "Savage": return 0;
                case "Bear": return 1;
                case "Usec": return 2;
            }
            return 0;
        }
        private static string GetLatinName(string nickname)
        {
            if (string.IsNullOrEmpty(nickname)) return "UNKNOWN";

            // 如果全是英文字符/数字，直接返回，省性能
            if (IsAllEnglish(nickname)) return nickname;

            try
            {
                // 调用原版底层方法翻译俄语名字 (常用于狗牌)
                return GStruct21.ConvertToLatinic(nickname);
            }
            catch
            {
                // 兜底：如果某些特定版本找不到 GStruct21，直接返回原名防止报错
                return nickname;
            }
        }

        private static bool IsAllEnglish(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                // 允许大写 A-Z，小写 a-z，数字 0-9，以及空格、连字符、下划线
                if ((c < 'A' || c > 'Z') &&
                    (c < 'a' || c > 'z') &&
                    (c < '0' || c > '9') &&
                    c != ' ' && c != '-' && c != '_')
                {
                    return false;
                }
            }
            return true;
        }
    }
}