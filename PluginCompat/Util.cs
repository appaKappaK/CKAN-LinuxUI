namespace CKAN.GUI
{
    public static class Util
    {
        public static bool TryOpenWebPage(string url)
            => Utilities.ProcessStartURL(url);
    }
}
