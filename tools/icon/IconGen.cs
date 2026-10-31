// Renders the No way? Yahweh! icon — a cross breaking a succubus tail — from vector paths
// into every size the project needs. Single source of truth for the artwork.
//   dotnet run tools/icon/IconGen.cs
#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

var root = Path.GetFullPath(Path.Combine(AppContext.GetData("EntryPointFileDirectoryPath") as string ?? ".", "..", ".."));

// 256×256 artboard.
var background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0C, 0x0E));
var tailColor = new SolidColorBrush(Color.FromRgb(0x7F, 0xA8, 0xD9)); // soft blue accent
var crossColor = new SolidColorBrush(Color.FromRgb(0xF2, 0xF3, 0xF5));
background.Freeze(); tailColor.Freeze(); crossColor.Freeze(); // used on the STA render thread

// The tail curls in from the left and is cut by the cross: the piece after the cross falls away,
// drooping, with a gap on both sides of the vertical bar.
const string TailLower = "M 40 214 C 24 188 40 158 66 160 C 84 162 92 176 104 172";
const string TailUpper = "M 152 168 C 168 172 180 182 186 196";
// Heart-shaped spade tip (the classic succubus-tail point), aligned with the falling piece
// (computed along the tail direction: tip forward, two rounded lobes at the base).
const string Spade = "M 187.6 199.7 C 186.4 189.3 200.3 181.2 206.0 191.8 C 210.8 200.6 205.4 216.0 200.2 229.1 C 187.1 223.8 172.2 217.1 169.2 207.6 C 165.4 196.1 180.9 191.7 187.6 199.7 Z";
// Upright cross, drawn on top.
const string CrossV = "M 116 30 H 140 V 226 H 116 Z";
const string CrossH = "M 72 76 H 184 V 100 H 72 Z";

DrawingGroup Artwork()
{
    var g = new DrawingGroup();
    var tailPen = new Pen(tailColor, 15) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
    g.Children.Add(new GeometryDrawing(background, null, new RectangleGeometry(new Rect(0, 0, 256, 256), 56, 56)));
    g.Children.Add(new GeometryDrawing(null, tailPen, Geometry.Parse(TailLower)));
    g.Children.Add(new GeometryDrawing(null, tailPen, Geometry.Parse(TailUpper)));
    g.Children.Add(new GeometryDrawing(tailColor, new Pen(tailColor, 4) { LineJoin = PenLineJoin.Round }, Geometry.Parse(Spade)));
    // Union, so the overlap of the two bars is filled (a GeometryGroup would leave a hole).
    var cross = new CombinedGeometry(GeometryCombineMode.Union, Geometry.Parse(CrossV), Geometry.Parse(CrossH));
    // A thin ring of background around the cross makes the break in the tail read clearly.
    g.Children.Add(new GeometryDrawing(crossColor, new Pen(background, 10) { LineJoin = PenLineJoin.Round }, cross));
    g.Children.Add(new GeometryDrawing(crossColor, null, cross));
    return g;
}

byte[] Png(int size)
{
    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        dc.PushTransform(new ScaleTransform(size / 256.0, size / 256.0));
        dc.DrawDrawing(Artwork());
    }
    var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var ms = new MemoryStream();
    encoder.Save(ms);
    return ms.ToArray();
}

// ICO with PNG-compressed entries (supported since Windows Vista).
void Ico(string path, int[] sizes)
{
    var images = sizes.Select(Png).ToArray();
    using var w = new BinaryWriter(File.Create(path));
    w.Write((short)0); w.Write((short)1); w.Write((short)images.Length);
    var offset = 6 + 16 * images.Length;
    for (var i = 0; i < images.Length; i++)
    {
        w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
        w.Write(images[i].Length); w.Write(offset);
        offset += images[i].Length;
    }
    foreach (var image in images) w.Write(image);
}

void Run()
{
    var icons = Path.Combine(root, "extension", "icons");
    Directory.CreateDirectory(icons);
    foreach (var size in new[] { 16, 32, 48, 128 }) File.WriteAllBytes(Path.Combine(icons, $"icon-{size}.png"), Png(size));
    Ico(Path.Combine(root, "src", "NoWayYahweh.App", "app.ico"), [16, 24, 32, 48, 64, 128, 256]);
    File.WriteAllBytes(Path.Combine(root, "docs", "icon-512.png"), Png(512));
    Console.WriteLine("Ícones gerados em extension/icons, src/NoWayYahweh.App/app.ico e docs/icon-512.png");
}

var thread = new Thread(Run);
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();
