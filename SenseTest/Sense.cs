using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class Sense
{
    private const string SENSE_HAT_FRAMEBUFFER_NAME = "RPi-Sense FB";

    private const int Rows = 8;
    private const int Columns = 8;
    private static readonly IEnumerable<int> rowRange = Enumerable.Range(0, Rows);
    private static readonly IEnumerable<int> columnRange = Enumerable.Range(0, Columns);

    public static string GetFrameBufferDevicePath()
    {
        bool IsSenseFrameBuffer(string file)
        {
            var nameFile = Path.Combine(file, "name");
            if (!File.Exists(nameFile))
            {
                return false;
            }

            var name = File.ReadAllText(nameFile).Trim();
            return name == SENSE_HAT_FRAMEBUFFER_NAME;
        }

        string GetDevice(string file)
        {
            var frameBufferDevice = Path.Combine("/dev", Path.GetFileName(file));
            return File.Exists(frameBufferDevice)
                ? frameBufferDevice
                : null;
        }

        return Directory
            .EnumerateFileSystemEntries("/sys/class/graphics/", "fb*")
            .Where(IsSenseFrameBuffer)
            .Select(GetDevice)
            .FirstOrDefault(p => p != null)
            ?? throw new Exception("Cannot detect Sense HAT device");
    }

    public static Sense Create()
    {
        return new Sense(GetFrameBufferDevicePath());
    }

    private readonly string frameBufferDevicePath;

    private Sense(string frameBufferDevicePath)
    {
        this.frameBufferDevicePath = frameBufferDevicePath;
    }

    public void SetPixels(LedPixels value)
    {
        byte[] Pack(Color color)
        {
            var r = (color.Red >> 3) & 0x1F;
            var g = (color.Green >> 2) & 0x3F;
            var b = (color.Blue >> 3) & 0x1F;
            var bits16 = (short)((r << 11) + (g << 5) + b);
            return BitConverter.GetBytes(bits16);
        }

        var content = GetColors(value)
            .SelectMany(Pack)
            .ToArray();

        File.WriteAllBytes(frameBufferDevicePath, content);
    }

    private static IEnumerable<Color> GetColors(LedPixels value)
    {
        var equalityComparer = EqualityComparer
            .Create((CellColor p) => new { p.Cell.Row, p.Cell.Column });

        var defaultColor = new Color(0, 0, 0);
        return value.Pixels
            .Where(p => rowRange.Contains(p.Cell.Row) && columnRange.Contains(p.Cell.Column))
            .Union(
                rowRange
                    .SelectMany(row => columnRange
                        .Select(column => new CellColor(new Cell(row, column), defaultColor))
                    ),
                equalityComparer
            )
            .OrderBy(p => p.Cell.Row)
            .ThenBy(p => p.Cell.Column)
            .Select(p => p.Color);
    }
}