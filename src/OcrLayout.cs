using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace LightTranslate
{
    internal static class OcrLayout
    {
        private sealed class TextBox
        {
            internal string Text;
            internal RectangleF Bounds;
            internal float FontHeight;
        }

        private sealed class Paragraph
        {
            internal readonly List<TextBox> Lines = new List<TextBox>();
            internal RectangleF Bounds;
            internal TextBox Last { get { return Lines[Lines.Count - 1]; } }
            internal void Add(TextBox line)
            {
                Bounds = Lines.Count == 0 ? line.Bounds : RectangleF.Union(Bounds, line.Bounds);
                Lines.Add(line);
            }
        }

        internal static string Reconstruct(Dictionary<string, object> result)
        {
            string original = result.ContainsKey("text") ? result["text"] as string : null;
            if (string.IsNullOrWhiteSpace(original)) return original;
            object[] lines = result.ContainsKey("lines") ? result["lines"] as object[] : null;
            if (lines == null || lines.Length == 0 || IsRotated(result)) return Normalize(original);
            var fragments = ReadFragments(lines);
            if (fragments == null || fragments.Count == 0) return Normalize(original);
            var paragraphs = GroupParagraphs(fragments);
            return string.Join("\n", ReadingOrder(paragraphs).Select(p => JoinLines(p.Lines)));
        }

        private static bool IsRotated(Dictionary<string, object> result)
        {
            if (!result.ContainsKey("angle") || result["angle"] == null) return false;
            return Math.Abs(Convert.ToDouble(result["angle"], CultureInfo.InvariantCulture)) > 3;
        }

        private static List<TextBox> ReadFragments(object[] lines)
        {
            var fragments = new List<TextBox>();
            foreach (var item in lines)
            {
                var line = item as Dictionary<string, object>;
                if (line == null || !line.ContainsKey("text") || !line.ContainsKey("words")) return null;
                string text = line["text"] as string;
                if (string.IsNullOrWhiteSpace(text)) continue;
                var words = ReadWords(line["words"] as object[]);
                if (words == null || words.Count == 0) return null;
                var pieces = SplitLine(words);
                fragments.AddRange(pieces);
            }
            return fragments;
        }

        private static List<TextBox> ReadWords(object[] items)
        {
            if (items == null) return null;
            var words = new List<TextBox>();
            foreach (var item in items)
            {
                var word = item as Dictionary<string, object>;
                if (word == null || !word.ContainsKey("text")) return null;
                string text = word["text"] as string;
                if (string.IsNullOrWhiteSpace(text)) return null;
                float x, y, width, height;
                if (!Number(word, "x", out x) || !Number(word, "y", out y)
                    || !Number(word, "width", out width) || !Number(word, "height", out height)
                    || width <= 0 || height <= 0) return null;
                words.Add(new TextBox { Text = text, Bounds = new RectangleF(x, y, width, height), FontHeight = height });
            }
            return words.OrderBy(w => w.Bounds.Left).ToList();
        }

        private static bool Number(Dictionary<string, object> value, string key, out float number)
        {
            number = 0;
            object raw;
            if (!value.TryGetValue(key, out raw) || raw == null) return false;
            return float.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Float,
                CultureInfo.InvariantCulture, out number) && !float.IsNaN(number) && !float.IsInfinity(number);
        }

        private static List<TextBox> SplitLine(List<TextBox> words)
        {
            var pieces = new List<TextBox>();
            float height = Median(words.Select(w => w.Bounds.Height));
            var gaps = words.Skip(1).Select((word, index) => word.Bounds.Left - words[index].Bounds.Right)
                .Where(gap => gap > 0).ToArray();
            // Estimate spaces from ordinary gaps, excluding gutters between columns.
            float[] normalGaps = gaps.Where(gap => gap < height).ToArray();
            float spacing = normalGaps.Length >= 3 ? Median(normalGaps) : 0;
            float threshold = Math.Max(2 * height, 5 * spacing);
            TextBox current = null;
            TextBox previous = null;
            var heights = new List<float>();
            foreach (var word in words)
            {
                if (current == null || word.Bounds.Left - previous.Bounds.Right >= threshold)
                {
                    current = new TextBox { Text = word.Text, Bounds = word.Bounds };
                    pieces.Add(current);
                    heights.Clear();
                }
                else
                {
                    current.Text = AppendWord(current.Text, word.Text, word.Bounds.Left - previous.Bounds.Right, spacing, height);
                    current.Bounds = RectangleF.Union(current.Bounds, word.Bounds);
                }
                previous = word;
                heights.Add(word.FontHeight);
                current.FontHeight = Median(heights);
            }
            return pieces;
        }

        private static List<Paragraph> GroupParagraphs(List<TextBox> fragments)
        {
            var paragraphs = new List<Paragraph>();
            foreach (var line in fragments.OrderBy(l => l.Bounds.Top).ThenBy(l => l.Bounds.Left))
            {
                var paragraph = paragraphs.Where(p => Continues(p, line))
                    .OrderBy(p => Math.Abs(line.Bounds.Top - p.Last.Bounds.Bottom))
                    .ThenBy(p => Math.Abs(line.Bounds.Left - p.Last.Bounds.Left)).FirstOrDefault();
                if (paragraph == null)
                {
                    paragraph = new Paragraph();
                    paragraphs.Add(paragraph);
                }
                paragraph.Add(line);
            }
            return paragraphs;
        }

        private static bool Continues(Paragraph paragraph, TextBox next)
        {
            var previous = paragraph.Last;
            float height = Math.Min(previous.FontHeight, next.FontHeight);
            float gap = next.Bounds.Top - previous.Bounds.Bottom;
            if (gap < -0.15f * height || gap > 0.65f * height) return false;
            if (Math.Max(previous.FontHeight, next.FontHeight) > 1.3f * height) return false;
            if (IsListStart(next.Text) || previous.Text.EndsWith(":") || previous.Text.EndsWith("：")) return false;
            if (!Aligned(paragraph, next, height)) return false;
            // A short completed line usually ends a paragraph or heading.
            if (previous.Bounds.Width < 0.6f * next.Bounds.Width) return false;
            return true;
        }

        private static bool Aligned(Paragraph paragraph, TextBox next, float height)
        {
            var previous = paragraph.Last;
            float offset = next.Bounds.Left - previous.Bounds.Left;
            if (Math.Abs(offset) <= 1.25f * height) return true;
            if (Math.Abs(previous.Bounds.Right - next.Bounds.Right) > 1.25f * height) return false;
            if (offset < 0 && paragraph.Lines.Count == 1 && !IsListStart(previous.Text))
                return -offset <= 2.5f * height;
            return offset > 0 && IsListStart(paragraph.Lines[0].Text) && offset <= 3 * height;
        }

        private static bool IsListStart(string text)
        {
            return Regex.IsMatch(text, @"^(?:[•●▪■\-*]|\d+[.)、]|[（(]?\d+[）)]|[一二三四五六七八九十]+[、.)])(?:\s|(?=[\u4e00-\u9fff]))");
        }

        private static IEnumerable<Paragraph> ReadingOrder(List<Paragraph> paragraphs)
        {
            if (paragraphs.Count < 2) return paragraphs;
            float height = Median(paragraphs.Select(p => p.Lines[0].FontHeight));
            List<Paragraph> first, second;
            if (Cut(paragraphs, true, 1.25f * height, out first, out second)
                || Cut(paragraphs, false, 0.2f * height, out first, out second))
                return ReadingOrder(first).Concat(ReadingOrder(second));
            return paragraphs.OrderBy(p => p.Bounds.Top).ThenBy(p => p.Bounds.Left);
        }

        private static bool Cut(List<Paragraph> paragraphs, bool vertical, float minimum,
            out List<Paragraph> first, out List<Paragraph> second)
        {
            first = second = null;
            var sorted = paragraphs.OrderBy(p => vertical ? p.Bounds.Left : p.Bounds.Top).ToList();
            float end = vertical ? sorted[0].Bounds.Right : sorted[0].Bounds.Bottom;
            float largest = minimum, midpoint = 0;
            foreach (var paragraph in sorted.Skip(1))
            {
                float start = vertical ? paragraph.Bounds.Left : paragraph.Bounds.Top;
                if (start - end > largest) { largest = start - end; midpoint = (start + end) / 2; }
                end = Math.Max(end, vertical ? paragraph.Bounds.Right : paragraph.Bounds.Bottom);
            }
            if (midpoint == 0) return false;
            first = sorted.Where(p => (vertical ? p.Bounds.Right : p.Bounds.Bottom) <= midpoint).ToList();
            second = sorted.Except(first).ToList();
            return first.Count > 0 && second.Count > 0;
        }

        private static string JoinLines(List<TextBox> lines)
        {
            return lines.Select(line => Normalize(line.Text)).Aggregate(Append);
        }

        private static string Append(string left, string right)
        {
            left = Normalize(left);
            right = Normalize(right);
            bool chinese = Regex.IsMatch(left, @"[\u4e00-\u9fff，。！？；：、）】”’]$")
                && Regex.IsMatch(right, @"^[\u4e00-\u9fff（【“‘]");
            return left + (chinese ? "" : " ") + right;
        }

        private static string AppendWord(string left, string right, float gap, float spacing, float height)
        {
            bool letters = Regex.IsMatch(left.Split(' ').Last(), @"^[A-Za-z]+$")
                && Regex.IsMatch(right, @"^[a-z]+$");
            if (letters && gap >= 0 && gap < Math.Min(0.4f * spacing, 0.2f * height)) return left + right;
            if (Regex.IsMatch(right, @"^[,.;:!?%\)\]}，。！？；：、）】]")) return left + right;
            return Append(left, right);
        }

        private static string Normalize(string text)
        {
            return Regex.Replace(text.Trim(), @"(?<=[\p{IsCJKUnifiedIdeographs}]) +(?=[\p{IsCJKUnifiedIdeographs}])", "");
        }

        private static float Median(IEnumerable<float> numbers)
        {
            float[] sorted = numbers.OrderBy(n => n).ToArray();
            return (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
        }
    }
}
