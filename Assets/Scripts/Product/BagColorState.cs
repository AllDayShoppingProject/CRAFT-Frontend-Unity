using System.Collections.Generic;
using UnityEngine;

/// 가방의 현재 컬러를 한 곳에 모아두는 세션 단위 보관소. 키는 정수 product_id다.
/// 화면마다 따로 기억하면 어긋난다 (BagViewUI가 열릴 때마다 첫 컬러로 되돌려 선택이 날아갔다).
/// static이라 플레이를 멈추면 사라진다 = 세션 종료 시 초기화.
public static class BagColorState {

    private static readonly Dictionary<int, string> selected = new Dictionary<int, string>();

    /// 현재 컬러 코드. 아직 정해지지 않았으면 null.
    public static string GetColor(int productId) {
        return selected.TryGetValue(productId, out string color) ? color : null;
    }

    public static void SetColor(int productId, string color) {
        if (productId <= 0 || string.IsNullOrEmpty(color)) return;

        selected[productId] = color;
    }

    /// 현재 컬러. 없으면 지원 컬러 중 하나를 무작위로 뽑아 확정한다 (세션 내내 유지된다).
    public static ProductColorOption ResolveOrAssignRandom(ProductData product) {
        if (product == null || product.colors == null || product.colors.Count == 0) return null;

        ProductColorOption stored = Find(product, GetColor(product.product_id));
        if (stored != null) return stored;

        List<ProductColorOption> candidates = new List<ProductColorOption>(product.colors.Count);
        foreach (ProductColorOption option in product.colors) {
            if (option != null && option.model_supported) candidates.Add(option);
        }

        // 모델 지원 컬러가 하나도 없으면 목록 전체에서 고른다
        if (candidates.Count == 0) candidates.AddRange(product.colors);
        if (candidates.Count == 0) return null;

        ProductColorOption picked = candidates[Random.Range(0, candidates.Count)];
        SetColor(product.product_id, picked.color);

        return picked;
    }

    /// 제품마다 컬러 객체가 복사본이라 객체 비교가 아닌 코드로 찾아야 한다.
    public static ProductColorOption Find(ProductData product, string color) {
        if (product == null || product.colors == null || string.IsNullOrEmpty(color)) return null;

        foreach (ProductColorOption option in product.colors) {
            if (option != null && option.color == color) return option;
        }
        return null;
    }

    /// 같은 실행 안에서 새 세션을 시작할 때 호출
    public static void Clear() {
        selected.Clear();
    }
}
