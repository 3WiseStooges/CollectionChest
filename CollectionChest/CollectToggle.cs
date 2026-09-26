using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CollectionChest
{
    /// <summary>
    /// The switch in the chest menu: a copy of the menu's own "Take all" button, so it has the
    /// game's look, font and click sound, hung just below the chest panel.
    ///
    /// Below rather than inside, because the panel has no spare room. Its 46px header is already
    /// Take all on the left, the chest name in the middle and Place stacks on the right, and
    /// everything under the header is the item grid. The weight readout already sits outside the
    /// panel's bottom-right corner, so this takes the bottom-left.
    /// </summary>
    internal static class CollectToggle
    {
        private const string ObjectName = "CollectionChest_AutoCollect";

        private const float Width = 190f;
        private const float Gap = 8f;

        private const string OnLabel = "Auto-collect: <color=#9BD86B>ON</color>";
        private const string OffLabel = "Auto-collect: <color=#A0A0A0>OFF</color>";

        private static InventoryGui _builtFor;
        private static GameObject _button;
        private static TMP_Text _label;
        private static Collector _target;
        private static int _shownState = -1;

        /// <summary>Called every frame the inventory screen updates its chest panel.</summary>
        internal static void Refresh(InventoryGui gui)
        {
            Container container = GameAccess.CurrentContainer(gui);
            Collector collector = container != null ? container.GetComponent<Collector>() : null;
            bool show = collector != null && ModConfig.Enabled.Value;

            if (show) EnsureBuilt(gui);
            if (_button == null) return;

            if (_button.activeSelf != show) _button.SetActive(show);
            _target = show ? collector : null;
            if (!show) return;

            // Always the chest's real state, read back from its ZDO - never what was last
            // clicked - so a request the owner did not act on cannot leave the button lying.
            int state = collector.IsCollecting ? 1 : 0;
            if (state == _shownState) return;

            _shownState = state;
            if (_label != null) _label.text = state == 1 ? OnLabel : OffLabel;
        }

        private static void EnsureBuilt(InventoryGui gui)
        {
            // The inventory screen is rebuilt with every world load, and the old button went
            // with it; a destroyed Unity object compares equal to null.
            if (_button != null && ReferenceEquals(_builtFor, gui)) return;

            if (_button != null) Object.Destroy(_button);
            _builtFor = gui;
            _button = null;
            _label = null;
            _shownState = -1;

            Button template = gui.m_takeAllButton;
            RectTransform panel = gui.m_container;
            if (template == null || panel == null) return;

            GameObject button = Object.Instantiate(template.gameObject, panel, false);
            button.name = ObjectName;

            // Hidden until Refresh shows it. Doing it now also makes the copied ButtonSfx drop
            // the click-sound listener it just put on the copied onClick, which is replaced
            // below; OnEnable adds it back to the new one when the button is first shown.
            button.SetActive(false);

            // Take all is bound to a gamepad stick click; a copy would fire on the same press.
            foreach (UIGamePad pad in button.GetComponentsInChildren<UIGamePad>(true))
            {
                if (pad.m_hint != null) Object.DestroyImmediate(pad.m_hint);
                Object.DestroyImmediate(pad);
            }

            // Anything that could re-translate the label back to "Take all".
            foreach (Localize localize in button.GetComponentsInChildren<Localize>(true))
            {
                Object.DestroyImmediate(localize);
            }

            // Clear the copied click handlers before adding ours, in case the prefab ever gains
            // a persistent one that would take everything out of the chest.
            Button clone = button.GetComponent<Button>();
            clone.onClick = new Button.ButtonClickedEvent();
            clone.onClick.AddListener(OnClick);
            clone.interactable = true;

            var rect = (RectTransform)button.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -Gap);
            rect.sizeDelta = new Vector2(Width, rect.sizeDelta.y);

            Transform text = button.transform.Find("Text");
            _label = text != null ? text.GetComponent<TMP_Text>() : button.GetComponentInChildren<TMP_Text>(true);

            _button = button;

            if (ModConfig.VerboseLogging.Value)
            {
                Plugin.Log.LogInfo($"Auto-collect switch added under {panel.name} (label found: {_label != null}).");
            }
        }

        private static void OnClick()
        {
            if (_target == null) return;
            _target.RequestCollecting(!_target.IsCollecting);
        }
    }
}
