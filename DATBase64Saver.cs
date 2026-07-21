using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace Base64ToImage;

/// <summary>
/// 将 Base64 图片数据直接保存到本地文件的工具类。
/// 独立完成解析、渲染、保存全流程，不依赖 <see cref="DATBase64Helper"/>。
/// </summary>
public static class DATBase64Saver
{
    /// <summary>
    /// 将 Base64 字符串渲染后直接保存到本地文件。
    /// 支持 Data URI 前缀、HL7 ED 前缀（BMP^/DAT^/JPG^/PNG^）或纯 Base64。
    /// </summary>
    /// <param name="input">Base64 字符串，可带 data:...base64, 前缀或 HL7 的 BMP^/DAT^/JPG^/PNG^ 前缀</param>
    /// <param name="savePath">保存路径（含文件名，如 C:\output\result.png）</param>
    /// <param name="useWhiteBg">白色背景还是透明背景（仅对标准图片格式有效）</param>
    public static void Save(string input, string savePath, bool useWhiteBg)
    {
        // 1. 去除 Data URI 前缀（data:image/xxx;base64,）
        var cleanInput = input;
        var commaIndex = cleanInput.IndexOf(',');
        if (commaIndex >= 0)
        {
            cleanInput = cleanInput[(commaIndex + 1)..];
        }

        // 2. 检测 HL7 ED 数据类型前缀（如 BMP^、DAT^、JPG^、PNG^ 等）
        var dataType = "";
        var caretIndex = cleanInput.LastIndexOf('^');
        if (caretIndex >= 0)
        {
            var prefix = cleanInput[..caretIndex].Trim();
            var lastPipe = prefix.LastIndexOf('|');
            if (lastPipe >= 0) prefix = prefix[(lastPipe + 1)..];
            if (prefix.Length > 0 && prefix.Length <= 10)
            {
                dataType = prefix.ToUpperInvariant();
            }
            cleanInput = cleanInput[(caretIndex + 1)..];
        }

        // 3. 去除空白字符
        cleanInput = cleanInput.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");

        // 4. Base64 解码后渲染
        var bytes = Convert.FromBase64String(cleanInput);
        using var bitmap = Render(bytes, dataType, useWhiteBg);

        // 5. 确保目标目录存在
        var dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // 6. 保存到本地文件
        bitmap.Save(savePath, ImageFormat.Png);
    }

    // ========================================================================
    //  渲染
    // ========================================================================

    private static Bitmap Render(byte[] data, string dataType, bool useWhiteBg)
    {
        var type = dataType.ToUpperInvariant();

        if (type is "DAT" or "BIN")
        {
            return RenderRawData(data);
        }

        // BMP/JPG/PNG/空：标准图片格式，直接加载后去背景
        using var ms = new MemoryStream(data);
        using var srcBitmap = new Bitmap(ms);
        return RenderWithCleanBackground(srcBitmap, useWhiteBg);
    }

    private static Bitmap RenderRawData(byte[] data)
    {
        if (data.Length == 256)
        {
            return RenderHistogram(data);
        }
        if (data.Length == 65536)
        {
            return RenderScattergram(data, 256, 256);
        }

        // 其他大小：尝试按平方排列
        var size = (int)Math.Sqrt(data.Length);
        if (size * size == data.Length)
        {
            return RenderScattergram(data, size, size);
        }

        // 实在不行按 1 行排列
        return RenderScattergram(data, data.Length, 1);
    }

    // ========================================================================
    //  标准图片去背景
    // ========================================================================

    private static Bitmap RenderWithCleanBackground(Bitmap srcBitmap, bool useWhiteBg)
    {
        var width = srcBitmap.Width;
        var height = srcBitmap.Height;

        using var normalized = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(normalized))
        {
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(
                srcBitmap,
                new Rectangle(0, 0, width, height),
                new Rectangle(0, 0, width, height),
                GraphicsUnit.Pixel);
        }

        var backgroundColor = GetDominantCornerColor(normalized);
        var backgroundMask = BuildEdgeConnectedBackgroundMask(normalized, backgroundColor);
        var dstFormat = useWhiteBg ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;
        var dst = new Bitmap(width, height, dstFormat);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if (backgroundMask[index])
                {
                    dst.SetPixel(x, y, useWhiteBg ? Color.White : Color.Transparent);
                    continue;
                }

