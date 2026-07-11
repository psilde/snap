using System.Collections.Generic;
using System.Drawing;
using Snap.Models;

namespace Snap.Services;

public static class CaptureComposer
{
    public static Bitmap Compose(Bitmap captureRegion, IEnumerable<BlurBox> blurBoxes)
    {
        var current = new Bitmap(captureRegion);

        foreach (var box in blurBoxes)
        {
            var next = BlurService.ApplyBlur(current, box.ToRectangle(), box.Radius);
            current.Dispose();
            current = next;
        }

        return current;
    }
}
