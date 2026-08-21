/*
 * 화면 전체에 Tile A / Tile B 패턴을 반복 배치한다.
 * PatternTile(격자 크기) 안에 Image(Sprite 렌더 크기)가 들어가는 구조라,
 * Sprite Scale을 바꿔도 패턴 간격과 중앙 정렬은 영향을 받지 않는다.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IntroPatternUI : MonoBehaviour
{
    [Header("Pattern Tiles")]
    [SerializeField] private Sprite tileA;
    [SerializeField] private Sprite tileB;

    [Header("Sprite Render Scale")]
    [Range(0.1f, 2f)]
    [SerializeField] private float tileAScale = 1f;

    [Range(0.1f, 2f)]
    [SerializeField] private float tileBScale = 1f;

    [Header("Empty Content Area")]
    [SerializeField] private RectTransform contentArea;

    [Header("Tile Layout")]
    [SerializeField]
    private Vector2 tileSize =
        new Vector2(80f, 80f);

    [SerializeField]
    private Vector2 spacing =
        Vector2.zero;

    [Header("Extra Coverage")]
    [SerializeField] private int extraColumns = 2;
    [SerializeField] private int extraRows = 2;

    private RectTransform rectTransform;

    private readonly List<GameObject> spawnedTiles = new();

    private Vector2 lastSize;

    private void Awake()
    {
        rectTransform =
            GetComponent<RectTransform>();

        lastSize =
            rectTransform.rect.size;

        GeneratePattern();
    }

    private void OnEnable()
    {
        if (rectTransform == null)
        {
            rectTransform =
                GetComponent<RectTransform>();
        }

        GeneratePattern();
    }

    private void Update()
    {
        Vector2 currentSize =
            rectTransform.rect.size;

        if (currentSize != lastSize)
        {
            lastSize = currentSize;

            GeneratePattern();
        }
    }

    private void GeneratePattern()
    {
        ClearPattern();

        if (tileA == null || tileB == null)
        {
            Debug.LogWarning(
                "IntroPatternUI: Tile A 또는 Tile B가 지정되지 않았습니다.",
                this
            );

            return;
        }

        Rect rect =
            rectTransform.rect;

        float stepX =
            tileSize.x + spacing.x;

        float stepY =
            tileSize.y + spacing.y;

        if (stepX <= 0f || stepY <= 0f)
        {
            return;
        }

        int columns =
            Mathf.CeilToInt(
                rect.width / stepX
            )
            + extraColumns * 2;

        int rows =
            Mathf.CeilToInt(
                rect.height / stepY
            )
            + extraRows * 2;

        // 홀수여야 타일 하나가 정확히 정중앙에 온다.
        if (columns % 2 == 0)
        {
            columns++;
        }

        if (rows % 2 == 0)
        {
            rows++;
        }

        int centerColumn =
            columns / 2;

        int centerRow =
            rows / 2;

        float centerX =
            rect.center.x;

        float centerY =
            rect.center.y;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int relativeX =
                    column - centerColumn;

                int relativeY =
                    row - centerRow;

                Vector2 position =
                    new Vector2(
                        centerX + relativeX * stepX,
                        centerY + relativeY * stepY
                    );

                if (IsTileInsideContentArea(position))
                {
                    continue;
                }

                // 체커보드 배치. 중앙 (0, 0)은 반드시 A.
                bool useTileA =
                    (relativeX + relativeY) % 2 == 0;

                Sprite sprite =
                    useTileA
                        ? tileA
                        : tileB;

                float spriteScale =
                    useTileA
                        ? tileAScale
                        : tileBScale;

                CreateTile(
                    sprite,
                    spriteScale,
                    position
                );
            }
        }
    }

    private void CreateTile(
        Sprite sprite,
        float spriteScale,
        Vector2 position
    )
    {
        // PatternTile: 격자 역할이라 크기는 항상 tileSize다.
        GameObject tileObject =
            new GameObject(
                "PatternTile",
                typeof(RectTransform)
            );

        tileObject.transform.SetParent(
            transform,
            false
        );

        RectTransform tileRect =
            tileObject.GetComponent<RectTransform>();

        tileRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        tileRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        tileRect.pivot =
            new Vector2(0.5f, 0.5f);

        // 이 값은 Sprite Scale과 무관하다.
        tileRect.sizeDelta =
            tileSize;

        tileRect.anchoredPosition =
            position;

        GameObject imageObject =
            new GameObject(
                "Sprite",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        imageObject.transform.SetParent(
            tileObject.transform,
            false
        );

        RectTransform imageRect =
            imageObject.GetComponent<RectTransform>();

        imageRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        imageRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        imageRect.pivot =
            new Vector2(0.5f, 0.5f);

        // Sprite Scale은 여기서만 적용한다. PatternTile 크기는 건드리지 않는다.
        imageRect.sizeDelta =
            tileSize * spriteScale;

        imageRect.anchoredPosition =
            Vector2.zero;

        Image image =
            imageObject.GetComponent<Image>();

        image.sprite =
            sprite;

        image.preserveAspect =
            true;

        image.raycastTarget =
            false;

        spawnedTiles.Add(
            tileObject
        );
    }

    private bool IsTileInsideContentArea(
        Vector2 localPosition
    )
    {
        if (contentArea == null)
        {
            return false;
        }

        Vector3 worldPosition =
            rectTransform.TransformPoint(
                localPosition
            );

        Vector3[] corners =
            new Vector3[4];

        contentArea.GetWorldCorners(
            corners
        );

        Bounds contentBounds =
            new Bounds(
                corners[0],
                Vector3.zero
            );

        for (int i = 1; i < corners.Length; i++)
        {
            contentBounds.Encapsulate(
                corners[i]
            );
        }

        // 비교 대상은 실제 Sprite 크기가 아니라 타일 격자 영역이다.
        Vector3 worldTileSize =
            rectTransform.TransformVector(
                tileSize
            );

        Bounds tileBounds =
            new Bounds(
                worldPosition,
                new Vector3(
                    Mathf.Abs(worldTileSize.x),
                    Mathf.Abs(worldTileSize.y),
                    0f
                )
            );

        return contentBounds.Intersects(
            tileBounds
        );
    }

    private void ClearPattern()
    {
        for (int i = spawnedTiles.Count - 1; i >= 0; i--)
        {
            if (spawnedTiles[i] != null)
            {
                Destroy(
                    spawnedTiles[i]
                );
            }
        }

        spawnedTiles.Clear();
    }

    private void OnValidate()
    {
        tileAScale =
            Mathf.Max(
                0.1f,
                tileAScale
            );

        tileBScale =
            Mathf.Max(
                0.1f,
                tileBScale
            );
    }
}