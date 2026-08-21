/*
 * TMP 폰트 에셋 진단 / 복구 도구.
 *
 * 한글이 네모(□)로 깨지는 사고의 대부분은 아래 두 설정 때문이다.
 *
 *  - Multi Atlas Textures 가 꺼진 Dynamic 폰트
 *      아틀라스 한 장(보통 1024x1024)이 꽉 차는 순간부터 새 글리프를 못 넣는다.
 *      TMP는 콘솔에 "Unable to add the requested character..." 를 뱉고 □ 를 그린다.
 *      Font Asset Creator 창으로 만든 폰트는 이 옵션이 기본으로 꺼져 있다.
 *
 *  - Clear Dynamic Data On Build 가 켜진 Dynamic 폰트
 *      빌드에 아틀라스가 텅 빈 채로 들어간다. 실행 중에 전부 다시 구워야 하는데,
 *      위 옵션까지 꺼져 있으면 빌드에서만 한글이 전부 깨진다.
 *
 * 메뉴에서 한 번 돌리면 프로젝트의 모든 TMP 폰트를 검사하고 위 두 개를 고쳐준다.
 */

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class KoreanFontDoctor
{
    [MenuItem("Tools/한글 폰트 진단 및 복구")]
    public static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");

        StringBuilder report = new StringBuilder();
        int repaired = 0;

        report.AppendLine(
            $"[KoreanFontDoctor] TMP 폰트 에셋 {guids.Length}개 검사"
        );
        report.AppendLine();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            TMP_FontAsset font =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (font == null)
            {
                continue;
            }

            bool isDynamic =
                font.atlasPopulationMode == AtlasPopulationMode.Dynamic;

            List<string> fixes = new List<string>();

            SerializedObject so = new SerializedObject(font);

            SerializedProperty multiAtlas =
                so.FindProperty("m_IsMultiAtlasTexturesEnabled");

            SerializedProperty clearOnBuild =
                so.FindProperty("m_ClearDynamicDataOnBuild");

            if (isDynamic &&
                multiAtlas != null &&
                !multiAtlas.boolValue)
            {
                multiAtlas.boolValue = true;
                fixes.Add("Multi Atlas Textures 켬");
            }

            if (isDynamic &&
                clearOnBuild != null &&
                clearOnBuild.boolValue)
            {
                clearOnBuild.boolValue = false;
                fixes.Add("Clear Dynamic Data On Build 끔");
            }

            if (fixes.Count > 0)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(font);
                repaired++;
            }

            int pages =
                font.atlasTextures != null
                    ? font.atlasTextures.Length
                    : 0;

            report.AppendLine($"[{font.name}]");
            report.AppendLine($"  경로       : {path}");
            report.AppendLine($"  채우기     : {font.atlasPopulationMode}");
            report.AppendLine(
                $"  아틀라스   : {font.atlasWidth}x{font.atlasHeight}, {pages}장"
            );
            report.AppendLine(
                $"  구워진 글자: {font.characterTable.Count}자"
            );
            report.AppendLine(
                $"  원본 폰트  : " +
                (font.sourceFontFile != null
                    ? font.sourceFontFile.name
                    : "<비어있음>")
            );

            if (isDynamic && font.sourceFontFile == null)
            {
                report.AppendLine(
                    "  !! Dynamic 인데 Source Font File 이 비어있다. " +
                    "런타임에 새 글자를 만들 수 없어서 한글이 □ 로 뜬다."
                );
            }

            if (fixes.Count > 0)
            {
                report.AppendLine(
                    "  => 수정함: " + string.Join(", ", fixes)
                );
            }

            report.AppendLine();
        }

        if (repaired > 0)
        {
            AssetDatabase.SaveAssets();
        }

        report.AppendLine($"수정된 폰트 에셋: {repaired}개");

        Debug.Log(report.ToString());
    }
}
