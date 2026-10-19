using Avalonia;
using Avalonia.Media;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// The geometry produced for an edge marker, together with whether it should be
/// filled and/or stroked. Building this separately from drawing allows the
/// marker vocabulary to be unit-tested headlessly.
/// </summary>
internal readonly record struct DrawIoMarkerGeometry(Geometry? Geometry, bool Fill, bool Stroke);

/// <summary>
/// Builds draw.io edge markers (arrow heads/tails), ported from the official
/// <c>mxMarker</c> registry plus draw.io's extra marker definitions.
/// </summary>
internal static class DrawIoEdgeMarker
{
    /// <summary>
    /// Builds the geometry for a marker whose tip is at <paramref name="tip"/>.
    /// <paramref name="direction"/> points from the edge toward the tip (i.e.
    /// the travel direction), matching mxMarker's unit vector.
    /// </summary>
    internal static DrawIoMarkerGeometry Build(
        string kind,
        Point tip,
        Vector direction,
        double size,
        double strokeWidth,
        bool filled)
    {
        var u = Normalize(direction);
        if (u.Length < 1e-9)
        {
            return new DrawIoMarkerGeometry(null, false, false);
        }

        var ux = u.X;
        var uy = u.Y;
        var sw = strokeWidth;

        return kind.ToLowerInvariant() switch
        {
            "none" or "" => new DrawIoMarkerGeometry(null, false, false),
            "classic" => Arrow(tip, ux, uy, size, sw, widthFactor: 2, notch: true, filled),
            "classicthin" => Arrow(tip, ux, uy, size, sw, widthFactor: 3, notch: true, filled),
            "block" => Arrow(tip, ux, uy, size, sw, widthFactor: 2, notch: false, filled),
            "blockthin" => Arrow(tip, ux, uy, size, sw, widthFactor: 3, notch: false, filled),
            "open" => OpenArrow(tip, ux, uy, size, sw, widthFactor: 2),
            "openthin" => OpenArrow(tip, ux, uy, size, sw, widthFactor: 3),
            "oval" => Oval(tip, ux, uy, size, filled),
            "diamond" => Diamond(tip, ux, uy, size, sw, thin: false, filled),
            "diamondthin" => Diamond(tip, ux, uy, size, sw, thin: true, filled),
            "async" => Async(tip, ux, uy, size, sw, filled),
            "box" => Box(tip, ux, uy, size, sw, filled),
            "halfcircle" => HalfCircle(tip, ux, uy, size, sw),
            "dash" => Dash(tip, ux, uy, size, sw),
            "basedash" => BaseDash(tip, ux, uy, size, sw),
            "cross" => Cross(tip, ux, uy, size, sw),
            "circle" => Circle(tip, ux, uy, size, filled),
            "circleplus" => CirclePlus(tip, ux, uy, size, sw),
            "doubleblock" => DoubleBlock(tip, ux, uy, size, sw, filled),
            "erone" => ErOne(tip, ux, uy, size, sw),
            "ermandone" => ErMandOne(tip, ux, uy, size, sw),
            "ermany" => ErMany(tip, ux, uy, size, sw),
            "eronetomany" => ErOneToMany(tip, ux, uy, size, sw),
            "erzerotoone" => ErZeroToOne(tip, ux, uy, size, sw),
            "erzerotomany" => ErZeroToMany(tip, ux, uy, size, sw),
            // Unknown marker: fall back to a classic filled arrow.
            _ => Arrow(tip, ux, uy, size, sw, widthFactor: 2, notch: true, filled: true),
        };
    }

