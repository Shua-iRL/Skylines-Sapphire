using ColossalFramework.UI;
using UnityEngine;

namespace Quartz
{
    public static class UIUtil
    {

        public static UIButton CreateQuartzButton(Skin.ModuleClass moduleClass)
        {
			Debug.Log("Creating button");
            var uiView = GameObject.Find("UIView").GetComponent<UIView>();
            if (uiView == null)
            {
                Debug.LogError("UIView is null!");
                return null;
            }

            var button = uiView.AddUIComponent(typeof(UIButton)) as UIButton;

	        if (button != null)
	        {
				button.name = "QuartzButton";
				button.gameObject.name = "QuartzButton";
		        button.width = 32;
		        button.height = 32;

		        button.pressedBgSprite = "";
		        button.normalBgSprite = "";
		        button.hoveredBgSprite = "";
		        button.disabledBgSprite = "";

		        button.atlas = EmbeddedResources.GetQuartzAtlas();
				button.normalFgSprite = "QuartzIcon";
				button.hoveredFgSprite = "QuartzIconHover";
				button.pressedFgSprite = "QuartzIconPressed";
		        button.foregroundSpriteMode = UIForegroundSpriteMode.Scale;
		        button.scaleFactor = 1.0f;

				button.tooltip = "Quartz Skin Manager";
		        button.tooltipBox = uiView.defaultTooltipBox;

		        Vector2 viewSize = uiView.GetScreenResolution();

		        button.relativePosition = moduleClass == Skin.ModuleClass.MainMenu
                                              ? new Vector3(viewSize.x - 4.0f - button.width, 2.0f, 0.0f)
                                              : new Vector3(viewSize.x - 19.0f - button.width, 34.0f + button.height, 0.0f);
            }
			return button;
        }

        public delegate void ButtonClicked();

        public static UIButton MakeButton(UIPanel panel, string name, string text, Vector2 position, ButtonClicked clicked)
        {
            var button = panel.AddUIComponent<UIButton>();
            button.name = name;
            button.text = text;
            button.relativePosition = position;
            button.size = new Vector2(200.0f, 24.0f);
            button.normalBgSprite = "ButtonMenu";
            button.disabledBgSprite = "ButtonMenuDisabled";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.focusedBgSprite = "ButtonMenu";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textScale = 0.8f;

            button.eventClick += (component, param) => clicked();

            return button;
        }

        public delegate void CheckboxSetValue(bool value);

        public static UICheckBox MakeCheckbox(UIPanel panel, string name, string text, Vector2 position, bool value,
            CheckboxSetValue setValue)
        {
            var label = panel.AddUIComponent<UILabel>();
            label.name = name;
            label.text = text;
            label.relativePosition = position;
            label.textScale = 0.8f;
            label.textColor = Color.black;

            var checkbox = panel.AddUIComponent<UICheckBox>();
            checkbox.AlignTo(label, UIAlignAnchor.TopLeft);
            checkbox.relativePosition = new Vector3(checkbox.relativePosition.x + 274.0f, checkbox.relativePosition.y - 2.0f);
            checkbox.size = new Vector2(16.0f, 16.0f);
            checkbox.isVisible = true;
            checkbox.canFocus = true;
            checkbox.isInteractive = true;

            if (setValue != null)
            {
                checkbox.eventCheckChanged += (component, newValue) => setValue(newValue);
            }

            var uncheckSprite = checkbox.AddUIComponent<UISprite>();
            uncheckSprite.size = new Vector2(16.0f, 16.0f);
            uncheckSprite.relativePosition = new Vector3(0, 0);
            uncheckSprite.spriteName = "check-unchecked";
            uncheckSprite.isVisible = true;

            var checkSprite = checkbox.AddUIComponent<UISprite>();
            checkSprite.size = new Vector2(16.0f, 16.0f);
            checkSprite.relativePosition = new Vector3(0, 0);
            checkSprite.spriteName = "check-checked";

            checkbox.isChecked = value;
            checkbox.checkedBoxObject = checkSprite;
            return checkbox;
        }

    }
}
