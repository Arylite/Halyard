using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace Halyard.Controls;

/// <summary>
/// Small attached motion behaviours, all purely visual: a hover scope (optionally lifting), children
/// that fade in or zoom while their scope is hovered, and items of an <see cref="ItemsRepeater"/>
/// that fade up in a short stagger when they first appear. Everything stays still when Windows'
/// "Animation effects" setting is off.
/// </summary>
public static class Motion
{
    private const int MaxStaggerSteps = 12;
    private const float LiftOffset = -3;
    private const float ZoomScale = 1.04f;
    private const float EntranceScale = 0.97f;

    private static readonly TimeSpan HoverDuration = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan ZoomDuration = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan StaggerStep = TimeSpan.FromMilliseconds(24);

    /// <summary>Items prepared this soon after a load or a collection change animate; later ones (scrolling) don't.</summary>
    private static readonly TimeSpan EntranceWindow = TimeSpan.FromMilliseconds(400);

    private static readonly UISettings SystemSettings = new();

    private static bool AnimationsEnabled
    {
        get
        {
            try
            {
                return SystemSettings.AnimationsEnabled;
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
            {
                return true;
            }
        }
    }

    // --- Hover scope -------------------------------------------------------------------------

    public static readonly DependencyProperty HoverScopeProperty = DependencyProperty.RegisterAttached(
        "HoverScope", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnHoverScopeChanged));

    public static bool GetHoverScope(UIElement element) => (bool)element.GetValue(HoverScopeProperty);

    public static void SetHoverScope(UIElement element, bool value) => element.SetValue(HoverScopeProperty, value);

