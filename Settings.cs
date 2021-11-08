namespace Vaetech.Runtime.Html.Compressor
{
    public static class Settings
    {
        //default settings
        public static bool RemoveComments = true;
        public static bool RemoveMultiSpaces = true;

        //optional settings
        public static bool RemoveIntertagSpaces = false;
        public static bool RemoveQuotes = false;
        public static bool CompressJavaScript = false;
        public static bool CompressCss = false;
        public static bool SimpleDoctype = false;
        public static bool RemoveScriptAttributes = false;
        public static bool RemoveStyleAttributes = false;
        public static bool RemoveLinkAttributes = false;
        public static bool RemoveFormAttributes = false;
        public static bool RemoveInputAttributes = false;
        public static bool SimpleBooleanAttributes = false;
        public static bool RemoveJavaScriptProtocol = false;
        public static bool RemoveHttpProtocol = false;
        public static bool RemoveHttpsProtocol = false;
        public static bool PreserveLineBreaks = false;
        public static string RemoveSurroundingSpaces = null;
    }
}