    /// <summary>
    /// Returns how far the edge line must stop short of the marker tip so the
    /// line does not poke out past the marker, ported from mxConnector's
    /// per-marker endpoint adjustments (<c>pe</c> offsets in mxMarker.js).
    /// </summary>
    internal static double LineInset(string kind, double size, double strokeWidth)
    {
        var sw = strokeWidth;
        var u = size + sw;
        const double k = 1.118;

        return kind.ToLowerInvariant() switch
        {
            "none" or "" => 0,
            // createArrow: pe += -unit * f - endOffset (f = 3/4 classic, 1 block).
            "classic" or "classicthin" => 0.75 * u + sw * k,
            "block" or "blockthin" => u + sw * k,
            // createOpenArrow: pe += -endOffset * 2.
            "open" or "openthin" => sw * k * 2,
            // oval: pe -= unit * (size / 2).
            "oval" => size / 2,
            // diamond: pe += -unit - endOffset (swFactor 0.7071 / 0.9862).
            "diamond" => u + sw * 0.7071,
            "diamondthin" => u + sw * 0.9862,
            // doubleBlock: pe += -unit * f * 2 - endOffset.
            "doubleblock" => 1.5 * u + sw * k,
            // async: pe += -unit - endOffset.
            "async" => u + sw * k,
            // Base of the marker lies ~u behind the tip for the remaining kinds.
            _ => u + 1,
        };
    }

    /// <summary>
    /// Paints a marker whose tip is at <paramref name="tip"/>.
    /// </summary>
    public static void Paint(
        DrawingContext dc,
        string kind,
        Point tip,
        Vector direction,
        double size,
        double strokeWidth,
        IBrush stroke,
        bool filled)
    {
        var result = Build(kind, tip, direction, size, strokeWidth, filled);
        if (result.Geometry is null)
        {
            return;
        }

        // draw.io paints markers with fillAndStroke (filled) or stroke (open).
        // The stroke is what makes the stroked arrow tip reach the endpoint, so
        // it must be applied for filled markers too.
        var pen = new Pen(stroke, Math.Max(1, strokeWidth));
        dc.DrawGeometry(result.Fill ? stroke : null, pen, result.Geometry);
    }

    private static DrawIoMarkerGeometry Result(Geometry geometry, bool fill, bool stroke) =>
        new(geometry, fill, stroke);

    // ---- Basic arrows (createArrow in mxMarker.js) ----

    private static DrawIoMarkerGeometry Arrow(
        Point tip, double ux, double uy, double size, double sw,
        double widthFactor, bool notch, bool filled)
    {
        var endOffX = ux * sw * 1.118;
        var endOffY = uy * sw * 1.118;

        var unitX = ux * (size + sw);
        var unitY = uy * (size + sw);

        var pt = new Point(tip.X - endOffX, tip.Y - endOffY);

        var p2 = new Point(pt.X - unitX - unitY / widthFactor, pt.Y - unitY + unitX / widthFactor);
        var p4 = new Point(pt.X + unitY / widthFactor - unitX, pt.Y - unitY - unitX / widthFactor);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(pt, true);
            ctx.LineTo(p2);
            if (notch)
            {
                ctx.LineTo(new Point(pt.X - unitX * 3 / 4, pt.Y - unitY * 3 / 4));
            }

            ctx.LineTo(p4);
            ctx.EndFigure(true);
        }

