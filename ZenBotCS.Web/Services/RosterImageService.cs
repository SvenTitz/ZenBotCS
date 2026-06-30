using System.Text;
using SkiaSharp;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Renders a clan's CWL roster to a PNG entirely on the server, so the result is identical for
/// everyone regardless of device — it's the server's render with a bundled font, not a browser
/// screenshot. Mirrors the website grid: day cells coloured green (playing), amber (benched),
/// red (player opted out), green with a red corner (rostered despite opting out).
/// </summary>
public class RosterImageService
{
    private static readonly RosterDays[] DayFlags =
    [
        RosterDays.Day1, RosterDays.Day2, RosterDays.Day3, RosterDays.Day4,
        RosterDays.Day5, RosterDays.Day6, RosterDays.Day7
    ];

    // Cell fills + glyphs are the same in both themes; only the page chrome changes.
    private static readonly SKColor Playing = SKColor.Parse("#A5D6A7");
    private static readonly SKColor Benched = SKColor.Parse("#FFE082");
    private static readonly SKColor OptedOut = SKColor.Parse("#EF9A9A");

    private static readonly SKColor LightBg = SKColors.White;
    private static readonly SKColor LightHeader = SKColor.Parse("#E3E5F0");
    private static readonly SKColor LightRowAlt = SKColor.Parse("#F5F5F7");
    private static readonly SKColor LightText = SKColor.Parse("#212121");
    private static readonly SKColor LightSubtle = SKColor.Parse("#616161");

    private static readonly SKColor DarkBg = SKColor.Parse("#1A1A27");
    private static readonly SKColor DarkHeader = SKColor.Parse("#2B2D55");
    private static readonly SKColor DarkRowAlt = SKColor.Parse("#242438");
    private static readonly SKColor DarkText = SKColor.Parse("#E6E6EA");
    private static readonly SKColor DarkSubtle = SKColor.Parse("#A2A2B5");

    // Slightly darker tones of each fill for the shape glyphs (colour-blind cue: ✓ / − / ✗) —
    // visible enough to read the shape, but soft so they don't jump out.
    private static readonly SKColor SymPlaying = SKColor.Parse("#5AA35E");
    private static readonly SKColor SymBenched = SKColor.Parse("#C79A45");
    private static readonly SKColor SymOptedOut = SKColor.Parse("#D77A7A");

    private readonly SKTypeface _regular;
    private readonly SKTypeface _bold;
    // Fallback fonts for glyphs Roboto lacks (CJK, emoji, symbols), tried in order.
    private readonly SKTypeface[] _fallbacks;

    public RosterImageService()
    {
        var fontsDir = Path.Combine(AppContext.BaseDirectory, "Fonts");
        _regular = SKTypeface.FromFile(Path.Combine(fontsDir, "Roboto-Regular.ttf"));
        _bold = SKTypeface.FromFile(Path.Combine(fontsDir, "Roboto-Bold.ttf"));
        _fallbacks = new[]
            {
                "NotoSansJP-Regular.ttf", "NotoSansSC-Regular.ttf", "NotoSansKR-Regular.ttf",
                "NotoEmoji-Regular.ttf", "NotoSansSymbols-Regular.ttf", "NotoSansSymbols2-Regular.ttf"
            }
            .Select(f => Path.Combine(fontsDir, f))
            .Where(File.Exists)
            .Select(SKTypeface.FromFile)
            .Where(tf => tf is not null)
            .ToArray();
    }

    private static bool HasGlyph(SKTypeface tf, int codepoint)
    {
        var glyphs = tf.GetGlyphs(char.ConvertFromUtf32(codepoint));
        return glyphs.Length > 0 && glyphs[0] != 0;
    }

    // The font that can render this codepoint, or null if none can (e.g. emoji variation
    // selectors / zero-width joiners) — those are skipped rather than drawn as tofu boxes.
    private SKTypeface? PickFont(int codepoint, SKTypeface primary)
    {
        if (HasGlyph(primary, codepoint)) return primary;
        foreach (var fb in _fallbacks)
            if (HasGlyph(fb, codepoint)) return fb;
        return null;
    }

    // Split text into runs of consecutive codepoints that share a font (primary or a fallback).
    private IEnumerable<(SKTypeface Font, string Text)> Runs(string text, SKTypeface primary)
    {
        var sb = new StringBuilder();
        SKTypeface? current = null;
        foreach (var rune in text.EnumerateRunes())
        {
            var tf = PickFont(rune.Value, primary);
            if (tf is null)
                continue; // no font can render it (e.g. a variation selector) — drop it
            if (current is not null && tf != current)
            {
                yield return (current, sb.ToString());
                sb.Clear();
            }
            current = tf;
            sb.Append(rune.ToString());
        }
        if (current is not null && sb.Length > 0)
            yield return (current, sb.ToString());
    }

