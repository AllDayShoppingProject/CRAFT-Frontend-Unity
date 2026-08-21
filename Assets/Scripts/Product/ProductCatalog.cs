using System.Collections.Generic;
using UnityEngine;

/// 제품 메타데이터 보관소. 기본은 더미 데이터이고 Load()로 서버 응답을 밀어넣으면 교체된다.
public static class ProductCatalog {

    /// BagLibrary가 없을 때만 쓰는 폴백 (자리표시자 큐브용)
    private static readonly List<ProductColorOption> FallbackColors = new List<ProductColorOption> {
        new ProductColorOption { color = "black",  displayName = "블랙",     hex = "#1C1C1E" },
        new ProductColorOption { color = "cognac", displayName = "코냑",     hex = "#8C5A2B" },
        new ProductColorOption { color = "ivory",  displayName = "아이보리", hex = "#EDE6D8" },
    };

    /// 컬러 목록 생성. 색마다 FBX가 따로라 제품별로 개수가 제각각이다.
    private static List<ProductColorOption> BuildColorOptions(int index) {
        BagVariantEntry[] variants = BagLibrary.GetVariants(index);

        if (variants.Length == 0) return CopyOf(FallbackColors);

        var options = new List<ProductColorOption>(variants.Length);

        foreach (BagVariantEntry variant in variants) {
            if (variant == null || variant.model == null) continue;

            options.Add(new ProductColorOption {
                color = variant.code,
                model_supported = true,
                displayName = variant.displayName,
                hex = variant.hex,
            });
        }

        return options.Count > 0 ? options : CopyOf(FallbackColors);
    }

    private static List<ProductColorOption> CopyOf(List<ProductColorOption> source) {
        var copy = new List<ProductColorOption>(source.Count);

        foreach (ProductColorOption option in source) {
            copy.Add(new ProductColorOption {
                color = option.color,
                model_supported = option.model_supported,
                displayName = option.displayName,
                hex = option.hex,
            });
        }

        return copy;
    }

    /// 카탈로그 인덱스. product_id 는 1부터라 인덱스는 하나 작다.
    public static int IndexOf(ProductData product) {
        return product != null && product.product_id > 0 ? product.product_id - 1 : -1;
    }

    private static List<ProductData> products;

    /// 백엔드 응답으로 카탈로그 교체 (없으면 더미 데이터 유지)
    public static void Load(ProductListResponse response) {
        if (response == null || response.items == null || response.items.Count == 0) return;

        // 서버는 컬러 코드만 준다. 표시 이름과 칩 색은 클라이언트가 채워야 한다.
        foreach (ProductData product in response.items) Enrich(product);

        products = response.items;
    }

    /// 응답 본문 파싱. 최상위가 배열이면 JsonUtility가 못 읽으므로 items 객체로 감싼 뒤 넘긴다.
    public static ProductListResponse ParseProductsResponse(string json) {
        if (string.IsNullOrEmpty(json)) return null;

        string trimmed = json.Trim();

        string wrapped = trimmed.StartsWith("[")
            ? "{\"items\":" + trimmed + "}"
            : trimmed;

        return JsonUtility.FromJson<ProductListResponse>(wrapped);
    }

    /// 표시 이름·칩 색을 BagLibrary에서 채운다. 없는 컬러는 코드를 이름으로 쓰고 회색 칩이 된다.
    private static void Enrich(ProductData product) {
        if (product == null || product.colors == null) return;

        int index = IndexOf(product);

        foreach (ProductColorOption option in product.colors) {
            if (option == null) continue;

            BagVariantEntry variant = BagLibrary.GetVariant(index, option.color);

            bool matched = variant != null && variant.code == option.color;

            option.displayName = matched ? variant.displayName : option.color;
            option.hex = matched ? variant.hex : "#8A8A8E";

            // 모델 없는 컬러는 서버 값과 무관하게 스와치 비활성
            if (!matched || variant.model == null) option.model_supported = false;
        }
    }