        return Result(geometry, filled, !filled);
    }

    private static DrawIoMarkerGeometry OpenArrow(
        Point tip, double ux, double uy, double size, double sw, double widthFactor)
    {
        var endOffX = ux * sw * 1.118;
        var endOffY = uy * sw * 1.118;

        var unitX = ux * (size + sw);
        var unitY = uy * (size + sw);

        var pt = new Point(tip.X - endOffX, tip.Y - endOffY);
        var p1 = new Point(pt.X - unitX - unitY / widthFactor, pt.Y - unitY + unitX / widthFactor);
        var p3 = new Point(pt.X + unitY / widthFactor - unitX, pt.Y - unitY - unitX / widthFactor);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(p1, false);
            ctx.LineTo(pt);
            ctx.LineTo(p3);
            ctx.EndFigure(false);
        }

        return Result(geometry, false, true);
    }

    private static DrawIoMarkerGeometry Oval(Point tip, double ux, double uy, double size, bool filled)
    {
        var a = size / 2;
        var center = new Point(tip.X - ux * a, tip.Y - uy * a);
        var geometry = new EllipseGeometry(new Rect(center.X - a, center.Y - a, size, size));
        return Result(geometry, filled, !filled);
    }

    private static DrawIoMarkerGeometry Diamond(
        Point tip, double ux, double uy, double size, double sw, bool thin, bool filled)
    {
        var swFactor = thin ? 0.9862 : 0.7071;
        var endOffX = ux * sw * swFactor;
        var endOffY = uy * sw * swFactor;

        var unitX = ux * (size + sw);
        var unitY = uy * (size + sw);

        var pt = new Point(tip.X - endOffX, tip.Y - endOffY);
        var tk = thin ? 3.4 : 2.0;

        var p2 = new Point(pt.X - unitX / 2 - unitY / tk, pt.Y + unitX / tk - unitY / 2);
        var p3 = new Point(pt.X - unitX, pt.Y - unitY);
        var p4 = new Point(pt.X - unitX / 2 + unitY / tk, pt.Y - unitY / 2 - unitX / tk);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(pt, true);
            ctx.LineTo(p2);
            ctx.LineTo(p3);
            ctx.LineTo(p4);
            ctx.EndFigure(true);
        }

        return Result(geometry, filled, !filled);
    }

    private static DrawIoMarkerGeometry Async(
        Point tip, double ux, double uy, double size, double sw, bool filled)
    {
        var endOffX = ux * sw * 1.118;
        var endOffY = uy * sw * 1.118;

        var unitX = ux * (size + sw);
        var unitY = uy * (size + sw);

        var pt = new Point(tip.X - endOffX, tip.Y - endOffY);
        var p2 = new Point(pt.X + unitY / 2 - unitX, pt.Y - unitY - unitX / 2);
        var p3 = new Point(pt.X - unitX, pt.Y - unitY);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(pt, true);
            ctx.LineTo(p2);
            ctx.LineTo(p3);
            ctx.EndFigure(true);
        }

        return Result(geometry, filled, !filled);
    }

    // ---- draw.io extra markers ----

    private static DrawIoMarkerGeometry Box(
        Point tip, double ux, double uy, double size, double sw, bool filled)
    {
        var t = ux * (size + sw + 1);
        var d = uy * (size + sw + 1);

        var e = new Point(tip.X + t / 2, tip.Y + d / 2);
        var p1 = new Point(e.X - t / 2 - d / 2, e.Y - d / 2 + t / 2);
        var p2 = new Point(e.X - t / 2 + d / 2, e.Y - d / 2 - t / 2);
        var p3 = new Point(e.X + d / 2 - 3 * t / 2, e.Y - 3 * d / 2 - t / 2);
        var p4 = new Point(e.X - d / 2 - 3 * t / 2, e.Y - 3 * d / 2 + t / 2);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(p1, true);
            ctx.LineTo(p2);
            ctx.LineTo(p3);
            ctx.LineTo(p4);
            ctx.EndFigure(true);
        }

        return Result(geometry, filled, true);
    }

    private static DrawIoMarkerGeometry HalfCircle(
        Point tip, double ux, double uy, double size, double sw)
    {
        var t = ux * (size + sw + 1);
        var d = uy * (size + sw + 1);
        var e = new Point(tip.X + t, tip.Y + d);
        var back = new Point(tip.X, tip.Y);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(e.X - d, e.Y + t), false);
            ctx.QuadraticBezierTo(new Point(back.X - d, back.Y + t), back);
            ctx.QuadraticBezierTo(new Point(back.X + d, back.Y - t), new Point(e.X + d, e.Y - t));
            ctx.EndFigure(false);
        }

        return Result(geometry, false, true);
    }

    private static DrawIoMarkerGeometry Dash(
        Point tip, double ux, double uy, double size, double sw)
    {
        var t = ux * (size + sw + 1);
        var d = uy * (size + sw + 1);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(tip.X - t / 2 - d / 2, tip.Y - d / 2 + t / 2), false);
            ctx.LineTo(new Point(tip.X + d / 2 - 3 * t / 2, tip.Y - 3 * d / 2 - t / 2));
            ctx.EndFigure(false);
        }

        return Result(geometry, false, true);
    }

    private static DrawIoMarkerGeometry BaseDash(
        Point tip, double ux, double uy, double size, double sw)
    {
        var nx = ux * (size + sw + 1);
        var ny = uy * (size + sw + 1);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(tip.X - ny / 2, tip.Y + nx / 2), false);
            ctx.LineTo(new Point(tip.X + ny / 2, tip.Y - nx / 2));
            ctx.EndFigure(false);
        }

        return Result(geometry, false, true);
    }

    private static DrawIoMarkerGeometry Cross(
        Point tip, double ux, double uy, double size, double sw)
    {
        var t = ux * (size + sw + 1);
        var d = uy * (size + sw + 1);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(tip.X - t / 2 - d / 2, tip.Y - d / 2 + t / 2), false);
            ctx.LineTo(new Point(tip.X + d / 2 - 3 * t / 2, tip.Y - 3 * d / 2 - t / 2));
            ctx.EndFigure(false);
            ctx.BeginFigure(new Point(tip.X - t / 2 + d / 2, tip.Y - d / 2 - t / 2), false);
            ctx.LineTo(new Point(tip.X - d / 2 - 3 * t / 2, tip.Y - 3 * d / 2 + t / 2));
            ctx.EndFigure(false);
        }

        return Result(geometry, false, true);
    }

    private static DrawIoMarkerGeometry Circle(Point tip, double ux, double uy, double size, bool filled)
    {
        var r = size * 0.5;
        var center = new Point(tip.X - ux * r, tip.Y - uy * r);
        var geometry = new EllipseGeometry(new Rect(center.X - r, center.Y - r, r * 2, r * 2));
        return Result(geometry, filled, true);
    }

    private static DrawIoMarkerGeometry CirclePlus(
        Point tip, double ux, double uy, double size, double sw)
    {
        var e = ux * (size + 2 * sw);
        var g = uy * (size + 2 * sw);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(tip.X - ux * sw, tip.Y - uy * sw), false);
            ctx.LineTo(new Point(tip.X - 2 * e + ux * sw, tip.Y - 2 * g + uy * sw));
            ctx.EndFigure(false);
            ctx.BeginFigure(new Point(tip.X - e - g + uy * sw, tip.Y - g + e - ux * sw), false);
            ctx.LineTo(new Point(tip.X + g - e - uy * sw, tip.Y - g - e + ux * sw));
            ctx.EndFigure(false);
        }

        // Reuse the circle figure plus the cross lines.
        var circle = Circle(tip, ux, uy, size, filled: false);
        var group = new GeometryGroup();
        if (circle.Geometry is not null)
        {
            group.Children.Add(circle.Geometry);
        }

        group.Children.Add(geometry);
        return Result(group, false, true);
    }

    private static DrawIoMarkerGeometry DoubleBlock(
        Point tip, double ux, double uy, double size, double sw, bool filled)
    {
        const double widthFactor = 2;
        var endOffX = ux * sw * 1.118;
        var endOffY = uy * sw * 1.118;

        var unitX = ux * (size + sw);
        var unitY = uy * (size + sw);

        var pt = new Point(tip.X - endOffX, tip.Y - endOffY);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(pt, true);
            ctx.LineTo(new Point(pt.X - unitX - unitY / widthFactor, pt.Y - unitY + unitX / widthFactor));
            ctx.LineTo(new Point(pt.X + unitY / widthFactor - unitX, pt.Y - unitY - unitX / widthFactor));
            ctx.EndFigure(true);
            ctx.BeginFigure(new Point(pt.X - unitX, pt.Y - unitY), true);
            ctx.LineTo(new Point(pt.X - 2 * unitX - 0.5 * unitY, pt.Y + 0.5 * unitX - 2 * unitY));
            ctx.LineTo(new Point(pt.X - 2 * unitX + 0.5 * unitY, pt.Y - 0.5 * unitX - 2 * unitY));
            ctx.EndFigure(true);
        }

        return Result(geometry, filled, true);
    }

    // ---- ER markers ----

    private static DrawIoMarkerGeometry ErOne(
        Point tip, double ux, double uy, double size, double sw)
    {
        var t = ux * (size + sw + 1);
        var d = uy * (size + sw + 1);
        var center = new Point(tip.X - t / 2, tip.Y - d / 2);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(center.X - t / 2 - d / 2, center.Y - d / 2 + t / 2), false);
            ctx.LineTo(new Point(center.X - t / 2 + d / 2, center.Y - d / 2 - t / 2));
            ctx.EndFigure(false);
        }

        return Result(geometry, false, true);
    }

    private static DrawIoMarkerGeometry ErMandOne(
        Point tip, double ux, double uy, double size, double sw)
    {
        var t = ux * (size + sw + 1);
        var d = uy * (size + sw + 1);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < 2; i++)
            {
                var c = new Point(tip.X - t / 2 - i * t, tip.Y - d / 2 - i * d);
                ctx.BeginFigure(new Point(c.X - t / 2 - d / 2, c.Y - d / 2 + t / 2), false);
                ctx.LineTo(new Point(c.X - t / 2 + d / 2, c.Y - d / 2 - t / 2));
                ctx.EndFigure(false);
            }
        }

        return Result(geometry, false, true);
    }

    private static DrawIoMarkerGeometry ErMany(
        Point tip, double ux, double uy, double size, double sw)
    {
        var t = ux * (size + sw + 1);
        var d = uy * (size + sw + 1);
        var back = new Point(tip.X - t, tip.Y - d);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(tip, false);
            ctx.LineTo(back);
            ctx.EndFigure(false);
            ctx.BeginFigure(new Point(tip.X + d / 2, tip.Y - t / 2), false);
            ctx.LineTo(back);
            ctx.EndFigure(false);
            ctx.BeginFigure(new Point(tip.X - d / 2, tip.Y + t / 2), false);
            ctx.LineTo(back);
            ctx.EndFigure(false);
        }

        return Result(geometry, false, true);
    }

    private static DrawIoMarkerGeometry ErOneToMany(
        Point tip, double ux, double uy, double size, double sw)
    {
        var many = ErMany(tip, ux, uy, size, sw);
        var t = ux * (size + sw + 1);
        var d = uy * (size + sw + 1);

        var bar = new StreamGeometry();
        using (var ctx = bar.Open())
        {
            var c = new Point(tip.X - t * 1.5, tip.Y - d * 1.5);
            ctx.BeginFigure(new Point(c.X + d / 2, c.Y - t / 2), false);
            ctx.LineTo(new Point(c.X - d / 2, c.Y + t / 2));
            ctx.EndFigure(false);
        }

        var group = new GeometryGroup();
        if (many.Geometry is not null)
        {
            group.Children.Add(many.Geometry);
        }

        group.Children.Add(bar);
        return Result(group, false, true);
    }

    private static DrawIoMarkerGeometry ErZeroToOne(
        Point tip, double ux, double uy, double size, double sw)
    {
        var t = ux * (size + sw + 1);
        var d = uy * (size + sw + 1);

        var r = size * 0.4;
        var c = new Point(tip.X - t * 0.75, tip.Y - d * 0.75);
        var group = new GeometryGroup();
        group.Children.Add(new EllipseGeometry(new Rect(c.X - r, c.Y - r, r * 2, r * 2)));

        var bar = new StreamGeometry();
        using (var ctx = bar.Open())
        {
            var b = new Point(tip.X - t * 1.6, tip.Y - d * 1.6);
            ctx.BeginFigure(new Point(b.X + d / 2, b.Y - t / 2), false);
            ctx.LineTo(new Point(b.X - d / 2, b.Y + t / 2));
            ctx.EndFigure(false);
        }

        group.Children.Add(bar);
        return Result(group, false, true);
    }

    private static DrawIoMarkerGeometry ErZeroToMany(
        Point tip, double ux, double uy, double size, double sw)
    {
        var many = ErMany(tip, ux, uy, size, sw);
        var t = ux * (size + sw + 1);
        var d = uy * (size + sw + 1);

        var r = size * 0.4;
        var c = new Point(tip.X - t * 1.4, tip.Y - d * 1.4);

        var group = new GeometryGroup();
        if (many.Geometry is not null)
        {
            group.Children.Add(many.Geometry);
        }

        group.Children.Add(new EllipseGeometry(new Rect(c.X - r, c.Y - r, r * 2, r * 2)));
        return Result(group, false, true);
    }

    private static Vector Normalize(Vector v)
    {
        var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        return len < 1e-9 ? default : new Vector(v.X / len, v.Y / len);
    }
}