    // Draw left-aligned text with per-codepoint font fallback.
    private void DrawRuns(SKCanvas canvas, SKPaint paint, SKTypeface primary, string text, float x, float baseline)
    {
        foreach (var (font, run) in Runs(text, primary))
        {
            paint.Typeface = font;
            canvas.DrawText(run, x, baseline, paint);
            x += paint.MeasureText(run);
        }
        paint.Typeface = primary;
    }

    private float MeasureRuns(SKPaint paint, SKTypeface primary, string text)
    {
        var w = 0f;
        foreach (var (font, run) in Runs(text, primary))
        {
            paint.Typeface = font;
            w += paint.MeasureText(run);
        }
        paint.Typeface = primary;
        return w;
    }

    public byte[] Generate(string clanName, IReadOnlyList<CwlSignup> signups, bool dark = false)
    {
        var rows = signups.OrderBy(s => s.PlayerThLevel).ThenBy(s => s.PlayerName).ToList();

        var bg = dark ? DarkBg : LightBg;
        var headerBg = dark ? DarkHeader : LightHeader;
        var rowAlt = dark ? DarkRowAlt : LightRowAlt;
        var text = dark ? DarkText : LightText;
        var subtle = dark ? DarkSubtle : LightSubtle;

        const int pad = 14;
        const int rowH = 38;
        const int headerH = 50;
        const int titleH = 58;
        const int dayW = 46;
        const int thW = 56;
        const int countW = 48;
        const int legendH = 42;

        using var regular = new SKPaint { Typeface = _regular, TextSize = 20, IsAntialias = true };
        using var bold = new SKPaint { Typeface = _bold, TextSize = 20, IsAntialias = true };
        using var titleFont = new SKPaint { Typeface = _bold, TextSize = 28, IsAntialias = true, Color = text };
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.3f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };

        var nameW = Math.Max(190, (int)rows.Select(r => MeasureRuns(regular, _regular, r.PlayerName)).DefaultIfEmpty(0f).Max() + 2 * pad);
        var prefW = Math.Max(96, (int)rows.Select(r => regular.MeasureText(r.WarPreference.ToString())).DefaultIfEmpty(0f).Max() + 2 * pad);

        var width = nameW + thW + 7 * dayW + prefW + countW;
        var height = titleH + headerH + rows.Count * rowH + legendH;

        float xName = 0, xTh = nameW, xDay0 = nameW + thW, xPref = nameW + thW + 7 * dayW, xCount = xPref + prefW;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(bg);

        DrawRuns(canvas, titleFont, _bold, $"{clanName} — CWL Roster", pad, Baseline(titleFont, 0, titleH));

        float y = titleH;
        fill.Color = headerBg;
        canvas.DrawRect(SKRect.Create(0, y, width, headerH), fill);

        bold.Color = text;
        DrawLeft(canvas, "Player", bold, xName + pad, y, headerH);
        DrawCenter(canvas, "TH", bold, xTh, y, thW, headerH);
        for (var d = 0; d < 7; d++)
        {
            var count = rows.Count(r => r.EffectiveRosterDays.HasFlag(DayFlags[d]));
            DrawCenter(canvas, $"D{d + 1}", bold, xDay0 + d * dayW, y + 2, dayW, headerH / 2f);
            regular.Color = subtle;
            DrawCenter(canvas, count.ToString(), regular, xDay0 + d * dayW, y + headerH / 2f - 2, dayW, headerH / 2f);
        }
        DrawCenter(canvas, "Pref", bold, xPref, y, prefW, headerH);
        DrawCenter(canvas, "#", bold, xCount, y, countW, headerH);

        y += headerH;

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var rowY = y + i * rowH;
            if (i % 2 == 1)
            {
                fill.Color = rowAlt;
                canvas.DrawRect(SKRect.Create(0, rowY, width, rowH), fill);
            }

            regular.Color = text;
            DrawRuns(canvas, regular, _regular, r.PlayerName, xName + pad, Baseline(regular, rowY, rowH));
            DrawCenter(canvas, r.PlayerThLevel.ToString(), regular, xTh, rowY, thW, rowH);

            for (var d = 0; d < 7; d++)
            {
                var available = ((int)r.OptOutDays & (int)DayFlags[d]) == 0;
                var playing = r.EffectiveRosterDays.HasFlag(DayFlags[d]);
                DrawDayCell(canvas, fill, stroke, xDay0 + d * dayW, rowY, dayW, rowH, available, playing);
            }

