using System.Numerics;
using ClaudeOS.Core.Design;
using ClaudeOS.Core.Presence;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI;

namespace ClaudeOS.Shell.Controls;

/// <summary>
/// The Spark: Claude's presence, drawn with Composition visuals so every animation runs on the
/// system compositor (independent of the UI thread, at the display's refresh rate). It is only
/// ever driven by a <see cref="PresenceFrame"/>; it has no opinions of its own.
///
///   idle        a slow breath, the ring at rest
///   listening   tighter and brighter; the ring closes
///   working     the ring orbits at a steady speed
///   needs you   an amber pulse that does not stop until you decide
///   done        settles, ring complete
///   refused     steady and dim: no shaking, no alarm
/// </summary>
public sealed class PresenceOrb : Grid
{
    private Compositor? _compositor;
    private ContainerVisual? _root;
    private SpriteVisual? _halo;
    private ShapeVisual? _core;
    private ShapeVisual? _ring;
    private CompositionSpriteShape? _ringShape;
    private CompositionColorBrush? _ringBrush;
    private CompositionColorGradientStop[] _coreStops = [];
    private PresenceState _shown = PresenceState.Dormant;
    private bool _animated = true;

    public PresenceOrb()
    {
        Loaded += (_, _) => Build();
        SizeChanged += (_, _) => Layout();
        ActualThemeChanged += (_, _) => Recolor();
    }

    public void Apply(PresenceFrame frame, bool reducedMotion)
    {
        _animated = !reducedMotion;
        if (_compositor is null)
        {
            return;
        }

        if (frame.State != _shown)
        {
            _shown = frame.State;
            Restyle(frame);
        }
    }

    private ResolvedTheme Theme => ThemeResolver.Resolve(ActualTheme == ElementTheme.Dark ? Appearance.Dark : Appearance.Light);

    private static Color C(Rgba c) => Color.FromArgb(c.A, c.R, c.G, c.B);

    private void Build()
    {
        if (_compositor is not null)
        {
            return;
        }

        var host = ElementCompositionPreview.GetElementVisual(this);
        _compositor = host.Compositor;
        _root = _compositor.CreateContainerVisual();

        _halo = _compositor.CreateSpriteVisual();
        var haloBrush = _compositor.CreateRadialGradientBrush();
        haloBrush.ColorStops.Add(_compositor.CreateColorGradientStop(0.55f, Color.FromArgb(70, 0xEB, 0x68, 0x34)));
        haloBrush.ColorStops.Add(_compositor.CreateColorGradientStop(1f, Color.FromArgb(0, 0xEB, 0x68, 0x34)));
        _halo.Brush = haloBrush;

        var coreGeometry = _compositor.CreateEllipseGeometry();
        var coreShape = _compositor.CreateSpriteShape(coreGeometry);
        var coreBrush = _compositor.CreateRadialGradientBrush();
        coreBrush.GradientOriginOffset = new Vector2(-0.24f, -0.3f);
        _coreStops =
        [
            _compositor.CreateColorGradientStop(0f, Colors.White),
            _compositor.CreateColorGradientStop(0.4f, Colors.White),
            _compositor.CreateColorGradientStop(0.75f, Colors.White),
            _compositor.CreateColorGradientStop(1f, Colors.White),
        ];
        foreach (var stop in _coreStops)
        {
            coreBrush.ColorStops.Add(stop);
        }

        coreShape.FillBrush = coreBrush;
        _core = _compositor.CreateShapeVisual();
        _core.Shapes.Add(coreShape);

        var ringGeometry = _compositor.CreateEllipseGeometry();
        _ringShape = _compositor.CreateSpriteShape(ringGeometry);
        _ringBrush = _compositor.CreateColorBrush(Colors.White);
        _ringShape.StrokeBrush = _ringBrush;
        _ringShape.StrokeThickness = 2f;
        _ringShape.StrokeStartCap = CompositionStrokeCap.Round;
        _ringShape.StrokeEndCap = CompositionStrokeCap.Round;
        _ring = _compositor.CreateShapeVisual();
        _ring.Shapes.Add(_ringShape);

        _root.Children.InsertAtTop(_halo);
        _root.Children.InsertAtTop(_ring);
        _root.Children.InsertAtTop(_core);
        ElementCompositionPreview.SetElementChildVisual(this, _root);

        Layout();
        Recolor();
        Restyle(new PresenceFrame(PresenceState.Idle, ""));
        _shown = PresenceState.Idle;
    }

    private void Layout()
    {
        if (_compositor is null || _root is null || ActualWidth < 1)
        {
            return;
        }

        var size = (float)Math.Min(ActualWidth, ActualHeight);
        var center = new Vector2(size / 2f);
        var coreRadius = size * 0.28f;
        var ringRadius = size * 0.40f;

        _root.Size = new Vector2(size);
        _halo!.Size = new Vector2(size);
        _halo.Offset = Vector3.Zero;
        ((CompositionRadialGradientBrush)_halo.Brush).EllipseCenter = new Vector2(0.5f);

        var coreShape = _core!.Shapes[0] as CompositionSpriteShape;
        ((CompositionEllipseGeometry)coreShape!.Geometry!).Radius = new Vector2(coreRadius);
        coreShape.Offset = center;
        _core.Size = new Vector2(size);
        _core.CenterPoint = new Vector3(center, 0);

        ((CompositionEllipseGeometry)_ringShape!.Geometry!).Radius = new Vector2(ringRadius);
        _ringShape.Offset = center;
        _ringShape.StrokeDashArray.Clear();
        // Dash lengths are in multiples of the stroke width: an open ring, like a gauge.
        var circumference = 2 * Math.PI * ringRadius / _ringShape.StrokeThickness;
        _ringShape.StrokeDashArray.Add((float)(circumference * 0.78));
        _ringShape.StrokeDashArray.Add((float)(circumference * 0.22));
        _ring!.Size = new Vector2(size);
        _ring.CenterPoint = new Vector3(center, 0);
        _ring.RotationAngleInDegrees = -62;
    }

