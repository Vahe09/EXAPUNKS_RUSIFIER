param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [Parameter(Mandatory = $true)]
    [string]$HalfOutputDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$source = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

public static class ExapunksLoadingTextureBuilder
{
    private static readonly Color Teal = Color.FromArgb(255, 57, 164, 123);
    private static readonly Color DarkTeal = Color.FromArgb(255, 29, 63, 55);

    private static Font CreateFont(float size, bool bold)
    {
        string[] candidates = new[]
        {
            "Bahnschrift SemiBold",
            "Bahnschrift",
            "Arial Narrow",
            "Arial"
        };

        foreach (string candidate in candidates)
        {
            try
            {
                return new Font(candidate, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            }
            catch
            {
            }
        }

        return new Font(FontFamily.GenericSansSerif, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
    }

    private static void SaveBitmap(Bitmap bitmap, string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void DrawTrackedText(Graphics g, string text, Font font, Brush brush, float canvasWidth, float y, float tracking)
    {
        using (StringFormat format = StringFormat.GenericTypographic)
        {
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            float width = 0f;
            foreach (char ch in text)
            {
                width += g.MeasureString(ch.ToString(), font, PointF.Empty, format).Width + tracking;
            }

            if (text.Length > 0)
            {
                width -= tracking;
            }

            float x = (canvasWidth - width) / 2f;
            foreach (char ch in text)
            {
                string value = ch.ToString();
                float charWidth = g.MeasureString(value, font, PointF.Empty, format).Width;
                g.DrawString(value, font, brush, x, y, format);
                x += charWidth + tracking;
            }
        }
    }

    private static void DrawCenteredText(Graphics g, string text, Font font, Brush brush, RectangleF rect)
    {
        using (StringFormat format = new StringFormat())
        {
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            g.DrawString(text, font, brush, rect, format);
        }
    }

    private static void BuildFull(string outputDir)
    {
        using (Font loadingFont = CreateFont(24f, true))
        using (Font subFont = CreateFont(22f, true))
        using (Font continueFont = CreateFont(26f, true))
        using (SolidBrush tealBrush = new SolidBrush(Teal))
        using (SolidBrush darkBrush = new SolidBrush(DarkTeal))
        {
            using (Bitmap empty = new Bitmap(1046, 62, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(empty))
            {
                g.Clear(Color.Black);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (Pen pen = new Pen(Teal, 4f))
                {
                    g.DrawRectangle(pen, 2, 2, empty.Width - 5, 31);
                }
                DrawTrackedText(g, "\u0417\u0410\u0413\u0420\u0423\u0417\u041A\u0410", loadingFont, tealBrush, empty.Width, 11f, 4.2f);
                DrawCenteredText(g, "\u0420\u0443\u0441\u0441\u0438\u0444\u0438\u043A\u0430\u0442\u043E\u0440 \u043E\u0442 Vahe", subFont, tealBrush, new RectangleF(0f, 40f, empty.Width, 20f));
                SaveBitmap(empty, Path.Combine(outputDir, "bar_empty.png"));
            }

            using (Bitmap full = new Bitmap(1046, 62, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(full))
            {
                g.Clear(Teal);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (SolidBrush footer = new SolidBrush(Color.Black))
                {
                    g.FillRectangle(footer, 0f, 35f, full.Width, full.Height - 35f);
                }
                DrawTrackedText(g, "\u0417\u0410\u0413\u0420\u0423\u0417\u041A\u0410", loadingFont, darkBrush, full.Width, 11f, 4.2f);
                DrawCenteredText(g, "\u0420\u0443\u0441\u0441\u0438\u0444\u0438\u043A\u0430\u0442\u043E\u0440 \u043E\u0442 Vahe", subFont, tealBrush, new RectangleF(0f, 40f, full.Width, 20f));
                SaveBitmap(full, Path.Combine(outputDir, "bar_full.png"));
            }

            using (Bitmap complete = new Bitmap(1046, 62, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(complete))
            {
                g.Clear(Color.Black);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                DrawTrackedText(g, "\u041D\u0410\u0416\u041C\u0418, \u0427\u0422\u041E\u0411\u042B \u041D\u0410\u0427\u0410\u0422\u042C", continueFont, tealBrush, complete.Width, 14f, 2.4f);
                SaveBitmap(complete, Path.Combine(outputDir, "bar_complete.png"));
            }
        }
    }

    private static void BuildHalf(string outputDir)
    {
        using (Font loadingFont = CreateFont(12f, true))
        using (Font subFont = CreateFont(12f, true))
        using (Font continueFont = CreateFont(13f, true))
        using (SolidBrush tealBrush = new SolidBrush(Teal))
        using (SolidBrush darkBrush = new SolidBrush(DarkTeal))
        {
            using (Bitmap empty = new Bitmap(523, 31, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(empty))
            {
                g.Clear(Color.Black);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (Pen pen = new Pen(Teal, 2f))
                {
                    g.DrawRectangle(pen, 1, 1, empty.Width - 3, 17);
                }
                DrawTrackedText(g, "\u0417\u0410\u0413\u0420\u0423\u0417\u041A\u0410", loadingFont, tealBrush, empty.Width, 5f, 2.0f);
                DrawCenteredText(g, "\u0420\u0443\u0441\u0441\u0438\u0444\u0438\u043A\u0430\u0442\u043E\u0440 \u043E\u0442 Vahe", subFont, tealBrush, new RectangleF(0f, 20f, empty.Width, 10f));
                SaveBitmap(empty, Path.Combine(outputDir, "bar_empty.png"));
            }

            using (Bitmap full = new Bitmap(523, 31, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(full))
            {
                g.Clear(Teal);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (SolidBrush footer = new SolidBrush(Color.Black))
                {
                    g.FillRectangle(footer, 0f, 19f, full.Width, full.Height - 19f);
                }
                DrawTrackedText(g, "\u0417\u0410\u0413\u0420\u0423\u0417\u041A\u0410", loadingFont, darkBrush, full.Width, 5f, 2.0f);
                DrawCenteredText(g, "\u0420\u0443\u0441\u0441\u0438\u0444\u0438\u043A\u0430\u0442\u043E\u0440 \u043E\u0442 Vahe", subFont, tealBrush, new RectangleF(0f, 20f, full.Width, 10f));
                SaveBitmap(full, Path.Combine(outputDir, "bar_full.png"));
            }

            using (Bitmap complete = new Bitmap(523, 31, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(complete))
            {
                g.Clear(Color.Black);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                DrawTrackedText(g, "\u041D\u0410\u0416\u041C\u0418, \u0427\u0422\u041E\u0411\u042B \u041D\u0410\u0427\u0410\u0422\u042C", continueFont, tealBrush, complete.Width, 8f, 1.2f);
                SaveBitmap(complete, Path.Combine(outputDir, "bar_complete.png"));
            }
        }
    }

    public static void Build(string outputDir, string halfOutputDir)
    {
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(halfOutputDir);
        BuildFull(outputDir);
        BuildHalf(halfOutputDir);
    }
}
"@

Add-Type -TypeDefinition $source -Language CSharp -ReferencedAssemblies @("System.dll", "System.Drawing.dll")
[ExapunksLoadingTextureBuilder]::Build($OutputDir, $HalfOutputDir)
