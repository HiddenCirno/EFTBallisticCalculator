using BepInEx;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
using EFTBallisticCalculator.Core;
using EFTBallisticCalculator.HUD;

namespace EFTBallisticCalculator
{
    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    public class PluginsCore : BaseUnityPlugin
    {
        #region 全局状态缓存
        public static bool _hasAmmo = false;

        public static float _currentSpeed = 0f;
        public static float _currentMass = 0f;
        public static float _currentBC = 0f;
        public static float _currentDiam = 0f;

        public static float _lockedHorizontalDist;
        public static float _lockedTOF;
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

        public static GameObject ImpactMarker => Instance._impactMarker;
        private EFT.Player.FirearmController _currentFC;
        private static Camera _cachedOpticCamera;
        private GameObject _impactMarker;
        private static PluginsCore Instance;
        #endregion

        public void Awake()
        {
            Instance = this;

            // 1. 下发配置给两大管家
            HotKeyManager.Init(Config);
            HUDManager.Init(Config);

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

            // 2. 集中处理按键状态
            if (HotKeyManager.KeyEnvPannel.Value.IsDown()) EnvPanel.Active.Value = !EnvPanel.Active.Value;
            if (HotKeyManager.KeyFcsPannel.Value.IsDown()) FCSPanel.Active.Value = !FCSPanel.Active.Value;

            if (Input.GetKeyDown(KeyCode.T))
            {
                ExecuteFcsLogic();
            }

            if (_currentFC != null)
            {
                if (HotKeyManager.KeyFcsClear.Value.IsDown())
                {
                    _lockedHorizontalDist = 0f;
                }

                float deltaDist = HotKeyManager.GetManualDistanceDelta();

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

            // 3. 渲染物理预测球
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