    private void Recolor()
    {
        if (_compositor is null)
        {
            return;
        }

        var theme = Theme;
        var glow = theme.Color("accent.glow");
        var mark = theme.Color("accent.mark");
        var action = theme.Color("accent.action");
        Color Mix(Rgba a, Rgba b, double t) => Color.FromArgb(255, (byte)(a.R + ((b.R - a.R) * t)), (byte)(a.G + ((b.G - a.G) * t)), (byte)(a.B + ((b.B - a.B) * t)));
        _coreStops[0].Color = Mix(glow, new Rgba(255, 255, 255), 0.45);
        _coreStops[1].Color = C(glow);
        _coreStops[2].Color = C(mark);
        _coreStops[3].Color = C(action);
        if (_ringBrush is not null)
        {
            _ringBrush.Color = Color.FromArgb(140, mark.R, mark.G, mark.B);
        }
    }

    private void Restyle(PresenceFrame frame)
    {
        if (_compositor is null || _core is null || _ring is null || _halo is null)
        {
            return;
        }

        _core.StopAnimation(nameof(Visual.Scale));
        _ring.StopAnimation(nameof(Visual.RotationAngleInDegrees));
        _halo.StopAnimation(nameof(Visual.Opacity));

        var theme = Theme;
        var mark = theme.Color("accent.mark");
        var amber = Rgba.Parse(Tokens.Status.Warning);
        var tone = frame.State switch
        {
            PresenceState.NeedsYou => amber,
            PresenceState.Refused => theme.Color("ink.tertiary"),
            _ => mark,
        };
        _ringBrush!.Color = Color.FromArgb(frame.State == PresenceState.Refused ? (byte)110 : (byte)150, tone.R, tone.G, tone.B);

        var (breathPeriod, breathScale, orbitPeriod, haloOpacity) = frame.State switch
        {
            PresenceState.Idle => (Tokens.Presence.BreathPeriodMs, (float)Tokens.Presence.BreathScale, 0, 0.55f),
            PresenceState.Listening => (1800, 1.07f, 0, 0.9f),
            PresenceState.Understanding => (700, 1.12f, 0, 1f),
            PresenceState.Working => (Tokens.Presence.BreathPeriodMs, 1.04f, Tokens.Presence.OrbitPeriodMs, 0.85f),
            PresenceState.NeedsYou => (1500, 1.06f, 0, 1f),
            PresenceState.Done => (0, 1f, 0, 0.7f),
            PresenceState.Refused => (0, 1f, 0, 0.2f),
            _ => (0, 1f, 0, 0f),
        };

        _halo.Opacity = haloOpacity;
        if (!_animated)
        {
            return;
        }

        if (breathPeriod > 0)
        {
            var breath = _compositor.CreateVector3KeyFrameAnimation();
            breath.InsertKeyFrame(0f, Vector3.One);
            breath.InsertKeyFrame(0.5f, new Vector3(breathScale, breathScale, 1f), _compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(0.2f, 1f)));
            breath.InsertKeyFrame(1f, Vector3.One, _compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(0.2f, 1f)));
            breath.Duration = TimeSpan.FromMilliseconds(breathPeriod);
            breath.IterationBehavior = AnimationIterationBehavior.Forever;
            _core.StartAnimation(nameof(Visual.Scale), breath);
        }
        else
        {
            // Settle with a spring instead of snapping back.
            var settle = _compositor.CreateSpringVector3Animation();
            settle.FinalValue = Vector3.One;
            settle.DampingRatio = 0.7f;
            settle.Period = TimeSpan.FromMilliseconds(280);
            _core.StartAnimation(nameof(Visual.Scale), settle);
        }

        if (orbitPeriod > 0)
        {
            var orbit = _compositor.CreateScalarKeyFrameAnimation();
            orbit.InsertKeyFrame(0f, -62f);
            orbit.InsertKeyFrame(1f, 298f, _compositor.CreateLinearEasingFunction());
            orbit.Duration = TimeSpan.FromMilliseconds(orbitPeriod);
            orbit.IterationBehavior = AnimationIterationBehavior.Forever;
            _ring.StartAnimation(nameof(Visual.RotationAngleInDegrees), orbit);
        }
        else
        {
            var rest = _compositor.CreateSpringScalarAnimation();
            rest.FinalValue = -62f;
            rest.DampingRatio = 0.8f;
            rest.Period = TimeSpan.FromMilliseconds(320);
            _ring.StartAnimation(nameof(Visual.RotationAngleInDegrees), rest);
        }

        if (frame.State == PresenceState.NeedsYou)
        {
            var pulse = _compositor.CreateScalarKeyFrameAnimation();
            pulse.InsertKeyFrame(0f, 0.6f);
            pulse.InsertKeyFrame(0.5f, 1f, _compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(0.2f, 1f)));
            pulse.InsertKeyFrame(1f, 0.6f, _compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(0.2f, 1f)));
            pulse.Duration = TimeSpan.FromMilliseconds(1500);
            pulse.IterationBehavior = AnimationIterationBehavior.Forever;
            _halo.StartAnimation(nameof(Visual.Opacity), pulse);
        }
    }
}
