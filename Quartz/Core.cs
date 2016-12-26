using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ColossalFramework.IO;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using UnityEngine;

namespace Quartz
{

    public class Core : MonoBehaviour
    {

        private static bool _bootstrapped;
        private static Skin.ModuleClass _currentModuleClass;

        public static void Bootstrap(Skin.ModuleClass moduleClass)
        {
			if (!MyInfo().isEnabled)
			{
				Deinitialize();
				return;
			}
			Debug.LogWarning("Trying to bootstrap moduleClass: " + Enum.GetName(typeof(Skin.ModuleClass), moduleClass) + " status of bootstrap is: " + _bootstrapped);
			if (_bootstrapped && moduleClass != _currentModuleClass)
            {
				Debug.LogWarning("De-strapping moduleClass: " + Enum.GetName(typeof(Skin.ModuleClass), moduleClass) + " because bootstrap was " + _bootstrapped);
				Deinitialize();
            }
			else if (_bootstrapped && moduleClass == _currentModuleClass)
			{
				Debug.Log("Already bootstrapped for this class, skipping");
				return;
			}

            ErrorLogger.ResetSettings();

            _currentModuleClass = moduleClass;
			Debug.LogWarning("Success bootstrapping moduleClass: " + Enum.GetName(typeof(Skin.ModuleClass), moduleClass));

            FindObjectOfType<UIView>().gameObject.AddComponent<Core>();
            _bootstrapped = true;
        }

        public static void Deinitialize()
        {
            var core = FindObjectOfType<UIView>().GetComponent<Core>();
            if (core != null)
            {
                Destroy(core);
            }
        }

        public static string OverrideDirectory = Path.Combine(DataLocation.localApplicationData, Path.Combine("Quartz", "Override"));

        private List<SkinMetadata> _availableSkins;
        private Skin _currentSkin;

