using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using GorillaNetworking;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Argon
{
    internal enum MenuNavigation { RightStick, LeftStick, Grips, GripsInverse }

    internal sealed class ArgonMenu : MonoBehaviour
    {
        private const float InputThreshold = 0.7f;
        private const string BackItemLabel = "Back";

        private sealed class StackEntry
        {
            public MenuPage Page;
            public Func<MenuPage> Factory;
            public bool HasBackItem;
            public int Selection;
        }

        private readonly List<StackEntry> pages = new List<StackEntry>();
        private bool hasEverOpened;
        private ConfigEntry<MenuNavigation> navigation;
        private GameObject menuObject;
        private TextMeshProUGUI menuText;
        private bool previousOpen, previousPrimary, previousUp, previousDown;

        internal void Initialize(ConfigFile config)
        {
            navigation = config.Bind("Menu", "Menu Nav", MenuNavigation.RightStick, "Navigation input.");
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            ControllerInputPoller poller = ControllerInputPoller.instance;
            if (poller == null) return;
            try
            {
                bool openPressed = poller.rightControllerSecondaryButton;
                if (openPressed && !previousOpen) Toggle();
                previousOpen = openPressed;
                if (menuObject == null || !menuObject.activeSelf) return;

                FollowHead();
                bool primary = poller.rightControllerPrimaryButton;
                GetNavigation(poller, out bool up, out bool down);
                if (up && !previousUp) Move(-1);
                if (down && !previousDown) Move(1);
                if (primary && !previousPrimary) Enter();
                previousPrimary = primary;
                previousUp = up;
                previousDown = down;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        private void Toggle()
        {
            EnsureMenu();
            menuObject.SetActive(!menuObject.activeSelf);
            if (!menuObject.activeSelf) return;
            previousPrimary = true;
            previousUp = previousDown = false;
            if (!hasEverOpened)
            {
                hasEverOpened = true;
                pages.Add(new StackEntry { Page = CreateRootPage(), Factory = CreateRootPage, Selection = 0 });
            }
            else
            {
                RefreshCurrent();
            }
            FollowHead();
            Render();
        }

        private void Back()
        {
            if (pages.Count == 1) { menuObject.SetActive(false); return; }
            pages.RemoveAt(pages.Count - 1);
            RefreshCurrent();
        }

        private void Enter()
        {
            StackEntry current = Current;
            if (current.Selection >= current.Page.Items.Count) return;
            MenuItem item = current.Page.Items[current.Selection];
            if (item.Disabled) return;
            if (item.OpenPage != null)
            {
                try
                {
                    OpenSubPage(item.OpenPage);
                }
                catch (Exception error)
                {
                    Debug.LogException(error, this);
                }
                return;
            }
            if (item.Action != null) item.Action.Invoke();
            if (item.ReturnsToPrevious) Back();
            else RefreshCurrent();
        }

        private void OpenSubPage(Func<MenuPage> openPage)
        {
            pages.Add(new StackEntry { Page = WithBackItem(openPage()), Factory = openPage, HasBackItem = true, Selection = 1 });
            Render();
        }

        private void RefreshCurrent()
        {
            StackEntry current = Current;
            if (current.Factory != null) current.Page = current.HasBackItem ? WithBackItem(current.Factory()) : current.Factory();
            current.Selection = Mathf.Clamp(current.Selection, 0, current.Page.Items.Count - 1);
            if (current.Selection < 0) current.Selection = 0;
            Render();
        }

        private StackEntry Current => pages[pages.Count - 1];

        private MenuPage CreateRootPage()
        {
            List<MenuItem> items = new List<MenuItem>(MenuApi.RootItems)
            {
                new MenuItem("Settings", CreateSettingsPage)
            };
            return new MenuPage("Argon v" + PluginInfo.Version, items);
        }

        private MenuPage CreateSettingsPage() => new MenuPage("Settings", new[]
        {
            new MenuItem("Menu Nav: " + navigation.Value, CycleNavigation)
        });

        private void CycleNavigation()
        {
            navigation.Value = (MenuNavigation)(((int)navigation.Value + 1) % Enum.GetValues(typeof(MenuNavigation)).Length);
            RefreshCurrent();
        }

        private void Move(int direction)
        {
            StackEntry current = Current;
            int count = current.Page.Items.Count;
            if (count == 0) return;
            current.Selection = (current.Selection + direction + count) % count;
            Render();
        }

        private void GetNavigation(ControllerInputPoller poller, out bool up, out bool down)
        {
            if (navigation.Value == MenuNavigation.Grips || navigation.Value == MenuNavigation.GripsInverse)
            {
                bool inverse = navigation.Value == MenuNavigation.GripsInverse;
                up = inverse ? poller.rightControllerGripFloat > InputThreshold : poller.leftControllerGripFloat > InputThreshold;
                down = inverse ? poller.leftControllerGripFloat > InputThreshold : poller.rightControllerGripFloat > InputThreshold;
                return;
            }
            Vector2 stick = navigation.Value == MenuNavigation.RightStick ? poller.rightControllerPrimary2DAxis : poller.leftControllerPrimary2DAxis;
            up = stick.y > InputThreshold;
            down = stick.y < -InputThreshold;
        }

        private void EnsureMenu()
        {
            if (menuObject != null) return;
            menuObject = new GameObject("Argon Menu");
            DontDestroyOnLoad(menuObject);

            Canvas canvas = menuObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = menuObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3000;
            menuObject.AddComponent<GraphicRaycaster>();

            menuText = new GameObject("Argon Menu Text").AddComponent<TextMeshProUGUI>();
            menuText.transform.SetParent(menuObject.transform, false);
            RectTransform rect = (RectTransform)menuText.transform;
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = new Vector2(0.5f, 0.4f);
            rect.anchoredPosition = Vector2.zero;
            menuText.alignment = TextAlignmentOptions.Center;
            menuText.fontSize = 0.03f;
            menuText.color = Color.white;
            menuText.textWrappingMode = TextWrappingModes.NoWrap;
            menuText.font = LocalisationManager.GetFontAssetForCurrentLocale(out LocalisationFontPair fontPair)
                ? fontPair.fontAsset : TMP_Settings.defaultFontAsset;
            DoXrayShit(menuText);
        }

        private static void DoXrayShit(TextMeshProUGUI text)
        {
            Material source = text.fontSharedMaterial;
            if (source == null) return;
            if (source.HasProperty("_ZTest"))
            {
                Material clone = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
                clone.SetInt("_ZTest", (int)CompareFunction.Always);
                text.fontSharedMaterial = clone;
                return;
            }
            Shader shader = Shader.Find("GUI/Text Shader");
            if (shader == null) return;
            text.fontSharedMaterial = new Material(shader) { mainTexture = source.mainTexture, hideFlags = HideFlags.HideAndDontSave };
        }

        private void FollowHead()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            Transform head = camera.transform;
            menuObject.transform.SetParent(head, false);
            menuObject.transform.localPosition = new Vector3(0f, 0f, 1.25f);
            menuObject.transform.localRotation = Quaternion.identity;
            menuObject.transform.localScale = Vector3.one * 0.75f;
        }

        private void Render()
        {
            MenuPage page = Current.Page;
            StringBuilder text = new StringBuilder(page.Title).Append("\n\n");
            for (int i = 0; i < page.Items.Count; i++)
            {
                if (i == Current.Selection) text.Append("> ");
                text.Append(page.Items[i].Label);
                if (i == Current.Selection) text.Append(" <");
                text.Append('\n');
            }
            menuText.text = text.ToString();
        }

        private static MenuPage WithBackItem(MenuPage page)
        {
            List<MenuItem> items = new List<MenuItem> { new MenuItem(BackItemLabel, (Action)null, true) };
            items.AddRange(page.Items);
            return new MenuPage(page.Title, items);
        }
    }
}
