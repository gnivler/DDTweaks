using QxFramework.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DDTweaks;

public class EasyCloser : MonoBehaviour, IPointerClickHandler
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            foreach (var easyCloser in FindObjectsOfType<EasyCloser>())
            {
                // check if on top, in case an event window popped in
                if (GetComponent<RectTransform>().GetSiblingIndex() <= 1)
                {
                    var window = easyCloser.GetComponent<UIBase>();
                    if (window is not null)
                        UIManager.Instance.Close(window);
                }
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            var window = eventData.pointerPressRaycast.gameObject.GetComponentInParent<UIBase>();
            if (window is null)
                return;
            if (window == gameObject.GetComponentInParent<UIBase>())
                UIManager.Instance.Close(GetComponent<UIBase>());
        }
    }
}