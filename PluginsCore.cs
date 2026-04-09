using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace EFTBallisticCalculator
{
    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    public class PluginsCore : BaseUnityPlugin
    {
        #region BepInEx 快捷键配置
        public static ConfigEntry<KeyboardShortcut> KeyFcsToggle;
        public static ConfigEntry<KeyboardShortcut> KeyFcsClear;
        public static ConfigEntry<KeyboardShortcut> KeyDistUp100;
        public static ConfigEntry<KeyboardShortcut> KeyDistDown100;
        public static ConfigEntry<KeyboardShortcut> KeyDistUp10;
        public static ConfigEntry<KeyboardShortcut> KeyDistDown10;
        public static ConfigEntry<KeyboardShortcut> KeyDistUp1;
        public static ConfigEntry<KeyboardShortcut> KeyDistDown1;
        #endregion

        #region 全局状态缓存
        public static bool _isFcsActive = false;
        public static bool _isHudActive = true;
        public static bool _hasAmmo = false;

        public static float _currentSpeed = 0f;
        public static float _currentMass = 0f;
        public static float _currentBC = 0f;
        public static float _currentDiam = 0f;

        public static float _lockedHorizontalDist;
        public static float _lockedTOF;
        #endregion

        #region UI 排版参数
        public static float _hudOffsetX = 30f;
        public static float _hudStartYOffset = -180f;
        public static float _hudScale = 1.0f;
        public static float _panelSpacing = 15f;
        #endregion

        #region 跨脚本对象与环境
        public static float _weatherSeedGlobal;
        public static System.Random _weatherRng;
        public class WeatherSeed { public float b; public float x; public float y; public float r; }
        public class WeatherSeedMap
        {
            public WeatherSeed windSpeed = new WeatherSeed();
            public WeatherSeed windDirection = new WeatherSeed();
            public WeatherSeed humidity = new WeatherSeed();
            public WeatherSeed temperature = new WeatherSeed();
        }
        public static WeatherSeedMap _weatherSeedMap = new WeatherSeedMap();
        public static string _cachedLocationName = null;
        public static int _layerMask = -1;
        public static Player CorrectPlayer { get; set; }
        public static GameWorld CorrectGameWorld { get; set; }

        // 暴露给 UI 的公有属性
        public static GameObject ImpactMarker => Instance._impactMarker;
        private EFT.Player.FirearmController _currentFC;
        private static Camera _cachedOpticCamera;
        private GameObject _impactMarker;
        private static PluginsCore Instance;
        #endregion

        public void Awake()
        {
            Instance = this; // 保存单例供内部访问

            KeyFcsToggle = Config.Bind("1. Controls", "Toggle HUD", new KeyboardShortcut(KeyCode.KeypadDivide), "开启/关闭火控面板");
            KeyFcsClear = Config.Bind("1. Controls", "Clear Target (Unlock)", new KeyboardShortcut(KeyCode.Backspace), "脱锁并清除距离数据");

            KeyDistUp100 = Config.Bind("2. Manual Dial", "Distance +100m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftShift), "手动增加距离 100m");
            KeyDistDown100 = Config.Bind("2. Manual Dial", "Distance -100m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftShift), "手动减少距离 100m");

            KeyDistUp10 = Config.Bind("2. Manual Dial", "Distance +10m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftAlt), "手动增加距离 10m");
            KeyDistDown10 = Config.Bind("2. Manual Dial", "Distance -10m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftAlt), "手动减少距离 10m");

            KeyDistUp1 = Config.Bind("2. Manual Dial", "Distance +1m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftControl), "手动增加距离 1m");
            KeyDistDown1 = Config.Bind("2. Manual Dial", "Distance -1m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftControl), "手动减少距离 1m");

            var harmony = new Harmony(PluginsInfo.GUID);
            harmony.PatchAll();
        }

        public void Update()
        {
            if (CorrectPlayer == null || Camera.main == null) return;
            if (_layerMask == -1) _layerMask = LayerMask.GetMask("Terrain", "HighPolyCollider", "Default");

            _currentFC = CorrectPlayer.HandsController as EFT.Player.FirearmController;

            if (_currentFC != null && _currentFC.Item != null && _currentFC.Item.Chambers.Length > 0 && _currentFC.Item.Chambers[0].ContainedItem is AmmoItemClass currentAmmo)
            {
                _hasAmmo = true;
                _currentSpeed = currentAmmo.AmmoTemplate.InitialSpeed * _currentFC.Item.SpeedFactor;
                _currentMass = currentAmmo.AmmoTemplate.BulletMassGram;
                _currentBC = currentAmmo.AmmoTemplate.BallisticCoeficient;
                _currentDiam = currentAmmo.AmmoTemplate.BulletDiameterMilimeters;
            }
            else
            {
                _hasAmmo = false;
            }

            bool hasLockedDistance = _lockedHorizontalDist > 0f;
            bool isFcsLocked = (_currentFC != null) && _hasAmmo && hasLockedDistance;

            UpdateOpticCache();

            // 监听输入
            if (KeyFcsToggle.Value.IsDown()) _isHudActive = !_isHudActive;

            if (Input.GetKeyDown(KeyCode.T))
            {
                ExecuteFcsLogic();
            }

            if (_currentFC != null)
            {
                if (KeyFcsClear.Value.IsDown())
                {
                    _lockedHorizontalDist = 0f;
                }

                float deltaDist = 0f;
                if (KeyDistUp100.Value.IsDown()) deltaDist += 100f;
                if (KeyDistDown100.Value.IsDown()) deltaDist -= 100f;
                if (KeyDistUp10.Value.IsDown()) deltaDist += 10f;
                if (KeyDistDown10.Value.IsDown()) deltaDist -= 10f;
                if (KeyDistUp1.Value.IsDown()) deltaDist += 1f;
                if (KeyDistDown1.Value.IsDown()) deltaDist -= 1f;

                if (deltaDist != 0f)
                {
                    _lockedHorizontalDist += deltaDist;
                    _lockedHorizontalDist = (int)_lockedHorizontalDist;

                    if (_lockedHorizontalDist <= 0f)
                    {
                        _lockedHorizontalDist = 0f;
                    }
                }
            }

            // 3D 渲染
            if (isFcsLocked)
            {
                bool isAiming = false;
                var pwa = CorrectPlayer.ProceduralWeaponAnimation;
                if (pwa != null) isAiming = pwa.IsAiming;

                if (isAiming)
                {
                    if (_impactMarker == null) InitializeMarker();

                    Vector3 currentFireportPos = _currentFC.CurrentFireport.position;
                    Vector3 currentBoreDir = _currentFC.WeaponDirection.normalized;
                    Vector3 currentVelocity = currentBoreDir * _currentSpeed;

                    Vector3 impactPoint3D = BallisticsSimulator.SimulateToHorizontalDistance(
                        currentFireportPos, currentVelocity, _currentMass, _currentDiam, _currentBC, _lockedHorizontalDist, out _lockedTOF
                    );

                    _impactMarker.transform.position = impactPoint3D;

                    Camera activeCam = (_cachedOpticCamera != null && _cachedOpticCamera.isActiveAndEnabled) ? _cachedOpticCamera : Camera.main;
                    float distToCamera = Vector3.Distance(activeCam.transform.position, impactPoint3D);
                    float dynamicScale = distToCamera * Mathf.Tan(activeCam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.015f;
                    _impactMarker.transform.localScale = new Vector3(dynamicScale, dynamicScale, dynamicScale);

                    if (!_impactMarker.activeSelf) _impactMarker.SetActive(true);
                }
                else
                {
                    if (_impactMarker != null && _impactMarker.activeSelf) _impactMarker.SetActive(false);
                }
            }
            else
            {
                if (_impactMarker != null && _impactMarker.activeSelf) _impactMarker.SetActive(false);
            }
        }

        private void ExecuteFcsLogic()
        {
            if (_currentFC == null) return;
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _layerMask))
            {
                Vector3 fireportPos = _currentFC.CurrentFireport.position;
                _lockedHorizontalDist = new Vector2(hit.point.x - fireportPos.x, hit.point.z - fireportPos.z).magnitude;
            }
            else
            {

                _lockedHorizontalDist = 0f;
            }
        }

        public static Transform GetAzimuth()
        {
            return (_cachedOpticCamera != null && _cachedOpticCamera.isActiveAndEnabled) ? _cachedOpticCamera.transform : Camera.main.transform;
        }

        // --- 委托给 UI 管理器 ---
        public void OnGUI()
        {
            HUDManager.DrawGUI();
        }

        private void UpdateOpticCache()
        {
            var pwa = CorrectPlayer.ProceduralWeaponAnimation;
            if (pwa != null && pwa.IsAiming)
            {
                if (_cachedOpticCamera == null || !_cachedOpticCamera.isActiveAndEnabled)
                {
                    foreach (Camera cam in Camera.allCameras)
                    {
                        if (cam.isActiveAndEnabled && cam != Camera.main && cam.targetTexture != null)
                        {
                            _cachedOpticCamera = cam;
                            break;
                        }
                    }
                }
            }
            else { _cachedOpticCamera = null; }
        }

        private void InitializeMarker()
        {
            _impactMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_impactMarker.GetComponent<Collider>());
            Material mat = new Material(Shader.Find("GUI/Text Shader"));
            mat.color = Color.yellow;
            MeshRenderer renderer = _impactMarker.GetComponent<MeshRenderer>();
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}