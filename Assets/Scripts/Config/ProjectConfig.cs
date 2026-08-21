public static class ProjectConfig
{
    public static readonly string[] API_BASE_URLS =
    {
        "https://craft-h1i6.onrender.com/v1",
        "https://likelioncentralhackathon-production.up.railway.app/v1"
    };

    private static string activeApiBaseUrl;

    public static string API_BASE_URL =>
        activeApiBaseUrl ?? API_BASE_URLS[0];

    public static bool IsApiConfigured =>
        !string.IsNullOrEmpty(API_BASE_URL) && !API_BASE_URL.Contains("<");

    public static void SetActiveApiBaseUrl(string url)
    {
        activeApiBaseUrl = url;
    }

    public static string[] GetApiBaseUrls()
    {
        return API_BASE_URLS;
    }

    public static readonly int[] AllowedHeights =
    {
        150, 160, 170, 180, 190
    };
}