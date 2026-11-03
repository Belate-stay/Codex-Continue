using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace CodexContinue;

static class ReleaseNotes
{
    public static FlowDocument Render(string markdown, Action<Uri> openLink)
    {
        var document = new FlowDocument { PagePadding = new Thickness(0), FontSize = 14, LineHeight = 25,
            FontFamily = (FontFamily)Application.Current.FindResource("BodyFont") };
        document.SetResourceReference(TextElement.ForegroundProperty, "TextPrimaryBrush");
        var code = new StringBuilder();
        bool fenced = false;
        foreach (string line in markdown.Replace("\r", "").Split('\n'))
        {
            if (line.TrimStart().StartsWith("```"))
            {
                if (fenced) { AddCode(); fenced = false; } else fenced = true;
                continue;
            }
            if (fenced) { code.AppendLine(line); continue; }
            string text = line.Trim();
            if (text.Length == 0) continue;
            var heading = Regex.Match(text, @"^(#{1,6})\s+(.+)$");
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 12) };
            if (heading.Success)
            {
                paragraph.FontSize = heading.Groups[1].Length <= 2 ? 19 : 16;
                paragraph.FontWeight = FontWeights.SemiBold;
                paragraph.Margin = new Thickness(0, document.Blocks.Count == 0 ? 0 : 14, 0, 10);
                text = heading.Groups[2].Value;
            }
            else if (Regex.IsMatch(text, @"^[-*+]\s+")) text = "•  " + Regex.Replace(text, @"^[-*+]\s+", "");
            int position = 0;
            foreach (Match item in Regex.Matches(text, @"\*\*(.+?)\*\*|`([^`]+)`|(?<!!)\[([^\]]+)\]\((https?://[^\s)]+)\)"))
            {
                paragraph.Inlines.Add(new Run(text[position..item.Index]));
                if (item.Groups[1].Success) paragraph.Inlines.Add(new Bold(new Run(item.Groups[1].Value)));
                else if (item.Groups[2].Success) paragraph.Inlines.Add(new Run(item.Groups[2].Value) { FontFamily = new FontFamily("Consolas"), FontSize = 13 });
                else if (Uri.TryCreate(item.Groups[4].Value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                {
                    var link = new Hyperlink(new Run(item.Groups[3].Value)) { NavigateUri = uri };
                    link.SetResourceReference(TextElement.ForegroundProperty, "AccentInkBrush");
                    link.RequestNavigate += (_, e) => { openLink(e.Uri); e.Handled = true; };
                    paragraph.Inlines.Add(link);
                }
                position = item.Index + item.Length;
            }
            paragraph.Inlines.Add(new Run(text[position..]));
            document.Blocks.Add(paragraph);
        }
        if (code.Length > 0) AddCode();
        return document;

        void AddCode()
        {
            var block = new Paragraph(new Run(code.ToString().TrimEnd())) { FontFamily = new FontFamily("Consolas"), FontSize = 13,
                LineHeight = 22, Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 4, 0, 16) };
            block.SetResourceReference(TextElement.BackgroundProperty, "CanvasBrush");
            document.Blocks.Add(block); code.Clear();
        }
    }
}
