namespace LightTranslate
{
    internal sealed class TranslationResult
    {
        internal string Text { get; private set; }
        internal string Source { get; private set; }
        internal string Target { get; private set; }

        internal TranslationResult(string text, string source, string target)
        {
            Text = text;
            Source = source;
            Target = target;
        }
    }
}
