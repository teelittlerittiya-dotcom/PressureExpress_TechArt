#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Small RectTransform helpers available from the component context menu.
/// </summary>
internal static class RectTransformContextMenu
{
    private const string ZeroLayoutMenuPath =
        "CONTEXT/RectTransform/Set Anchors to Current Position";

    [MenuItem(ZeroLayoutMenuPath)]
    private static void SetAnchorsToCurrentRect(MenuCommand command)
    {
        var rectTransform = command.context as RectTransform;
        var parent = rectTransform != null ? rectTransform.parent as RectTransform : null;
        if (rectTransform == null || parent == null)
        {
            return;
        }

        // Calculate the current rectangle in the parent's local space before changing anchors.
        // This allows the anchors to absorb the current position and size.
        var localBottomLeft = parent.InverseTransformPoint(rectTransform.TransformPoint(
            new Vector3(rectTransform.rect.xMin, rectTransform.rect.yMin, 0f)));
        var localTopRight = parent.InverseTransformPoint(rectTransform.TransformPoint(
            new Vector3(rectTransform.rect.xMax, rectTransform.rect.yMax, 0f)));

        var parentRect = parent.rect;
        var parentWidth = parentRect.width;
        var parentHeight = parentRect.height;
        if (Mathf.Approximately(parentWidth, 0f) || Mathf.Approximately(parentHeight, 0f))
        {
            return;
        }

        var anchorMin = new Vector2(
            (localBottomLeft.x - parentRect.xMin) / parentWidth,
            (localBottomLeft.y - parentRect.yMin) / parentHeight);
        var anchorMax = new Vector2(
            (localTopRight.x - parentRect.xMin) / parentWidth,
            (localTopRight.y - parentRect.yMin) / parentHeight);

        Undo.RecordObject(rectTransform, "Set RectTransform Anchors to Current Rect");

        // The current rectangle is now represented entirely by its anchors.
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition3D = Vector3.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        EditorUtility.SetDirty(rectTransform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rectTransform);
    }

    [MenuItem(ZeroLayoutMenuPath, true)]
    private static bool ValidateSetAnchorsToCurrentRect(MenuCommand command)
    {
        var rectTransform = command.context as RectTransform;
        return rectTransform != null && rectTransform.parent is RectTransform;
    }
}
#endif
