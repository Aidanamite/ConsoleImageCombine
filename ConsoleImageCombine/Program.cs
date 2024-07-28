using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Drawing.Imaging;
using System.Reflection;

namespace ConsoleImageCombine
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
        start: var i = 0;
            Console.Write($"{Options.Join(x => $"{i++}: {x.Item1}\n","")} Selection: ");
            var c = Console.ReadKey();
            Console.WriteLine();
            Process(c.KeyChar);
            goto start;
        }
        static void Process(char pressed)
        {
            for (int i = 0; i < Options.Count; i++)
                if (i.ToString() == pressed.ToString())
                {
                    Console.WriteLine("Doing " + Options[i].Item1);
                    Options[i].Item2();
                    Console.WriteLine("Done");
                }
        }

        public static List<(string, Action)> Options = new List<(string, Action)>
        {
            ("black as transparency",() => {
                var d = new OpenFileDialog();
                d.Filter = "Images|*.png";
                if (d.ShowDialog() != DialogResult.OK)
                {
                    d.Dispose();
                    return;
                }
                var image = new Bitmap(d.FileName, false);
                image.MakeTransparent();
                for (int x = 0; x < image.Width; x++)
                    for (int y = 0; y < image.Height; y++)
                        image.SetPixel(x, y, Color.FromArgb(image.GetPixel(x, y).R, 255, 255, 255));
                var d2 = new SaveFileDialog();
                d2.Filter = d.Filter;
                if (d2.ShowDialog() == DialogResult.OK)
                    image.Save(d2.FileName);
                d.Dispose();
                image.Dispose();
                d2.Dispose();
            }),
            ("calculate normal map",() => {
                var d = new OpenFileDialog();
                d.Filter = "Images|*.png";
                d.Title = "Select image as lit from the top";
                if (d.ShowDialog() != DialogResult.OK)
                    return;
                var image1 = new Bitmap(d.FileName, false);
                d.Title = "Select image as lit from the right";
                if (d.ShowDialog() != DialogResult.OK)
                    return;
                var image2 = new Bitmap(d.FileName, false);
                for (int x = 0; x < image1.Width; x++)
                    for (int y = 0; y < image1.Height; y++)
                    {
                        var c1 = image1.GetPixel(x, y);
                        var c2 = image2.GetPixel(x, y);
                        image1.SetPixel(x, y, Color.FromArgb(c2.R, c1.R, (int)Math.Round((1 - Math.Abs((c2.R + c1.R) / 255.0 - 1)) * 127.5 + 127.5)));
                    }
                var d2 = new SaveFileDialog();
                d2.Filter = d.Filter;
                if (d2.ShowDialog() == DialogResult.OK)
                    image1.Save(d2.FileName);
                d.Dispose();
                image1.Dispose();
                image2.Dispose();
                d2.Dispose();
            }),
            ("split image channels",() => {
                var channels = "R,G,B,A".Split(',');
                var d = new OpenFileDialog();
                d.Filter = "Images|*.png";
                if (d.ShowDialog() != DialogResult.OK)
                {
                    d.Dispose();
                    return;
                }
                var image = new Bitmap(d.FileName, false);
                for (int i = 0; i < channels.Length; i++)
                {
                    var nImage = new Bitmap(image);
                    for (int x = 0; x < nImage.Width; x++)
                        for (int y = 0; y < nImage.Height; y++)
                        {
                            var color = nImage.GetPixel(x, y);
                            var v = color.GetProperty<Color,byte>(channels[i],~System.Reflection.BindingFlags.Default);
                            nImage.SetPixel(x, y, Color.FromArgb(255, v, v, v));
                        }
                    nImage.Save($"{Path.GetDirectoryName(d.FileName)}/{Path.GetFileNameWithoutExtension(d.FileName)}_{channels[i]}.png");
                    nImage.Dispose();
                }
                d.Dispose();
            }),
            ("merge image channels",() => {
                var channels = "R,G,B,A".Split(',');
                var images = new Bitmap[channels.Length];
                var d = new OpenFileDialog();
                d.Filter = "Images|*.png";
                for (int i = 0; i < channels.Length; i++)
                {
                    d.Title = $"Select {channels[i]} channel image";
                    if (d.ShowDialog() != DialogResult.OK)
                    {
                        d.Dispose();
                        foreach (var img in images)
                            img?.Dispose();
                        return;
                    }
                    images[i] = new Bitmap(d.FileName, false);
                    if (i > 0 && (images[0].Width != images[i].Width || images[0].Height != images[i].Height))
                    {
                        Console.WriteLine($"All images must have the same dimensions [Current={images[0].Width}x{images[0].Height},Selected={images[i].Width}x{images[i].Height}]");
                        d.Dispose();
                        foreach (var img in images)
                            img?.Dispose();
                        return;
                    }
                }
                var image = new Bitmap(images[0]);
                for (int x = 0; x < image.Width; x++)
                    for (int y = 0; y < image.Height; y++)
                    {
                        var pixel = new byte[channels.Length];
                        for (int i = 0; i < channels.Length; i++)
                            pixel[i] = images[i].GetPixel(x, y).Grayscale();
                        image.SetPixel(x, y, Color.FromArgb(pixel[3], pixel[0], pixel[1], pixel[2]));
                    }
                var d2 = new SaveFileDialog();
                d2.Filter = d.Filter;
                if (d2.ShowDialog() == DialogResult.OK)
                    image.Save(d2.FileName);
                d.Dispose();
                image.Dispose();
                foreach (var i in images)
                    i?.Dispose();
                d2.Dispose();
            }),
            ("convert standard normal map to red normal map",() => {
                var d = new OpenFileDialog();
                d.Filter = "Images|*.png";
                if (d.ShowDialog() != DialogResult.OK)
                {
                    d.Dispose();
                    return;
                }
                var image = new Bitmap(d.FileName, false);
                image.MakeTransparent();
                for (int x = 0; x < image.Width; x++)
                    for (int y = 0; y < image.Height; y++)
                    {
                        var c = image.GetPixel(x, y);
                        var g = (byte)Math.Round(((c.G / 127.5 - 1) * (c.G < 127.5 ? 0.48 : 0.24) + 0.76) * 255);
                        image.SetPixel(x, y, Color.FromArgb((byte)Math.Round(c.R * 0.92 + 10), 255, g, g));
                    }
                var d2 = new SaveFileDialog();
                d2.Filter = d.Filter;
                if (d2.ShowDialog() == DialogResult.OK)
                    image.Save(d2.FileName);
                d.Dispose();
                image.Dispose();
                d2.Dispose();
            }),
            ("convert red normal map to standard normal map",() => {
                var d = new OpenFileDialog();
                d.Filter = "Images|*.png";
                if (d.ShowDialog() != DialogResult.OK)
                {
                    d.Dispose();
                    return;
                }
                var image = new Bitmap(d.FileName, false);
                image.MakeTransparent();
                for (int x = 0; x < image.Width; x++)
                    for (int y = 0; y < image.Height; y++)
                    {
                        var c = image.GetPixel(x, y);
                        var gr = c.G / 255d;
                        var g = (byte)Math.Round(((gr - 0.76) / (gr < 0.76 ? 0.48 : 0.24) + 1) * 127.5);
                        image.SetPixel(x, y, Color.FromArgb(255, c.A, g, (byte)Math.Round(Math.Sqrt(1 - Math.Pow(g / 127.5d - 1, 2) - Math.Pow(c.A / 127.5d - 1, 2)) * 127.5 + 127.5)));
                    }
                var d2 = new SaveFileDialog();
                d2.Filter = d.Filter;
                if (d2.ShowDialog() == DialogResult.OK)
                    image.Save(d2.FileName);
                d.Dispose();
                image.Dispose();
                d2.Dispose();
            }),
            ("convert standard normal map to exported red normal map",() => {
                var d = new OpenFileDialog();
                d.Filter = "Images|*.png";
                if (d.ShowDialog() != DialogResult.OK)
                {
                    d.Dispose();
                    return;
                }
                var image = new Bitmap(d.FileName, false);
                image.MakeTransparent();
                for (int x = 0; x < image.Width; x++)
                    for (int y = 0; y < image.Height; y++)
                    {
                        var c = image.GetPixel(x, y);
                        image.SetPixel(x, y, Color.FromArgb(c.R, 255, c.G, c.G));
                    }
                var d2 = new SaveFileDialog();
                d2.Filter = d.Filter;
                if (d2.ShowDialog() == DialogResult.OK)
                    image.Save(d2.FileName);
                d.Dispose();
                image.Dispose();
                d2.Dispose();
            }),
            ("convert exported red normal map to standard normal map",() => {
                var d = new OpenFileDialog();
                d.Filter = "Images|*.png";
                if (d.ShowDialog() != DialogResult.OK)
                {
                    d.Dispose();
                    return;
                }
                var image = new Bitmap(d.FileName, false);
                image.MakeTransparent();
                for (int x = 0; x < image.Width; x++)
                    for (int y = 0; y < image.Height; y++)
                    {
                        var c = image.GetPixel(x, y);
                        image.SetPixel(x, y, Color.FromArgb(255, c.A, c.G, (byte)Math.Round(Math.Sqrt(1 - Math.Pow(c.G / 127.5d - 1, 2) - Math.Pow(c.A / 127.5d - 1, 2)) * 127.5 + 127.5)));
                    }
                var d2 = new SaveFileDialog();
                d2.Filter = d.Filter;
                if (d2.ShowDialog() == DialogResult.OK)
                    image.Save(d2.FileName);
                d.Dispose();
                image.Dispose();
                d2.Dispose();
            })/*,
            ("split creature data",(Action)(() => { // wip image data reading
                var d = new OpenFileDialog();
                d.Filter = "Images|*.png";
                if (d.ShowDialog() != DialogResult.OK)
                {
                    d.Dispose();
                    return;
                }
                var image = new Bitmap(d.FileName, false);
                var data = new PropertyList();
                foreach (var p in image.PropertyItems)
                    data.metadata.Add(new PropertyList.Property()
                    {
                        Type = p.Type,
                        Id = p.Id,
                        Data = p.Value
                    });
                foreach (var p in image.GetEncoderParameterList().Param)
                {
                    var len = p.ValueType == EncoderParameterValueType.ValueTypeByte;
                    new Encoder();
                }
                var d2 = new SaveFileDialog();
                d2.Filter = "JSON|*.json";
                if (d2.ShowDialog() == DialogResult.OK)
                    using (var file = File.OpenWrite(d2.FileName))
                        new DataContractJsonSerializer(typeof(PropertyList)).WriteObject(file, data);
                d.Dispose();
                image.Dispose();
                d2.Dispose();
            }))*/
        };
    }

    public class PropertyList
    {
        public List<Property> metadata = new List<Property>();
        public class Property
        {
            public int Id;
            public short Type;
            public byte[] Data;
        }
        public class Parameter
        {
            public EncoderParameterValueType Type;
            public byte[] Data;
        }
    }

    public static class ExtentionMethods
    {
        public static Y GetProperty<X,Y>(this X obj, string propertyName, System.Reflection.BindingFlags flags = default)
        {
            var t = typeof(X);
            var o = typeof(object);
            while (t != o)
            {
                var p = t.GetProperty(propertyName, flags);
                if (p != null)
                    return (Y)p.GetValue(obj);
                t = t.BaseType;
            }
            return default;
        }
        static FieldInfo _parameterValue = typeof(EncoderParameter).GetField("parameterValue", ~BindingFlags.Default);
        public static IntPtr GetValueHandle(this EncoderParameter parameter) => (IntPtr)_parameterValue.GetValue(parameter);
        public static byte Grayscale(this Color color) => (byte)((color.R + color.G + color.B) / 3);
        public static string Join<T>(this IEnumerable<T> c, Func<T, string> func = null, string delimeter = ", ")
        {
            if (func == null)
                func = x => x?.ToString() ?? "";
            var flag = true;
            var s = new StringBuilder();
            foreach (var i in c)
            {
                if (flag)
                    flag = false;
                else
                    s.Append(delimeter);
                s.Append(func(i));
            }
            return s.ToString();
        }
    }
}
