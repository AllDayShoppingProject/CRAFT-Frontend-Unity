/*
 * LegacyTextTabNav.cs
 *
 * Tab / Shift + Tab을 이용한 UI 입력 요소 간 이동을 담당한다.
 *
 * - Tab: 다음 UI 요소로 이동
 * - Shift + Tab: 이전 UI 요소로 이동
 * - TMP_InputField 선택 시 자동으로 입력 모드 활성화
 */

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public class LegacyTextTabNav : MonoBehaviour
{
    [SerializeField] private Selectable[] elements;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.tabKey.wasPressedThisFrame)
            return;

        GameObject current =
            EventSystem.current.currentSelectedGameObject;

        if (current == null)
            return;

        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i].gameObject != current)
                continue;

            bool reverse =
                Keyboard.current.leftShiftKey.isPressed ||
                Keyboard.current.rightShiftKey.isPressed;

            int next = reverse ? i - 1 : i + 1;

            if (next < 0)
                next = elements.Length - 1;

            if (next >= elements.Length)
                next = 0;

            EventSystem.current.SetSelectedGameObject(
                elements[next].gameObject
            );

            if (elements[next] is TMP_InputField inputField)
                inputField.ActivateInputField();

            break;
        }
    }
}