                var pixel = normalized.GetPixel(x, y);
                dst.SetPixel(x, y, useWhiteBg ? CompositeOverWhite(pixel) : pixel);
            }
        }

        return dst;
    }

    private static Color GetDominantCornerColor(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var corners = new[]
        {
            bitmap.GetPixel(0, 0),
            bitmap.GetPixel(width - 1, 0),
            bitmap.GetPixel(0, height - 1),
            bitmap.GetPixel(width - 1, height - 1),
        };

        return corners
            .GroupBy(color => color.ToArgb())
            .OrderByDescending(group => group.Count())
            .Select(group => Color.FromArgb(group.Key))
            .First();
    }

    private static bool[] BuildEdgeConnectedBackgroundMask(Bitmap bitmap, Color backgroundColor)
    {
        const int tolerance = 12;

        var width = bitmap.Width;
        var height = bitmap.Height;
        var mask = new bool[width * height];
        var queue = new Queue<Point>();

        void EnqueueIfBackground(int x, int y)
        {
            var index = y * width + x;
            if (mask[index] || !IsSameBackground(bitmap.GetPixel(x, y), backgroundColor, tolerance))
            {
                return;
            }

            mask[index] = true;
            queue.Enqueue(new Point(x, y));
        }

        for (var x = 0; x < width; x++)
        {
            EnqueueIfBackground(x, 0);
            EnqueueIfBackground(x, height - 1);
        }

        for (var y = 1; y < height - 1; y++)
        {
            EnqueueIfBackground(0, y);
            EnqueueIfBackground(width - 1, y);
        }

        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            if (point.X > 0) EnqueueIfBackground(point.X - 1, point.Y);
            if (point.X < width - 1) EnqueueIfBackground(point.X + 1, point.Y);
            if (point.Y > 0) EnqueueIfBackground(point.X, point.Y - 1);
            if (point.Y < height - 1) EnqueueIfBackground(point.X, point.Y + 1);
        }

        return mask;
    }

    private static bool IsSameBackground(Color pixel, Color backgroundColor, int tolerance)
    {
        if (pixel.A == 0 && backgroundColor.A == 0)
        {
            return true;
        }

        return Math.Abs(pixel.A - backgroundColor.A) <= tolerance
            && Math.Abs(pixel.R - backgroundColor.R) <= tolerance
            && Math.Abs(pixel.G - backgroundColor.G) <= tolerance
            && Math.Abs(pixel.B - backgroundColor.B) <= tolerance;
    }

    private static Color CompositeOverWhite(Color pixel)
    {
        if (pixel.A == 255)
        {
            return Color.FromArgb(pixel.R, pixel.G, pixel.B);
        }

        var alpha = pixel.A / 255d;
        var red = (int)Math.Round(pixel.R * alpha + 255 * (1 - alpha));
        var green = (int)Math.Round(pixel.G * alpha + 255 * (1 - alpha));
        var blue = (int)Math.Round(pixel.B * alpha + 255 * (1 - alpha));
        return Color.FromArgb(red, green, blue);
    }

    // ========================================================================
    //  直方图渲染（256 通道）
    // ========================================================================

    private static Bitmap RenderHistogram(byte[] data)
    {
        const int barWidth = 2;
        const int height = 300;
        const int width = 256 * barWidth;
        const int margin = 4;

        var maxVal = data.Max();
        if (maxVal == 0) maxVal = 1;

        var bmp = new Bitmap(width + margin * 2, height + margin * 2, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);

        var plotHeight = height - margin;
        for (var i = 0; i < 256; i++)
        {
            var barH = (int)((long)data[i] * plotHeight / maxVal);
            if (barH <= 0 && data[i] > 0) barH = 1;
            using var brush = new SolidBrush(Color.SteelBlue);
            g.FillRectangle(brush, margin + i * barWidth, margin + plotHeight - barH, barWidth - 1, barH);
        }

        // 画基线
        using var pen = new Pen(Color.LightGray);
        g.DrawLine(pen, margin, margin + plotHeight, margin + 256 * barWidth, margin + plotHeight);

        return bmp;
    }

    // ========================================================================
    //  散点图色度索引渲染（256x256）
    // ========================================================================

    private static Bitmap RenderScattergram(byte[] data, int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var colorMap = new Color[]
        {
            Color.Black,       // 0: Background
            Color.Gray,        // 1: GHOST
            Color.Red,         // 2: RRBC
            Color.DodgerBlue,  // 3: NEUT
            Color.LimeGreen,   // 4: LYMPH
            Color.Orange,      // 5: MONO
            Color.DeepPink,    // 6: EOS
            Color.Purple,      // 7: BASO
        };

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var val = data[y * width + x];
                if (val == 0)
                {
                    bmp.SetPixel(x, y, Color.Transparent);
                    continue;
                }

                var colorType = val / 10;
                var brightness = val % 10;

                Color baseColor;
                if (colorType >= 0 && colorType < colorMap.Length)
                    baseColor = colorMap[colorType];
                else
                    baseColor = Color.White;

                // 亮度调整：0-9 映射到 0.3-1.0
                var factor = 0.3f + brightness * 0.07f;
                var r = Math.Min(255, (int)(baseColor.R * factor));
                var g = Math.Min(255, (int)(baseColor.G * factor));
                var b = Math.Min(255, (int)(baseColor.B * factor));
                bmp.SetPixel(x, y, Color.FromArgb(r, g, b));
            }
        }

        return bmp;
    }
}