            DrawCenter(canvas, r.WarPreference.ToString(), regular, xPref, rowY, prefW, rowH);
            var playingCount = DayFlags.Count(f => r.EffectiveRosterDays.HasFlag(f));
            DrawCenter(canvas, playingCount.ToString(), regular, xCount, rowY, countW, rowH);
        }

        DrawLegend(canvas, fill, stroke, regular, subtle, pad, height - legendH);

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // Baseline y to vertically centre text in a box of height h starting at 'top'.
    private static float Baseline(SKPaint font, float top, float h)
    {
        var m = font.FontMetrics;
        return top + h / 2f - (m.Ascent + m.Descent) / 2f;
    }

    private static void DrawCenter(SKCanvas canvas, string text, SKPaint font, float x, float y, float w, float h)
    {
        var tx = x + (w - font.MeasureText(text)) / 2f;
        canvas.DrawText(text, tx, Baseline(font, y, h), font);
    }

    private static void DrawLeft(SKCanvas canvas, string text, SKPaint font, float x, float y, float h) =>
        canvas.DrawText(text, x, Baseline(font, y, h), font);

    private static void DrawDayCell(SKCanvas canvas, SKPaint fill, SKPaint stroke, float x, float y, float w, float h, bool available, bool playing)
    {
        var inner = SKRect.Create(x + 2, y + 3, w - 4, h - 6);
        if (playing)
        {
            fill.Color = Playing;
            canvas.DrawRect(inner, fill);
            if (!available) // rostered despite opting out -> red corner triangle
            {
                using var path = new SKPath();
                path.MoveTo(inner.Right, inner.Top);
                path.LineTo(inner.Right, inner.Bottom);
                path.LineTo(inner.Right - inner.Width * 0.55f, inner.Bottom);
                path.Close();
                fill.Color = OptedOut;
                canvas.DrawPath(path, fill);
            }
            DrawCheck(canvas, stroke, inner, SymPlaying);
        }
        else if (available)
        {
            fill.Color = Benched;
            canvas.DrawRect(inner, fill);
            DrawDash(canvas, stroke, inner, SymBenched);
        }
        else
        {
            fill.Color = OptedOut;
            canvas.DrawRect(inner, fill);
            DrawCross(canvas, stroke, inner, SymOptedOut);
        }
    }

    private static void DrawCheck(SKCanvas canvas, SKPaint stroke, SKRect c, SKColor color)
    {
        stroke.Color = color;
        float cx = c.MidX, cy = c.MidY, s = Math.Min(c.Width, c.Height) * 0.5f;
        using var p = new SKPath();
        p.MoveTo(cx - s * 0.45f, cy + s * 0.02f);
        p.LineTo(cx - s * 0.12f, cy + s * 0.34f);
        p.LineTo(cx + s * 0.46f, cy - s * 0.34f);
        canvas.DrawPath(p, stroke);
    }

    private static void DrawDash(SKCanvas canvas, SKPaint stroke, SKRect c, SKColor color)
    {
        stroke.Color = color;
        var s = Math.Min(c.Width, c.Height) * 0.5f;
        canvas.DrawLine(c.MidX - s * 0.4f, c.MidY, c.MidX + s * 0.4f, c.MidY, stroke);
    }

    private static void DrawCross(SKCanvas canvas, SKPaint stroke, SKRect c, SKColor color)
    {
        stroke.Color = color;
        float cx = c.MidX, cy = c.MidY, s = Math.Min(c.Width, c.Height) * 0.23f;
        canvas.DrawLine(cx - s, cy - s, cx + s, cy + s, stroke);
        canvas.DrawLine(cx - s, cy + s, cx + s, cy - s, stroke);
    }

    private static void DrawLegend(SKCanvas canvas, SKPaint fill, SKPaint stroke, SKPaint font, SKColor subtle, float x, float top)
    {
        font.Color = subtle;
        var cx = x;
        var cy = top + 10;

        void Swatch(SKColor c, SKColor sym, int shape, string label)
        {
            var rect = SKRect.Create(cx, cy, 20, 20);
            fill.Color = c;
            canvas.DrawRect(rect, fill);
            if (shape == 0) DrawCheck(canvas, stroke, rect, sym);
            else if (shape == 1) DrawDash(canvas, stroke, rect, sym);
            else DrawCross(canvas, stroke, rect, sym);
            cx += 26;
            canvas.DrawText(label, cx, cy + 15, font);
            cx += font.MeasureText(label) + 26;
        }

        Swatch(Playing, SymPlaying, 0, "playing");
        Swatch(Benched, SymBenched, 1, "benched");
        Swatch(OptedOut, SymOptedOut, 2, "opted out");
    }
}
