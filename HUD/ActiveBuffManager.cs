// ActiveBuffManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    /// <summary>
    /// 负责捕获、更新、合并所有玩家身上的 Buff/Debuff。
    /// 核心逻辑移植自 MedEffectsHUD，支持事件订阅 + 深度扫描 + 容器时间读取。
    /// 修改：只保留一个 AllEffects 列表，不分正负面。
    /// </summary>
    public static class ActiveBuffManager
    {
        // ------------------------------------------------------------
        // 对外输出数据
        // ------------------------------------------------------------
        public class DisplayEffect
        {
            public string Name;           // 显示名称
            public float TimeLeft;        // 剩余秒数，<=0 表示永久或未知
            public float Strength;        // 数值（如 +30 力量）
            public string EffectId;       // 用于图标匹配的稳定标识符
        }

        // 只保留一个列表，包含所有效果
        public static IReadOnlyList<DisplayEffect> AllEffects => _allEffects;

        // ------------------------------------------------------------
        // 私有状态（移植自 MedEffectsHUD）
        // ------------------------------------------------------------
        private static Player _localPlayer;
        private static IHealthController _healthController;
        private static Type _ipbType;                     // IPlayerBuff 接口类型
        private static bool _ipbSearched;
        private static bool _eventsSubscribed;
        private static bool _deepScanDone;

        private static readonly Dictionary<string, object> _capturedBuffs = new Dictionary<string, object>(StringComparer.Ordinal);
        private static readonly Dictionary<int, object> _buffToContainer = new Dictionary<int, object>();
        private static readonly HashSet<int> _containerIds = new HashSet<int>();
        private static readonly List<object> _containers = new List<object>();
        private static readonly Dictionary<int, float> _buffWholeTimeOffset = new Dictionary<int, float>();

        private static readonly List<DisplayEffect> _allEffects = new List<DisplayEffect>();

        private static float _lastUpdateTime;
        private const float UPDATE_INTERVAL = 0.5f;
        private static int _tick;

        // ------------------------------------------------------------
        // 初始化与更新
        // ------------------------------------------------------------
        public static void Update()
        {
            if (Time.time - _lastUpdateTime < UPDATE_INTERVAL) return;
            _lastUpdateTime = Time.time;

            RefreshPlayerRef();
            if (_healthController != null)
                RefreshEffects();
        }

        private static void RefreshPlayerRef()
        {
            try
            {
                if (_localPlayer != null && _localPlayer.HealthController != null) return;
                if (!Singleton<GameWorld>.Instantiated) { Reset(); return; }
                var gw = Singleton<GameWorld>.Instance;
                if (gw == null) { Reset(); return; }
                _localPlayer = gw.allAlivePlayersByID?.Values.FirstOrDefault(p => p.IsYourPlayer);
                _healthController = _localPlayer?.HealthController;

                if (_healthController != null)
                {
                    _eventsSubscribed = false;
                    _deepScanDone = false;
                    _capturedBuffs.Clear();
                    _buffToContainer.Clear();
                    _containerIds.Clear();
                    _containers.Clear();
                    _buffWholeTimeOffset.Clear();
                }
            }
            catch { Reset(); }
        }

        private static void Reset()
        {
            _localPlayer = null;
            _healthController = null;
            _eventsSubscribed = false;
            _deepScanDone = false;
            _allEffects.Clear();
            _capturedBuffs.Clear();
            _buffToContainer.Clear();
            _containerIds.Clear();
            _containers.Clear();
            _buffWholeTimeOffset.Clear();
        }

        private static void RefreshEffects()
        {
            _allEffects.Clear();

            if (_healthController == null) return;
            _tick++;

            if (!_eventsSubscribed) SubscribeEvents();
            if (!_deepScanDone) DeepScanHealthController();

            // 定期重新扫描容器映射（每 1.5 秒）
            if (_tick % 3 == 0)
                QuickRescan();

            // 处理所有捕获到的 IPlayerBuff 对象
            var deadKeys = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in _capturedBuffs)
            {
                try
                {
                    var buff = kv.Value;
                    if (buff == null) { deadKeys.Add(kv.Key); continue; }

                    bool active = GetBoolProp(buff, "Active", true);
                    if (!active) { deadKeys.Add(kv.Key); continue; }

                    string buffName = GetStringProp(buff, "BuffName");
                    if (string.IsNullOrEmpty(buffName)) continue;

                    float value = GetFloatProp(buff, "Value");
                    float timeLeft = SanitizeTimer(GetBuffTimeLeft(buff));

                    string cleanName = StripColorTags(buffName);
                    string displayName = StripValueFromName(cleanName);

                    // 不再区分正负面，统一添加到 AllEffects
                    string uniqueKey = $"{displayName}|{value:F2}";
                    var de = new DisplayEffect
                    {
                        Name = displayName,
                        TimeLeft = timeLeft,
                        Strength = value,
                        EffectId = GetBuffEffectId(buff)
                    };

                    AddDedup(_allEffects, seen, de, uniqueKey);
                }
                catch { }
            }

            foreach (var dk in deadKeys) _capturedBuffs.Remove(dk);

            // 排序：按剩余时间升序（时间短的在上）
            if (_allEffects.Count > 1)
                _allEffects.Sort((a, b) => a.TimeLeft.CompareTo(b.TimeLeft));
        }

        // ------------------------------------------------------------
        // 核心时间获取（取多个来源的最大值）
        // ------------------------------------------------------------
        private static float GetBuffTimeLeft(object buff)
        {
            float best = -1f;
            float duration = GetSettingsDuration(buff);
            float elapsed = GetFloatProp(buff, "WholeTime");
            int bid = buff.GetHashCode();

            if (duration > 0 && elapsed >= 0)
            {
                float offset = _buffWholeTimeOffset.TryGetValue(bid, out float off) ? off : 0f;
                float remaining = duration - (elapsed - offset);
                if (remaining > 0 && remaining < 100000f) best = remaining;
            }

            if (_buffToContainer.TryGetValue(bid, out object container))
            {
                float containerTL = GetFloatProp(container, "TimeLeft");
                if (containerTL > 0 && containerTL < 100000f && containerTL > best) best = containerTL;
            }

            float selfTL = GetFloatProp(buff, "TimeLeft");
            if (selfTL > 0 && selfTL < 100000f && selfTL > best) best = selfTL;

            return best;
        }

        private static float GetSettingsDuration(object buff)
        {
            try
            {
                var settingsProp = buff.GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (settingsProp == null) return -1f;
                var settings = settingsProp.GetValue(buff);
                if (settings == null) return -1f;
                return GetFloatProp(settings, "Duration");
            }
            catch { return -1f; }
        }

        // ------------------------------------------------------------
        // 事件订阅（反射注入 Action<IPlayerBuff>）
        // ------------------------------------------------------------
        private static void SubscribeEvents()
        {
            if (_eventsSubscribed) return;
            _eventsSubscribed = true;
            ResolveIPB();
            if (_ipbType == null) return;

            try
            {
                object hc = _healthController;
                var actionType = typeof(Action<>).MakeGenericType(_ipbType);
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                var t = hc.GetType();
                while (t != null && t != typeof(object))
                {
                    foreach (var f in t.GetFields(flags | BindingFlags.DeclaredOnly))
                    {
                        if (f.FieldType != actionType) continue;
                        bool isRemove = f.Name.Contains("1") || f.Name.ToLower().Contains("remove");
                        var handler = BuildActionDelegate(isRemove);
                        if (handler == null) continue;

                        var existing = (Delegate)f.GetValue(hc);
                        var combined = Delegate.Combine(existing, handler);
                        f.SetValue(hc, combined);
                    }
                    t = t.BaseType;
                }
            }
            catch { }
        }

        private static Delegate BuildActionDelegate(bool isRemove)
        {
            try
            {
                var actionType = typeof(Action<>).MakeGenericType(_ipbType);
                var wrapper = new BuffEventWrapper(isRemove);
                var mi = typeof(BuffEventWrapper).GetMethod("Handle");
                return Delegate.CreateDelegate(actionType, wrapper, mi);
            }
            catch { return null; }
        }

        private class BuffEventWrapper
        {
            private readonly bool _isRemove;
            public BuffEventWrapper(bool isRemove) => _isRemove = isRemove;
            public void Handle(object buff)
            {
                if (_isRemove) OnBuffRemoved(buff);
                else OnBuffAdded(buff);
            }
        }

        private static void OnBuffAdded(object buff)
        {
            try
            {
                string key = GetBuffKey(buff);
                if (string.IsNullOrEmpty(key)) return;

                string buffName = GetStringProp(buff, "BuffName");
                string bodyPart = GetStringProp(buff, "BodyPart");
                var staleKeys = _capturedBuffs.Keys.Where(k => k.StartsWith($"{buffName}|{bodyPart}|")).ToList();
                foreach (var sk in staleKeys) _capturedBuffs.Remove(sk);

                _capturedBuffs[key] = buff;
                int bid = buff.GetHashCode();
                float wholeTime = GetFloatProp(buff, "WholeTime");
                _buffWholeTimeOffset[bid] = wholeTime;
            }
            catch { }
        }

        private static void OnBuffRemoved(object buff)
        {
            try
            {
                string key = GetBuffKey(buff);
                if (!string.IsNullOrEmpty(key) && _capturedBuffs.ContainsKey(key))
                {
                    if (!GetBoolProp(buff, "Active", false))
                        _capturedBuffs.Remove(key);
                }
            }
            catch { }
        }

        private static string GetBuffKey(object buff)
        {
            try
            {
                string name = GetStringProp(buff, "BuffName");
                if (string.IsNullOrEmpty(name)) return null;
                string bodyPart = GetStringProp(buff, "BodyPart");
                float value = GetFloatProp(buff, "Value");
                int hash = buff.GetHashCode();
                return $"{name}|{bodyPart}|{(value >= 0 ? "+" : "-")}|{hash}";
            }
            catch { return null; }
        }

        // ------------------------------------------------------------
        // 深度扫描（首次 + 定期快扫）
        // ------------------------------------------------------------
        private static void DeepScanHealthController()
        {
            if (_deepScanDone) return;
            _deepScanDone = true;
            ResolveIPB();
            var visited = new HashSet<int>();
            DeepScan(_healthController, 0, 5, visited, "HC");
            ScanEffectsForBuffs(visited);
        }

        private static void QuickRescan()
        {
            try
            {
                var visited = new HashSet<int>();
                var hcType = _healthController.GetType();
                var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

                var t = hcType;
                while (t != null && t != typeof(object))
                {
                    var list0 = t.GetField("List_0", flags);
                    if (list0 != null && list0.GetValue(_healthController) is IList l0)
                        foreach (var c in l0) if (c != null) DeepScan(c, 0, 3, visited, "QS.L0");

                    var dict1 = t.GetField("Dictionary_1", flags);
                    if (dict1 != null && dict1.GetValue(_healthController) is IDictionary d1)
                        foreach (DictionaryEntry de in d1) if (de.Value != null) DeepScan(de.Value, 0, 3, visited, "QS.D1");

                    t = t.BaseType;
                }

                RefreshContainerMappings();
            }
            catch { }
        }

        private static void RefreshContainerMappings()
        {
            foreach (var container in _containers)
            {
                if (container == null) continue;
                var buffsField = container.GetType().GetField("Buffs", BindingFlags.Public | BindingFlags.Instance);
                if (buffsField?.GetValue(container) is IList buffsList)
                {
                    foreach (var buff in buffsList)
                        if (buff != null)
                            _buffToContainer[buff.GetHashCode()] = container;
                }
            }
        }

        private static int DeepScan(object obj, int depth, int maxDepth, HashSet<int> visited, string path)
        {
            if (obj == null || depth > maxDepth) return 0;
            int id = obj.GetHashCode();
            if (visited.Contains(id)) return 0;
            visited.Add(id);

            var type = obj.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return 0;
            if (typeof(Delegate).IsAssignableFrom(type)) return 0;
            if (type.Namespace?.StartsWith("UnityEngine") == true) return 0;

            int found = 0;
            if (IsIPB(obj))
            {
                found += CaptureBuffFromScan(obj, path);
                return found;
            }

            found += TryReadBuffContainer(obj, path);

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var f in type.GetFields(flags))
            {
                if (f.IsStatic) continue;
                if (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)) continue;
                var val = f.GetValue(obj);
                if (val == null) continue;

                if (val is IList list)
                {
                    for (int i = 0; i < Math.Min(list.Count, 300); i++)
                        found += DeepScan(list[i], depth + 1, maxDepth, visited, $"{path}.{f.Name}[{i}]");
                }
                else if (val is IDictionary dict)
                {
                    foreach (DictionaryEntry de in dict)
                        found += DeepScan(de.Value, depth + 1, maxDepth, visited, $"{path}.{f.Name}[D]");
                }
                else
                {
                    found += DeepScan(val, depth + 1, maxDepth, visited, $"{path}.{f.Name}");
                }
            }
            return found;
        }

        private static int TryReadBuffContainer(object obj, string path)
        {
            var t = obj.GetType();
            var buffsField = t.GetField("Buffs", BindingFlags.Public | BindingFlags.Instance);
            if (buffsField?.GetValue(obj) is IList buffsList && buffsList.Count > 0)
            {
                int cid = obj.GetHashCode();
                if (_containerIds.Add(cid)) _containers.Add(obj);

                foreach (var buff in buffsList)
                    if (buff != null)
                        _buffToContainer[buff.GetHashCode()] = obj;

                return CaptureBuffFromScan(buffsList[0], path);
            }
            return 0;
        }

        private static int CaptureBuffFromScan(object buff, string path)
        {
            if (!IsIPB(buff)) return 0;
            string key = GetBuffKey(buff);
            if (string.IsNullOrEmpty(key) || _capturedBuffs.ContainsKey(key)) return 0;
            _capturedBuffs[key] = buff;
            return 1;
        }

        private static void ScanEffectsForBuffs(HashSet<int> visited)
        {
            var method = _healthController.GetType().GetMethod("GetAllEffects", Type.EmptyTypes);
            if (method?.Invoke(_healthController, null) is IEnumerable effects)
            {
                foreach (var fx in effects)
                    if (fx != null)
                        DeepScan(fx, 0, 3, visited, "S4");
            }
        }

        // ------------------------------------------------------------
        // IPlayerBuff 识别
        // ------------------------------------------------------------
        private static void ResolveIPB()
        {
            if (_ipbSearched) return;
            _ipbSearched = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.FullName.Contains("Assembly-CSharp")) continue;
                foreach (var t in asm.GetTypes())
                    if (t.Name == "IPlayerBuff" && t.IsInterface)
                    {
                        _ipbType = t;
                        return;
                    }
            }
        }

        private static bool IsIPB(object obj)
        {
            if (obj == null) return false;
            if (_ipbType != null && _ipbType.IsInstanceOfType(obj)) return true;
            var type = obj.GetType();
            return type.GetProperty("BuffName") != null &&
                   type.GetProperty("Value") != null &&
                   type.GetProperty("Active") != null;
        }

        // ------------------------------------------------------------
        // 通用反射辅助
        // ------------------------------------------------------------
        private static float GetFloatProp(object obj, string name)
        {
            try
            {
                var type = obj.GetType();
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.GetValue(obj) is float f) return f;
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.GetValue(obj) is float f2) return f2;
            }
            catch { }
            return 0f;
        }

        private static bool GetBoolProp(object obj, string name, bool def = false)
        {
            try
            {
                var type = obj.GetType();
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.GetValue(obj) is bool b) return b;
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.GetValue(obj) is bool b2) return b2;
            }
            catch { }
            return def;
        }

        private static string GetStringProp(object obj, string name)
        {
            try
            {
                var type = obj.GetType();
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null) return prop.GetValue(obj)?.ToString();
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return field.GetValue(obj)?.ToString();
            }
            catch { }
            return null;
        }

        private static string GetBuffEffectId(object buff)
        {
            string name = GetStringProp(buff, "BuffName");
            return StripColorTags(name)?.Replace(" ", "");
        }

        // ------------------------------------------------------------
        // 字符串处理
        // ------------------------------------------------------------
        private static string StripColorTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = System.Text.RegularExpressions.Regex.Replace(s, @"<color=[^>]*>", "");
            s = s.Replace("</color>", "");
            return s.Trim();
        }

        private static string StripValueFromName(string name)
        {
            int idx = name.LastIndexOf(" (");
            if (idx > 0 && name.EndsWith(")"))
            {
                string inner = name.Substring(idx + 2, name.Length - idx - 3);
                if (float.TryParse(inner, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                    return name.Substring(0, idx);
            }
            return name;
        }

        private static float SanitizeTimer(float val) =>
            float.IsNaN(val) || float.IsInfinity(val) || val < -1f || val > 100000f ? -1f : val;

        private static void AddDedup(List<DisplayEffect> list, HashSet<string> seen, DisplayEffect de, string key)
        {
            if (seen.Contains(key)) return;
            var existing = list.Find(e => e.Name == de.Name);
            if (existing != null)
            {
                // 判断数值是否显著不同
                bool sameStrength = Math.Abs(existing.Strength - de.Strength) < 0.001f
                                 || (existing.Strength == 0f && de.Strength == 0f);

                if (!sameStrength && de.Strength != 0f && existing.Strength != 0f)
                {
                    seen.Add(key);
                    list.Add(de);
                    return;
                }

                // 数值相同或一方为零：合并，保留最长剩余时间
                if (de.TimeLeft > 0 && (existing.TimeLeft <= 0 || de.TimeLeft > existing.TimeLeft))
                    existing.TimeLeft = de.TimeLeft;
                if (de.Strength != 0f)
                    existing.Strength = de.Strength;
                seen.Add(key);
                return;
            }

            seen.Add(key);
            list.Add(de);
        }
    }
}