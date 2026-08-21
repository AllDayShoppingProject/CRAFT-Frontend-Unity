using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// 매장 화면의 톤 매핑을 켠다.
/// 없으면 밝기 1.0을 넘는 하이라이트가 흰색으로 잘려 색이 사라진다 (조명을 낮춰도 마찬가지).
public static class GalleryGrading {

    private const string ROOT_NAME = "PostProcessing";

    /// 카메라의 포스트 프로세싱을 켜고 전역 볼륨을 하나 세운다.
    public static Volume Apply(Camera camera, float exposure, float contrast, float saturation) {
        if (camera == null) return null;

        // 카메라 쪽 스위치를 켜야 볼륨이 실제로 적용된다
        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.Medium;

        GameObject existing = GameObject.Find(ROOT_NAME);
        if (existing != null) Object.Destroy(existing);

        GameObject root = new GameObject(ROOT_NAME);
        root.layer = 0;   // 카메라의 Volume Mask가 Default 레이어만 본다

        Volume volume = root.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.profile = BuildProfile(exposure, contrast, saturation);

        return volume;
    }

    private static VolumeProfile BuildProfile(float exposure, float contrast, float saturation) {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "GalleryGrading";

        // Neutral은 색을 건드리지 않고 밝기만 눌러 담는다 (ACES는 쇼룸에서 색이 왜곡된다).
        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.Neutral;

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.overrideState = true;
        color.postExposure.value = exposure;
        color.contrast.overrideState = true;
        color.contrast.value = contrast;
        color.saturation.overrideState = true;
        color.saturation.value = saturation;

        // 조명 기구와 하이라이트에만 아주 옅게. 값을 올리면 다시 뿌옇게 쨍해진다.
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.15f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.2f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.6f;

        return profile;
    }
}
