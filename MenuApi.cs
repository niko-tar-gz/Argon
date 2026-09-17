using System;
using System.Collections.Generic;

namespace Argon
{
    /// <summary>public entry point for extensions to add pages</summary>
    public static class MenuApi
    {
        private static readonly List<MenuItem> rootItems = new List<MenuItem>();

        public static void RegisterRootItem(string label, Func<MenuPage> openPage)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A menu label is required.", nameof(label));
            if (openPage == null) throw new ArgumentNullException(nameof(openPage));
            rootItems.RemoveAll(item => item.Label == label);
            rootItems.Add(new MenuItem(label, openPage));
        }

        public static void UnregisterRootItem(string label) => rootItems.RemoveAll(item => item.Label == label);
        internal static IReadOnlyList<MenuItem> RootItems => rootItems;
        internal static void Reset() => rootItems.Clear();
    }

    public class MenuPage
    {
        public MenuPage(string title, IEnumerable<MenuItem> items)
        {
            Title = title ?? string.Empty;
            Items = new List<MenuItem>(items ?? Array.Empty<MenuItem>());
        }

        public string Title { get; }
        public IReadOnlyList<MenuItem> Items { get; }
    }

    public class MenuItem
    {
        public MenuItem(string label, Action action) : this(label, action, false, false) { }
        public MenuItem(string label, Action action, bool returnsToPrevious) : this(label, action, returnsToPrevious, false) { }
        public MenuItem(string label, Action action, bool returnsToPrevious, bool disabled) : this(label, action, returnsToPrevious, disabled, null) { }
        public MenuItem(string label, Func<MenuPage> openPage) : this(label, null, false, false, openPage) { }
        private MenuItem(string label, Action action, bool returnsToPrevious, bool disabled, Func<MenuPage> openPage)
        {
            Label = label ?? string.Empty;
            Action = action;
            OpenPage = openPage;
            ReturnsToPrevious = returnsToPrevious;
            Disabled = disabled;
        }

        public string Label { get; }
        public Action Action { get; }
        public Func<MenuPage> OpenPage { get; private set; }
        /// <summary>when the action runs, go back to the previous page (confirmations).</summary>
        public bool ReturnsToPrevious { get; }
        /// <summary>item cannot be activated (e.g. options already used).</summary>
        public bool Disabled { get; }
    }
}
