using System.Drawing;

namespace Snap.Models;

public class BlurBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Radius { get; set; } = 12;

    public Rectangle ToRectangle() => new(X, Y, Width, Height);
}