		public static PluginManager.PluginInfo MyInfo()
		{
			var quartzPlugin = PluginManager.instance.GetPluginsInfo().FirstOrDefault(p => (p.publishedFileID.AsUInt64 == 576970398 || (p.modPath.Contains(@"AppData\Local\") && p.name == "Quartz")));
			return quartzPlugin;
		}

        private DebugRenderer _debugRenderer;

        private bool _autoReloadSkinOnChange;
        private float _autoReloadCheckTimer = 1.0f;

        private UIButton _quartzButton;
        private UIPanel _quartzPanel;

        void OnDestroy()
        {
            try
            {
                if (_currentSkin != null)
                {
                    _currentSkin.Rollback();
                }

                _currentSkin = null;

                if (_quartzPanel != null)
                {
					Debug.Log("Unload Quartz Panel");
                    Destroy(_quartzPanel);
                }

                if (_quartzButton != null)
                {
					Debug.Log("Unload Quartz Button");
					Destroy(_quartzButton);
                }

                SetCameraRectHelper.Deinitialize();

                _bootstrapped = false;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private bool _needToApplyCurrentSkin;

        void Start()
        {
			Debug.Log("Start ran!");
			if (!MyInfo().isEnabled)
			{
				Deinitialize();
				return;
			}
            SetCameraRectHelper.Initialize();

            if (!Directory.Exists(OverrideDirectory))
            {
                try
                {
                    Directory.CreateDirectory(OverrideDirectory);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            InitializeInGamePanels();

            _availableSkins = SkinLoader.FindAllSkins();

			if (!string.IsNullOrEmpty(ConfigManager.SelectedSkinPath) && ConfigManager.ApplySkinOnStartup)
            {
                foreach (var metadata in _availableSkins)
                {
					if (metadata.Path == ConfigManager.SelectedSkinPath)
                    {
                        _currentSkin = Skin.FromXmlFile(Path.Combine(metadata.Path, "skin.xml"), false);
                        _needToApplyCurrentSkin = true;
                        break;
                    }
                }
            }
            
            CreateUI();

            _debugRenderer = gameObject.AddComponent<DebugRenderer>();
        }

        void InitializeInGamePanels()
        {
            var roadsPanels = FindObjectsOfType<RoadsPanel>();

            foreach (var property in typeof(RoadsPanel).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (!property.CanRead)
                {
                    continue;
                }

                foreach (var roadsPanel in roadsPanels)
                {
                    try
                    {
                        property.GetValue(roadsPanel, null);
                    }
                    catch (Exception)
                    {
                    }
                }    
            }
        }

        void Update()
        {
            if (_needToApplyCurrentSkin)
            {
                if (_currentSkin.IsValid)
                {
                    _currentSkin.Apply(_currentModuleClass);
                }
                else
                {
                    Debug.LogWarning("Skin is invalid, will not apply.. (check messages above for errors)");
                }

                _needToApplyCurrentSkin = false;
            }

            if (Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKeyDown(KeyCode.A))
                {
                    if (_quartzPanel != null)
                    {
                        _quartzPanel.isVisible = !_quartzPanel.isVisible;
                    }
                }
                else if (Input.GetKeyDown(KeyCode.D))
                {
                    if (_debugRenderer != null)
                    {
                        _debugRenderer.drawDebugInfo = !_debugRenderer.drawDebugInfo;
                    }
                }
                else if (Input.GetKeyDown(KeyCode.S) && Input.GetKey(KeyCode.LeftShift))
                {
                    ReloadAndApplyActiveSkin();
                }
                else if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.J))
                {
                    var path = "vanilla_ui_dump.xml";
                    if (_currentSkin != null)
                    {
                        path = _currentSkin.Name + "_dump.xml";
                    }

                    SceneUtil.DumpSceneToXML(path);
                    Debug.LogWarningFormat("Dumped scene to \"{0}\"", path);   
                }
            }

            if (_currentSkin != null)
            {
                _currentSkin.ApplyStickyProperties(_currentModuleClass);
            }

            if (_currentSkin != null && _autoReloadSkinOnChange)
            {
                _autoReloadCheckTimer -= Time.deltaTime;
                if (_autoReloadCheckTimer <= 0.0f)
                {
                    _autoReloadCheckTimer = 1.0f;
                }

                _currentSkin.ReloadIfChanged();
            }
        }

        private void CreateUI()
        {
            _quartzPanel = CreateQuartzPanel();
			_quartzButton = UIUtil.CreateQuartzButton(_currentModuleClass);

			if (_currentModuleClass == Skin.ModuleClass.InGame && !ConfigManager.ShowQuartzIconInGame)
            {
                _quartzButton.isVisible = false;
            }

            _quartzButton.eventClick += (component, param) => { _quartzPanel.isVisible = !_quartzPanel.isVisible; };

            if (_currentModuleClass != Skin.ModuleClass.MainMenu)
            {
                try
                {
                    Camera.main.pixelRect = new Rect(0, 0, Screen.width, Screen.height);
                    GameObject.Find("Underground View").GetComponent<Camera>().pixelRect = new Rect(0, 0, Screen.width, Screen.height);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private UIDropDown _skinsDropdown;

		private UIPanel CreateQuartzPanel()
        {
            var uiView = GameObject.Find("UIView").GetComponent<UIView>();
            if (uiView == null)
            {
                Debug.LogError("UIView is null!");
                return null;
            }

            var panel = uiView.AddUIComponent(typeof(UIPanel)) as UIPanel;

	        if (panel != null)
	        {
		        panel.size = new Vector2(300, 290);
		        panel.isVisible = false;
		        panel.atlas = EmbeddedResources.GetQuartzAtlas();
		        panel.backgroundSprite = "DefaultPanelBackground";

				Vector2 viewSize = uiView.GetScreenResolution();

                panel.relativePosition = _currentModuleClass == Skin.ModuleClass.MainMenu
                    ? new Vector3(viewSize.x - 2.0f - panel.size.x, 34.0f)
                    : new Vector3(viewSize.x - 2.0f - panel.size.x, 34.0f + 64.0f + 15.0f);

                panel.name = "QuartzSkinManager";

		        var title = panel.AddUIComponent<UILabel>();
				title.relativePosition = new Vector3(2.0f, 2.0f);
				title.text = "Quartz Skin Manager";
				title.textColor = Color.black;
	        }

            float y = 32.0f;

			UIUtil.MakeCheckbox(panel, "ShowIconInGame", "Show Quartz icon in-game", new Vector2(4.0f, y), ConfigManager.ShowQuartzIconInGame, value =>
            {
				ConfigManager.ShowQuartzIconInGame = value;

                if (_quartzButton != null && !ConfigManager.ShowQuartzIconInGame && _currentModuleClass == Skin.ModuleClass.InGame)
                {
                    _quartzButton.isVisible = false;
                    if (_quartzPanel != null)
                    {
                        _quartzPanel.isVisible = false;
                    }
                }
                else if(_quartzButton != null)
                {
                    _quartzButton.isVisible = true;
                }
            });

            y += 28.0f;

			UIUtil.MakeCheckbox(panel, "AutoApplySkin", "Apply skin on start-up", new Vector2(4.0f, y), ConfigManager.ApplySkinOnStartup, value =>
            {
				ConfigManager.ApplySkinOnStartup = value;
            });

            y += 28.0f;

            UIUtil.MakeCheckbox(panel, "DrawDebugInfo", "Developer mode (Ctrl+D)", new Vector2(4.0f, y), false, value =>
            {
                if (_debugRenderer != null)
                {
                    _debugRenderer.drawDebugInfo = value;
                }
            });

            y += 28.0f;

            UIUtil.MakeCheckbox(panel, "AutoReload", "Auto-reload active skin on file change", new Vector2(4.0f, y), false, value =>
            {
                _autoReloadSkinOnChange = value;
                ReloadAndApplyActiveSkin();
            });

			y += 28.0f;

			UIUtil.MakeCheckbox(panel, "IgnoreMissing", "Force load (May break stuff)", new Vector2(4.0f, y), ConfigManager.IgnoreMissingComponents, value =>
			{
				ConfigManager.IgnoreMissingComponents = value;
			});

            y += 28.0f;

            _skinsDropdown = panel.AddUIComponent<UIDropDown>();

            _skinsDropdown.AddItem("Vanilla (by Colossal Order)");
            foreach (var skin in _availableSkins)
            {
                _skinsDropdown.AddItem(String.Format("{0} (by {1}){2}", skin.Name, skin.Author, skin.Legacy ? " [LEGACY]" : string.Empty));
            }

            _skinsDropdown.size = new Vector2(296.0f, 32.0f);
            _skinsDropdown.relativePosition = new Vector3(2.0f, y);
            _skinsDropdown.listBackground = "GenericPanelLight";
            _skinsDropdown.itemHeight = 32;
            _skinsDropdown.itemHover = "ListItemHover";
            _skinsDropdown.itemHighlight = "ListItemHighlight";
            _skinsDropdown.normalBgSprite = "ButtonMenu";
            _skinsDropdown.listWidth = 300;
            _skinsDropdown.listHeight = 500;
            _skinsDropdown.foregroundSpriteMode = UIForegroundSpriteMode.Stretch;
            _skinsDropdown.popupColor = new Color32(45, 52, 61, 255);
            _skinsDropdown.popupTextColor = new Color32(170, 170, 170, 255);
            _skinsDropdown.zOrder = 1;
            _skinsDropdown.textScale = 0.8f;
            _skinsDropdown.verticalAlignment = UIVerticalAlignment.Middle;
            _skinsDropdown.horizontalAlignment = UIHorizontalAlignment.Center;
            _skinsDropdown.textFieldPadding = new RectOffset(8, 0, 8, 0);
            _skinsDropdown.itemPadding = new RectOffset(8, 0, 2, 0);

            _skinsDropdown.selectedIndex = 0;

            if(_currentSkin != null)
            {
                int i = 1;
                foreach (var skin in _availableSkins)
                {
                    if (skin.Path == _currentSkin.SapphirePath)
                    {
                        _skinsDropdown.selectedIndex = i;
                    }

                    i++;
                }
            }

            _skinsDropdown.eventSelectedIndexChanged += (component, index) =>
            {
                if (index == 0)
                {
                    if (_currentSkin != null)
                    {
                        _currentSkin.Dispose();
                    }

                    _currentSkin = null;
                    return;
                }

                var skin = _availableSkins[index-1];
                if (_currentSkin != null && _currentSkin.SapphirePath == skin.Path)
                {
                    return;
                }

                if (_currentSkin != null)
                {
                    _currentSkin.Dispose();
                }

                _currentSkin = Skin.FromXmlFile(Path.Combine(skin.Path, "skin.xml"), _autoReloadSkinOnChange);

                if (_currentSkin.IsValid)
                {
                    _currentSkin.Apply(_currentModuleClass);
                }
                else
                {
                    Debug.LogWarning("Skin is invalid, will not apply.. (check messages above for errors)");
                }

				ConfigManager.SelectedSkinPath = _currentSkin.SapphirePath;
                panel.isVisible = false;
            };
                        
            var skinsDropdownButton = _skinsDropdown.AddUIComponent<UIButton>();
            _skinsDropdown.triggerButton = skinsDropdownButton;

            skinsDropdownButton.text = "";
            skinsDropdownButton.size = _skinsDropdown.size;
            skinsDropdownButton.relativePosition = new Vector3(0.0f, 0.0f);
            skinsDropdownButton.textVerticalAlignment = UIVerticalAlignment.Middle;
            skinsDropdownButton.textHorizontalAlignment = UIHorizontalAlignment.Center;
            skinsDropdownButton.normalFgSprite = "IconDownArrow";
            skinsDropdownButton.hoveredFgSprite = "IconDownArrowHovered";
            skinsDropdownButton.pressedFgSprite = "IconDownArrowPressed";
            skinsDropdownButton.focusedFgSprite = "IconDownArrowFocused";
            skinsDropdownButton.disabledFgSprite = "IconDownArrowDisabled";
            skinsDropdownButton.foregroundSpriteMode = UIForegroundSpriteMode.Fill;
            skinsDropdownButton.horizontalAlignment = UIHorizontalAlignment.Right;
            skinsDropdownButton.verticalAlignment = UIVerticalAlignment.Middle;
            skinsDropdownButton.zOrder = 0;
            skinsDropdownButton.textScale = 0.8f;

            y += 40.0f;

            UIUtil.MakeButton(panel, "ReloadSkin", "Reload active skin (Ctrl+Shift+S)", new Vector2(4.0f, y), ReloadAndApplyActiveSkin);

            y += 36.0f;

            UIUtil.MakeButton(panel, "RefreshSkins", "Refresh available skins", new Vector2(4.0f, y), RefreshSkinsList);

            return panel;
        }

        void RefreshSkinsList()
        {
            if (_skinsDropdown != null)
            {
                _availableSkins = SkinLoader.FindAllSkins();
                _skinsDropdown.localizedItems = new string[0];

                _skinsDropdown.AddItem("Vanilla (by Colossal Order)");
                foreach (var skin in _availableSkins)
                {
                    _skinsDropdown.AddItem(String.Format("{0} (by {1})", skin.Name, skin.Author));
                }

                _skinsDropdown.selectedIndex = 0;
                _skinsDropdown.Invalidate();
            }
        }

        private void ReloadAndApplyActiveSkin()
        {
            try
            {
                if (_currentSkin == null)
                {
                    return;
                }

                _currentSkin.SafeReload(_autoReloadSkinOnChange);

                if (_currentSkin.IsValid)
                { 
                    _currentSkin.Apply(_currentModuleClass);
                }
                else
                {
                    Debug.LogWarning("Skin is invalid, will not apply.. (check messages above for errors)");
                } 
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Failed to load skin: {0}", ex.Message);
            }
        }

    }

}
