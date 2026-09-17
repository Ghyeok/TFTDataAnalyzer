namespace TFTAnalyzer.Domain
{
    public sealed class LocalizedText
    {
        public static LocalizedText Empty { get; } = new LocalizedText("", "");

        public string KoKr { get; }
        public string EnUs { get; }
        public string DisplayValue => string.IsNullOrWhiteSpace(KoKr) ? EnUs : KoKr;
        public bool UsedEnglishFallback => string.IsNullOrWhiteSpace(KoKr) && !string.IsNullOrWhiteSpace(EnUs);

        public LocalizedText(string koKr, string enUs)
        {
            KoKr = koKr ?? "";
            EnUs = enUs ?? "";
        }
    }
}
