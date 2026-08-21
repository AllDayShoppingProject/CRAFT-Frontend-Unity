public static class ProjectConfig
{
    public const string API_BASE_URL = "https://craft-h1i6.onrender.com/v1";

    /// 주소가 자리표시자면 호출을 막는다. 시연 중 타임아웃 대기와 에러 도배를 막는 장치.
    public static bool IsApiConfigured =>
        !string.IsNullOrEmpty(API_BASE_URL) && !API_BASE_URL.Contains("<");

    /// 입력한 키는 이 중 가장 가까운 값으로 매칭되므로, 실제 3D 더미 모델이 있는 신장만 넣어야 한다.
    /// 모델 없는 값을 넣으면 존재하지 않는 아바타를 요구하게 된다 (명세서 dummy_height_options).
    public static readonly int[] AllowedHeights =
    {
        150, 160, 170, 180, 190
    };
}
