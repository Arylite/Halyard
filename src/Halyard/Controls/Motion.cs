using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Halyard.Controls;

/// <summary>
/// Attached hover feedback, purely visual and cheap: a hover scope, and children that fade in while
/// their scope is hovered (a lighter cover, a row highlight). No layout or transform animation, so
/// scrolling a long grid stays smooth.
/// </summary>
public static class Motion
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(120);

    public static readonly DependencyProperty HoverScopeProperty = DependencyProperty.RegisterAttached(
        "HoverScope", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnHoverScopeChanged));

    public static bool GetHoverScope(UIElement element) => (bool)element.GetValue(HoverScopeProperty);

    public static void SetHoverScope(UIElement element, bool value) => element.SetValue(HoverScopeProperty, value);

    /// <summary>Invisible until the enclosing hover scope is hovered.</summary>
    public static readonly DependencyProperty RevealOnHoverProperty = DependencyProperty.RegisterAttached(
        "RevealOnHover", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnRevealOnHoverChanged));

    public static bool GetRevealOnHover(FrameworkElement element) => (bool)element.GetValue(RevealOnHoverProperty);

    public static void SetRevealOnHover(FrameworkElement element, bool value) => element.SetValue(RevealOnHoverProperty, value);

    /// <summary>Hover state of a scope, shared with the children that react to it.</summary>
    private static readonly DependencyProperty ScopeStateProperty = DependencyProperty.RegisterAttached(
        "ScopeState", typeof(HoverState), typeof(Motion), new PropertyMetadata(null));

    private static readonly DependencyProperty IsWiredProperty = DependencyProperty.RegisterAttached(
        "IsWired", typeof(bool), typeof(Motion), new PropertyMetadata(false));

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
            if (state.IsHovered != hovered)
            {
                state.IsHovered = hovered;
                state.Changed?.Invoke(hovered);
            }
        }

        scope.AddHandler(UIElement.PointerEnteredEvent, new PointerEventHandler((_, _) => Set(true)), handledEventsToo: true);
        scope.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler((_, _) => Set(false)), handledEventsToo: true);
        scope.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler((_, _) => Set(false)), handledEventsToo: true);
        scope.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, _) => Set(false)), handledEventsToo: true);
    }

    private static void OnRevealOnHoverChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement element || args.NewValue is not true)
        {
            return;
        }

        element.Opacity = 0;
        element.OpacityTransition = new ScalarTransition { Duration = FadeDuration };

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
        state.Changed += hovered => element.Opacity = hovered ? 1 : 0;
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

    private sealed class HoverState
    {
        public bool IsHovered;

        public Action<bool>? Changed;
    }
}