    /// <summary>A hover scope that also rises a few pixels while hovered.</summary>
    public static readonly DependencyProperty HoverLiftProperty = DependencyProperty.RegisterAttached(
        "HoverLift", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnHoverScopeChanged));

    public static bool GetHoverLift(UIElement element) => (bool)element.GetValue(HoverLiftProperty);

    public static void SetHoverLift(UIElement element, bool value) => element.SetValue(HoverLiftProperty, value);

    /// <summary>Hover state of a scope, shared with the children that react to it.</summary>
    private static readonly DependencyProperty ScopeStateProperty = DependencyProperty.RegisterAttached(
        "ScopeState", typeof(HoverState), typeof(Motion), new PropertyMetadata(null));

    private static void OnHoverScopeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not UIElement scope || args.NewValue is not true || scope.GetValue(ScopeStateProperty) is HoverState)
        {
            return;
        }

        var state = new HoverState();
        scope.SetValue(ScopeStateProperty, state);

        void Set(bool hovered)
        {
            if (state.IsHovered == hovered)
            {
                return;
            }

            state.IsHovered = hovered;
            if (GetHoverLift(scope))
            {
                scope.TranslationTransition ??= new Vector3Transition { Duration = HoverDuration };
                scope.Translation = hovered && AnimationsEnabled ? new Vector3(0, LiftOffset, 0) : Vector3.Zero;
            }

            state.Changed?.Invoke(hovered);
        }

        scope.AddHandler(UIElement.PointerEnteredEvent, new PointerEventHandler((_, _) => Set(true)), handledEventsToo: true);
        scope.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler((_, _) => Set(false)), handledEventsToo: true);
        scope.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler((_, _) => Set(false)), handledEventsToo: true);
        scope.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, _) => Set(false)), handledEventsToo: true);
    }

    // --- Children reacting to their scope ----------------------------------------------------

    /// <summary>Invisible until the enclosing hover scope is hovered (decorations such as a brighter border).</summary>
    public static readonly DependencyProperty RevealOnHoverProperty = DependencyProperty.RegisterAttached(
        "RevealOnHover", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnScopeChildChanged));

    public static bool GetRevealOnHover(FrameworkElement element) => (bool)element.GetValue(RevealOnHoverProperty);

    public static void SetRevealOnHover(FrameworkElement element, bool value) => element.SetValue(RevealOnHoverProperty, value);

    /// <summary>Zooms in slightly while the enclosing hover scope is hovered (cover art).</summary>
    public static readonly DependencyProperty ZoomOnHoverProperty = DependencyProperty.RegisterAttached(
        "ZoomOnHover", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnScopeChildChanged));

    public static bool GetZoomOnHover(FrameworkElement element) => (bool)element.GetValue(ZoomOnHoverProperty);

    public static void SetZoomOnHover(FrameworkElement element, bool value) => element.SetValue(ZoomOnHoverProperty, value);

    private static readonly DependencyProperty IsWiredProperty = DependencyProperty.RegisterAttached(
        "IsWired", typeof(bool), typeof(Motion), new PropertyMetadata(false));

    private static void OnScopeChildChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement element || args.NewValue is not true)
        {
            return;
        }

        if (GetRevealOnHover(element))
        {
            element.Opacity = 0;
            element.OpacityTransition ??= new ScalarTransition { Duration = HoverDuration };
        }

        if (GetZoomOnHover(element))
        {
            element.ScaleTransition ??= new Vector3Transition { Duration = ZoomDuration };
            element.SizeChanged += (_, _) =>
            {
                Center(element);

                // The zoomed element must stay inside its container instead of spilling over its neighbours.
                if (VisualTreeHelper.GetParent(element) is FrameworkElement container)
                {
                    container.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight) };
                }
            };
        }

        // The scope is an ancestor, only reachable once the element is in the tree.
        element.Loaded += (_, _) => Wire(element);
    }

    private static void Wire(FrameworkElement element)
    {
        if ((bool)element.GetValue(IsWiredProperty) || FindScope(element) is not { } state)
        {
            return;
        }

        element.SetValue(IsWiredProperty, true);
        state.Changed += hovered =>
        {
            if (GetRevealOnHover(element))
            {
                element.Opacity = hovered ? 1 : 0;
            }

            if (GetZoomOnHover(element))
            {
                element.Scale = hovered && AnimationsEnabled ? new Vector3(ZoomScale, ZoomScale, 1) : Vector3.One;
            }
        };
    }

    private static HoverState? FindScope(DependencyObject element)
    {
        for (var current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current.GetValue(ScopeStateProperty) is HoverState state)
            {
                return state;
            }
        }

        return null;
    }

    private static void Center(UIElement element) =>
        element.CenterPoint = new Vector3(element.ActualSize.X / 2, element.ActualSize.Y / 2, 0);

    private sealed class HoverState
    {
        public bool IsHovered;

        public Action<bool>? Changed;
    }

    // --- Staggered entrance ------------------------------------------------------------------

    public static readonly DependencyProperty StaggerEntranceProperty = DependencyProperty.RegisterAttached(
        "StaggerEntrance", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnStaggerEntranceChanged));

    public static bool GetStaggerEntrance(ItemsRepeater repeater) => (bool)repeater.GetValue(StaggerEntranceProperty);

    public static void SetStaggerEntrance(ItemsRepeater repeater, bool value) => repeater.SetValue(StaggerEntranceProperty, value);

    private static void OnStaggerEntranceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not ItemsRepeater repeater || args.NewValue is not true)
        {
            return;
        }

        var stagger = new StaggerState();
        ItemsSourceView? watched = null;

        void Open()
        {
            stagger.WindowEnd = Environment.TickCount64 + (long)EntranceWindow.TotalMilliseconds;
            stagger.Count = 0;
        }

        void Watch()
        {
            if (watched is not null)
            {
                watched.CollectionChanged -= OnCollectionChanged;
            }

            watched = repeater.ItemsSourceView;
            if (watched is not null)
            {
                watched.CollectionChanged += OnCollectionChanged;
            }
        }

        void OnCollectionChanged(object? _, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Only a fresh burst restarts the stagger; items arriving mid-burst keep counting.
            if (Environment.TickCount64 > stagger.WindowEnd)
            {
                Open();
            }
        }

        repeater.Loaded += (_, _) =>
        {
            Open();
            Watch();
        };
        repeater.Unloaded += (_, _) =>
        {
            if (watched is not null)
            {
                watched.CollectionChanged -= OnCollectionChanged;
                watched = null;
            }
        };
        repeater.RegisterPropertyChangedCallback(ItemsRepeater.ItemsSourceProperty, (_, _) => Watch());
        repeater.ElementPrepared += (_, e) => Enter(e.Element, stagger);
    }

    private static void Enter(UIElement element, StaggerState stagger)
    {
        // Recycled elements may still be mid-entrance: settle them instantly first.
        element.OpacityTransition = null;
        element.ScaleTransition = null;

        if (!AnimationsEnabled || Environment.TickCount64 > stagger.WindowEnd || element.DispatcherQueue is not { } queue)
        {
            element.Opacity = 1;
            element.Scale = Vector3.One;
            return;
        }

        var step = Math.Min(stagger.Count++, MaxStaggerSteps);
        element.Opacity = 0;
        element.Scale = new Vector3(EntranceScale, EntranceScale, 1);
        if (element is FrameworkElement framework)
        {
            Center(framework);
        }

        // The hidden state must be on screen before the transition starts, so reveal on a later tick.
        var timer = queue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(16) + (StaggerStep * step);
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (element is FrameworkElement sized)
            {
                Center(sized);
            }

            element.OpacityTransition = new ScalarTransition { Duration = EntranceDuration };
            element.ScaleTransition = new Vector3Transition { Duration = EntranceDuration };
            element.Opacity = 1;
            element.Scale = Vector3.One;
        };
        timer.Start();
    }

    private sealed class StaggerState
    {
        public long WindowEnd;

        public int Count;
    }
}
