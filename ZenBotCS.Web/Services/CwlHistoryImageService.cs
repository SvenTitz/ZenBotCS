using System.Globalization;
using System.Text;
using SkiaSharp;
using ZenBotCS.Entities.Models.Cwl;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Renders a compact CWL performance summary to a PNG on the server (same approach as
/// <see cref="RosterImageService"/>: deterministic Skia render with bundled fonts, not a screenshot).
/// One row per player sorted by Score, with the key columns for posting in Discord: TH, Hits,
/// Reach +/-, Ave ⭐, Ave %, Score, plus a per-day totals footer.
/// </summary>
public class CwlHistoryImageService
{
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

    private static readonly SKColor Positive = SKColor.Parse("#5AA35E");
    private static readonly SKColor Negative = SKColor.Parse("#D77A7A");

    private readonly SKTypeface _regular;
    private readonly SKTypeface _bold;
    private readonly SKTypeface[] _fallbacks;

    public CwlHistoryImageService()
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
            .Select(p => SKTypeface.FromFile(p))
            .Where(tf => tf is not null)
            .ToArray();
    }

    public byte[] Generate(CwlSeasonPerformance perf, string clanName, bool dark = true)
    {
        var rows = perf.Players; // already sorted by Score desc

        var bg = dark ? DarkBg : LightBg;
        var headerBg = dark ? DarkHeader : LightHeader;
        var rowAlt = dark ? DarkRowAlt : LightRowAlt;
        var text = dark ? DarkText : LightText;
        var subtle = dark ? DarkSubtle : LightSubtle;

        const int pad = 16;
        const int rowH = 34;
        const int headerH = 40;
        const int titleH = 66;
        const int footerH = 40;
        const int rankW = 44;
        const int thW = 50;
        const int hitsW = 56;
        const int reachW = 72;
        const int starW = 68;
        const int destW = 60;
        const int scoreW = 72;
        const int bonusW = 70;

        using var regular = new SKPaint { Typeface = _regular, TextSize = 19, IsAntialias = true };
        using var bold = new SKPaint { Typeface = _bold, TextSize = 19, IsAntialias = true };
        using var titleFont = new SKPaint { Typeface = _bold, TextSize = 26, IsAntialias = true, Color = text };
        using var subFont = new SKPaint { Typeface = _regular, TextSize = 16, IsAntialias = true, Color = subtle };
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        var nameW = Math.Max(200, (int)rows.Select(r => MeasureRuns(regular, _regular, r.PlayerName)).DefaultIfEmpty(0f).Max() + 2 * pad);

        var width = nameW + rankW + thW + hitsW + reachW + starW + destW + scoreW + bonusW;
        var height = titleH + headerH + rows.Count * rowH + footerH;

        float xRank = 0,
              xName = rankW,
              xTh = rankW + nameW,
              xHits = xTh + thW,
              xReach = xHits + hitsW,
              xStar = xReach + reachW,
              xDest = xStar + starW,
              xScore = xDest + destW,
              xBonus = xScore + scoreW;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(bg);

        var monthLabel = MonthLabel(perf.Season, perf.StartTime);
        DrawRuns(canvas, titleFont, _bold, $"{clanName} — CWL {monthLabel}", pad, Baseline(titleFont, 8, titleH - 24));
        canvas.DrawText($"Started {perf.StartTime:dd MMM yyyy} · sorted by score", pad, titleH - 12, subFont);

        float y = titleH;
        fill.Color = headerBg;
        canvas.DrawRect(SKRect.Create(0, y, width, headerH), fill);

        bold.Color = text;
        DrawCenter(canvas, "#", bold, xRank, y, rankW, headerH);
        DrawLeft(canvas, "Player", bold, xName + pad, y, headerH);
        DrawCenter(canvas, "TH", bold, xTh, y, thW, headerH);
        DrawCenter(canvas, "Hits", bold, xHits, y, hitsW, headerH);
        DrawCenter(canvas, "Reach", bold, xReach, y, reachW, headerH);
        DrawRunsCenter(canvas, bold, _bold, "Avg★", xStar, y, starW, headerH);
        DrawCenter(canvas, "Avg%", bold, xDest, y, destW, headerH);
        DrawCenter(canvas, "Score", bold, xScore, y, scoreW, headerH);
        DrawCenter(canvas, "Bonus", bold, xBonus, y, bonusW, headerH);

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

            regular.Color = subtle;
            DrawCenter(canvas, (i + 1).ToString(), regular, xRank, rowY, rankW, rowH);

            regular.Color = text;
            DrawRuns(canvas, regular, _regular, r.PlayerName, xName + pad, Baseline(regular, rowY, rowH));
            DrawCenter(canvas, r.TownHallLevel.ToString(), regular, xTh, rowY, thW, rowH);
            DrawCenter(canvas, r.Hits.ToString(), regular, xHits, rowY, hitsW, rowH);

            regular.Color = r.ReachPlusMinus > 0 ? Positive : r.ReachPlusMinus < 0 ? Negative : subtle;
            DrawCenter(canvas, SignedReach(r.ReachPlusMinus), regular, xReach, rowY, reachW, rowH);

            regular.Color = text;
            DrawCenter(canvas, r.AverageStars.ToString("0.00", CultureInfo.InvariantCulture), regular, xStar, rowY, starW, rowH);
            DrawCenter(canvas, Math.Round(r.AverageDestruction).ToString(CultureInfo.InvariantCulture), regular, xDest, rowY, destW, rowH);

            bold.Color = text;
            DrawCenter(canvas, r.Score.ToString("0.00", CultureInfo.InvariantCulture), bold, xScore, rowY, scoreW, rowH);

            if (r.Bonus)
            {
                regular.Color = Positive;
                DrawRunsCenter(canvas, regular, _regular, "✓", xBonus, rowY, bonusW, rowH);
            }
        }

        // Footer: clan totals across the CWL.
        var footY = y + rows.Count * rowH;
        fill.Color = headerBg;
        canvas.DrawRect(SKRect.Create(0, footY, width, footerH), fill);
        bold.Color = text;
        var totalStars = perf.DailyTotalStars.Sum();
        var days = perf.DailyTotalStars.Count(s => s > 0);
        // DrawRuns (not DrawLeft) so the ★ falls back to a symbol font instead of a tofu box.
        DrawRuns(canvas, bold, _bold, $"Total: {totalStars}★ over {days} days", xName + pad, Baseline(bold, footY, footerH));

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string SignedReach(int reach) => reach > 0 ? $"+{reach}" : reach.ToString(CultureInfo.InvariantCulture);

    private static string MonthLabel(string season, DateTime start)
    {
        if (DateTime.TryParseExact(season, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        return start.ToString("MMM yyyy", CultureInfo.InvariantCulture);
    }

    // --- text helpers (mirrors RosterImageService's font-fallback rendering) ---

    private static bool HasGlyph(SKTypeface tf, int codepoint)
    {
        var glyphs = tf.GetGlyphs(char.ConvertFromUtf32(codepoint));
        return glyphs.Length > 0 && glyphs[0] != 0;
    }

    private SKTypeface? PickFont(int codepoint, SKTypeface primary)
    {
        if (HasGlyph(primary, codepoint)) return primary;
        foreach (var fb in _fallbacks)
            if (HasGlyph(fb, codepoint)) return fb;
        return null;
    }

    private IEnumerable<(SKTypeface Font, string Text)> Runs(string text, SKTypeface primary)
    {
        var sb = new StringBuilder();
        SKTypeface? current = null;
        foreach (var rune in text.EnumerateRunes())
        {
            var tf = PickFont(rune.Value, primary);
            if (tf is null)
                continue;
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

    private void DrawRunsCenter(SKCanvas canvas, SKPaint paint, SKTypeface primary, string text, float x, float y, float w, float h)
    {
        var tw = MeasureRuns(paint, primary, text);
        DrawRuns(canvas, paint, primary, text, x + (w - tw) / 2f, Baseline(paint, y, h));
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
}
