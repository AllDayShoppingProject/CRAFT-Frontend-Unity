using System;
using System.Collections.Generic;

/// GET /v1/products 응답의 colors[] 항목. 서버가 주는 건 color / model_supported 둘뿐이다.
[Serializable]
public class ProductColorOption {

    /// 서버·이벤트 로그가 쓰는 컬러 식별자. 에셋 폴더 접미사(p1_black)와 같은 값이다.
    public string color;

    /// 3D 모델이 없는 컬러는 false → 스와치를 회색 비활성으로 표시
    public bool model_supported = true;

    // 아래 둘은 서버 응답에 없고 클라이언트가 채우는 표시용 값 (ProductCatalog.Enrich)
    [NonSerialized] public string displayName;
    [NonSerialized] public string hex;
}

/// GET /v1/products 응답 1건
[Serializable]
public class ProductData {

    /// 서버 product_id는 정수다. 문자열로 다루면 비교가 어긋난다.
    public int product_id;

    public string name;
    public int price;
    public string category;
    public string heritage_text;
    public string launch_status;

    public List<string> sizes = new List<string>();
    public List<ProductColorOption> colors = new List<ProductColorOption>();
}

/// JsonUtility가 최상위 배열을 못 읽어서 items로 감싼 뒤 파싱하기 위한 래퍼.
[Serializable]
public class ProductListResponse {
    public List<ProductData> items = new List<ProductData>();
}

/// GET /v1/products?launch_status=dummy 의 top-level array 응답을 받기 위한 래퍼
[Serializable]
public class ProductListArrayResponse {
    public List<ProductData> items = new List<ProductData>();
}
