using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace IMerge3
{
    class PixelData
    {
        class Pixel
        {
            public byte r;
            public byte g;
            public byte b;
        }

        List<Pixel> _pixels;

        public int Width { get { return _pixels.Count; } }

        public PixelData()
        {
            _pixels = new List<Pixel>();
        }

        public void Add(PixelData other)
        {
            _pixels.AddRange(other._pixels);
        }

        public void AddColor(byte red, byte green, byte blue)
        {
            _pixels.Add(new Pixel() { r = red, g = green, b = blue });
        }

        public void Write(BinaryWriter bw)
        {
            foreach (Pixel p in _pixels)
            {
                bw.Write(p.r);
                bw.Write(p.g);
                bw.Write(p.b);
            }
        }
    }
}
