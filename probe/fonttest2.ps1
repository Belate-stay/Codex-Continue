Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Text;

public static class FontProbe2 {
  public static void Render(string outPath) {
    var pfc = new PrivateFontCollection();
    pfc.AddFontFile(@"D:\Desktop\HYSongYunLangHeiW-1.ttf");
    var fam = pfc.Families[0];
    Console.WriteLine("using family index 0, name length=" + fam.Name.Length);

    var bmp = new Bitmap(1200, 700);
    var g = Graphics.FromImage(bmp);
    g.Clear(Color.White);
    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

    var f72 = new Font(fam, 72);
    var f30 = new Font(fam, 30);
    g.DrawString("58%   19:34   82", f72, Brushes.Black, 20, 20);
    g.DrawString("Codex-Continue  10/27", f30, Brushes.Black, 20, 170);
    g.DrawString("HY SongYun display font", f30, Brushes.SteelBlue, 20, 230);

    // compare: what the app screenshots show vs this
    g.DrawString("19:34", new Font(fam, 44), Brushes.Black, 20, 320);
    g.DrawString("19:34", new Font("Microsoft YaHei UI", 44), Brushes.Gray, 300, 320);
    g.DrawString("19:34", new Font("Segoe UI", 44), Brushes.LightGray, 580, 320);

    g.DrawString("58%", new Font(fam, 84), Brushes.Black, 20, 420);
    g.DrawString("58%", new Font("Microsoft YaHei UI", 84), Brushes.Gray, 420, 420);

    g.Dispose();
    bmp.Save(outPath);
  }
}
'@
[FontProbe2]::Render('D:\Desktop\Codex-Continue\probe\fonttest2.png')
Write-Output 'saved fonttest2.png'