    public static IReadOnlyList<ProductData> All {
        get {
            if (products == null) products = BuildDummyData();
            return products;
        }
    }

    /// 스툴/가방 인덱스(0~5)로 조회. 범위를 벗어나면 순환시킨다.
    public static ProductData GetByIndex(int index) {
        var all = All;
        if (all.Count == 0) return null;
        return all[((index % all.Count) + all.Count) % all.Count];
    }

    /// "Bag_3" 같은 오브젝트 이름 뒤의 숫자를 인덱스로 써서 조회
    public static ProductData GetByObjectName(string objectName) {
        int index = 0;
        if (!string.IsNullOrEmpty(objectName)) {
            int underscore = objectName.LastIndexOf('_');
            if (underscore >= 0 && underscore < objectName.Length - 1) {
                int.TryParse(objectName.Substring(underscore + 1), out index);
            }
        }
        return GetByIndex(index);
    }

    /// 제품 한 건의 더미 데이터.
    /// Seeds 배열 순서가 곧 BagLibrary.bagModels 순서라, p1 → p6 순서를 지켜야 이름과 모델이 짝을 이룬다.
    private class Seed {
        public string folderKey;   // 짝이 되는 에셋 폴더 (메모용)
        public string name;
        public int price;
        public string category;    // 명세서 products.category (영문 소문자)
        public string size;        // 시안은 사이즈를 하나만 보여준다

        /// [제품 상세정보] 아코디언 본문. ProductData.heritage_text 자리에 담긴다.
        public string detailedInfo;
    }

