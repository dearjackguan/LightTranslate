namespace LightTranslate
{
    internal sealed class OcrRecognition
    {
        internal string Text { get; private set; }
        internal bool NeedsReview { get; private set; }
        internal OcrRecognition(string text, bool needsReview)
        {
            Text = text;
            NeedsReview = needsReview;
        }
    }
}
