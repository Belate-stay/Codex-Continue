Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Text;

public static class FontProbe {
  public static void Render(string outPath) {
    var pfc = new PrivateFontCollection();
    pfc.AddFontFile(@"D:\Desktop\HYSongYunLangHeiW-1.ttf");
    pfc.AddFontFile(@"D:\Desktop\NotoSansCJK-DemiLight-4.otf");
    foreach (var f in pfc.Families) Console.WriteLine("family: [" + f.Name + "]");

    var bmp = new Bitmap(1200, 620);
    var g = Graphics.FromImage(bmp);
    g.Clear(Color.White);
    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

    Font song = null, noto = null;
    foreach (var f in pfc.Families) {
      if (f.Name.Contains("Song") || f.Name.Contains("HY")) song = new Font(f, 72);
      else noto = new Font(f, 24);
    }
    string t1 = "58%   19:34   82";
    string t2 = "Codex-Continue " +
        "状" + "态检测 " + "10月27日";
    if (song != null) {
      g.DrawString(t1, song, Brushes.Black, 20, 20);
      g.DrawString(t2, new Font(song.FontFamily, 30), Brushes.Black, 20, 170);
    } else {
      g.DrawString("(song font missing)", new Font("Arial", 24), Brushes.Red, 20, 20);
    }
    if (noto != null) {
      g.DrawString(t1, new Font(noto.FontFamily, 72), Brushes.DimGray, 20, 260);
      g.DrawString("Noto body: " + "恢复时间 10月27日 23:53 在线", noto, Brushes.DimGray, 20, 420);
    }
    g.DrawString("YaHei: " + "状态检测 重新检测 运行 五小时窗口额度", new Font("Microsoft YaHei UI", 24), Brushes.Gray, 20, 500);
    g.Dispose();
    bmp.Save(outPath);
  }
}
'@
[FontProbe]::Render('D:\Desktop\Codex-Continue\probe\fonttest.png')
Write-Output 'saved fonttest.png'