    private static readonly Seed[] Seeds = {
        new Seed {
            folderKey = "p1", name = "Stark 사이드 스터드 비세토스 백팩",
            price = 1_890_000, category = "backpack", size = "M",
            detailedInfo =
                "피라미드 모양 스터드 장식과 천연 나파 가죽 트림이 특징인 비세토스 모노그램 캔버스 백팩\n" +
                "\n" +
                "글로벌 노마드의 자유로운 정신을 담은 휴대용 아이콘. 비세토스 캔버스와 천연 나파 가죽으로 " +
                "제작된 스타크 백팩은 기기, 서류, 액세서리를 품격 있게 정리할 수 있습니다. " +
                "피라미드 모양 스터드가 반짝이는 피니시로 실루엣을 장식합니다.\n" +
                "\n" +
                "• 조절 가능한 어깨 스트랩\n" +
                "• 가죽 손잡이 부분\n" +
                "• MCM 로고 플레이트\n" +
                "• 전면 지퍼 수납공간\n" +
                "• 외부 사이드 포켓과 스터드 장식\n" +
                "• 양방향 지퍼 클로저\n" +
                "• 내부 포켓 및 13인치 노트북과 태블릿을 위한 슬리브\n" +
                "• 코발트 금속 장식\n" +
                "• 바디: 비세토스 모노그램 캔버스\n" +
                "• 트림: 천연 나파 가죽\n" +
                "• 다크톤 코발트 금속 장식\n" +
                "• 인조 나파 안감\n" +
                "• 약 16 × 33 × 41 센티미터\n" +
                "• 스트랩 길이: 76cm~90cm, 핸들 길이: 8cm\n" +
                "• 제조국: 대한민국",
        },
        new Seed {
            folderKey = "p2", name = "Ella 맥시 모노그램 레더 보스턴 백",
            price = 1_690_000, category = "boston", size = "S",
            detailedInfo =
                "엠보싱 처리된 맥시 비세토스 모노그램과 바이에른 다이아몬드 가죽 참이 어우러진 " +
                "천연 풀그레인 레더 보스턴 백\n" +
                "\n" +
                "하우스의 맥시 모노그램이 각인된 풀 그레인 가죽 소재의 스몰 엘라 보스턴 백은 " +
                "뮌헨 황금시대의 장인 정신에서 탄생한 아이코닉한 아이템입니다. " +
                "독일 헤리티지의 상징인 바이에른 다이아몬드 모티브가 기하학적으로 표현된 " +
                "가죽 참 장식으로 포인트를 더하였습니다.\n" +
                "\n" +
                "• 탈착이 가능하며 길이 조절이 가능한 레더 스트랩\n" +
                "• 가죽 탑 핸들\n" +
                "• 엠보싱 처리된 맥시 모노그램\n" +
                "• 탈착 가능한 바바리안 다이아몬드 가죽 참\n" +
                "• 양방향 지퍼 클로저\n" +
                "• 가죽 패치가 더해진 내부 슬립 포켓\n" +
                "• 바디: 천연 풀 그레인 레더\n" +
                "• 16K 골드 도금 메탈 하드웨어\n" +
                "• 스웨이드 마감의 극세사 안감\n" +
                "• 약 12 × 22 × 15 센티미터\n" +
                "• 스트랩 길이: 115 cm - 134 cm, 핸들 드롭: 8 cm\n" +
                "• 제조국: 이탈리아",
        },
        new Seed {
            folderKey = "p3", name = "Aren 다이아몬드 퀼팅 레더 백팩",
            price = 2_690_000, category = "backpack", size = "M",
            detailedInfo =
                "다이아몬드 모티프 퀼딩 나파가죽 백팩\n" +
                "\n" +
                "백팩을 한층 세련된 실루엣으로 완성한 최신 Aren 백팩은 최고급 나파 가죽에 적용된 " +
                "바이에른 다이아몬드 퀼팅 패턴을 통해 하우스의 뛰어난 장인정신을 보여줍니다.\n" +
                "\n" +
                "• 길이 조절이 가능한 숄더 스트랩\n" +
                "• 가죽 탑 핸들\n" +
                "• 다이아몬드 퀼팅 모티프\n" +
                "• 로고 브라스 플레이트\n" +
                "• 양방향 지퍼 클로저\n" +
                "• 13인치 노트북 수납이 가능한 내부 슬리브 및 포켓\n" +
                "• 바디: 나파 레더\n" +
                "• 무광 블랙 메탈 하드웨어\n" +
                "• 리사이클 코튼 라이닝\n" +
                "• 약 20 × 29 × 45 센티미터\n" +
                "• 제조국: 이탈리아",
        },
        new Seed {
            folderKey = "p4", name = "Dessau 비세토스 드로우스트링 백",
            price = 1_550_000, category = "bucket", size = "M",
            detailedInfo =
                "매칭 되는 지퍼 파우치와 천연 가죽 트림이 더해진 비세토스 모노그램 캔버스 버킷백\n" +
                "\n" +
                "바우하우스 디자인의 역사적 중심지에서 이름을 딴 Dessau 드로스트링 백은, " +
                "실용성과 디자인의 균형을 통해 이 독일 예술 운동의 정신을 되살립니다. " +
                "비세토스 캔버스와 천연 가죽으로 정교하게 제작된 클래식 코냑 실루엣에 " +
                "뮌헨 하우스의 아이코닉 로고 브라스 플레이트로 품격을 더했습니다. " +
                "모노그램 파우치가 더해져 이 드로우스트링 백의 완성도를 높입니다.\n" +
                "\n" +
                "• 탈부착이 가능하며 길이 조절이 가능한 가죽 스트랩\n" +
                "• 탈부착 가능한 가죽 탑 핸들\n" +
                "• 로고 브라스 플레이트\n" +
                "• 가죽 드로스트링 잠금장치\n" +
                "• 비세토스 모노그램 지퍼 파우치 포함\n" +
                "• 바디: 비세토스 모노그램 캔버스\n" +
                "• 트림: 천연 가죽\n" +
                "• 24K 골드 도금 브라스 하드웨어\n" +
                "• 스웨이드 마감의 극세사 안감\n" +
                "• 약 14 × 28 × 23 센티미터\n" +
                "• 스트랩 길이: 97 cm - 121 cm, 핸들 드롭: 20 cm\n" +
                "• 제조국: 대한민국",
        },
        new Seed {
            folderKey = "p5", name = "Tracy 비세토스 레더 믹스 사첼",
            price = 1_850_000, category = "satchel", size = "S",
            detailedInfo =
                "라우렐 락 잠금장치와 이탈리안 카프스킨 레더 트림이 더해진 비세토스 모노그램 캔버스 사첼 백\n" +
                "\n" +
                "MCM의 오리지널 아이콘, ‘트레이시’는 한눈에 시선을 사로잡는 우아함과 세련됨을 겸비한 " +
                "디자인이 특징입니다. 24K 골드 도금 라우렐 락이 클래식한 실루엣에 화려한 포인트를 더하고, " +
                "비세토스 캔버스와 이탈리아산 카프스킨 디테일이 부드럽고 고급스러운 촉감을 선사합니다.\n" +
                "\n" +
                "• 탈부착이 가능하며 길이 조절이 가능한 가죽 스트랩\n" +
                "• 가죽 탑 핸들\n" +
                "• 라우렐 락 클로저\n" +
                "• 앞면 포켓\n" +
                "• 내부 지퍼 수납공간\n" +
                "• 바디: 비세토스 모노그램 캔버스\n" +
                "• 트림: 이탈리안 카프스킨\n" +
                "• 24K 골드 도금 브라스 하드웨어\n" +
                "• 스웨이드 마감의 극세사 안감\n" +
                "• 약 9 × 25 × 19 센티미터\n" +
                "• 스트랩 드롭: 55 cm, 핸들 드롭: 5 cm\n" +
                "• 제조국: 대한민국",
        },
        new Seed {
            folderKey = "p6", name = "Pina 비세토스 스터드 장식 토트",
            price = 1_690_000, category = "tote", size = "M",
            detailedInfo =
                "나파 가죽 트림과 메탈 스터드 장식이 돋보이는 비세토스 모노그램 캔버스 토트백\n" +
                "\n" +
                "비세토스 캔버스와 부드러운 나파 가죽의 클래식한 조합을 통해 하우스의 시그니처 장인 정신이 " +
                "돋보이는, 시대를 초월한 보울러 디자인의 백입니다. 코냑 컬러의 실루엣에는 바이에른 " +
                "다이아몬드와 아이코닉한 라우렐 엠블럼을 형상화한 스터드 장식을 더해 완성하였습니다.\n" +
                "\n" +
                "• 탈부착이 가능하며 길이 조절이 가능한 가죽 스트랩\n" +
                "• 가죽 핸들\n" +
                "• 로고 브라스 플레이트\n" +
                "• 금속 로고 스터드 장식\n" +
                "• 양방향 지퍼 클로저\n" +
                "• 내부 슬립 포켓 및 지퍼 수납공간\n" +
                "• D링 참 장식 디테일\n" +
                "• 가방 바닥 보호에 도움을 주는 메탈 피트\n" +
                "• 바디: 비세토스 모노그램 캔버스\n" +
                "• 트림: 나파 가죽\n" +
                "• 24K 골드 도금 브라스 하드웨어\n" +
                "• 스웨이드 마감의 극세사 안감\n" +
                "• 약 13 × 30 × 24 센티미터\n" +
                "• 스트랩 길이: 107.5cm - 131.5cm, 핸들 드롭: 11cm\n" +
                "• 제조국: 대한민국",
        },
    };

    private static List<ProductData> BuildDummyData() {
        var list = new List<ProductData>();

        for (int i = 0; i < Seeds.Length; i++) {
            Seed seed = Seeds[i];

            list.Add(new ProductData {
                // product_id 는 1부터 시작한다
                product_id = i + 1,
                name = seed.name,
                price = seed.price,
                category = seed.category,
                launch_status = "dummy",

                heritage_text = seed.detailedInfo,

                sizes = new List<string> { seed.size },

                // 제품마다 새 객체 (공유하면 한쪽 수정이 다른 제품에도 보인다)
                colors = BuildColorOptions(i)
            });
        }

        return list;
    }
}
