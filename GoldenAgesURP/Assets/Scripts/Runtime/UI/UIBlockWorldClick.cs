using UnityEngine;
using UnityEngine.EventSystems;

public class UIBlockWorldClick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public void OnPointerDown(PointerEventData eventData) { PlayerInput.BlockWorldPointerThisFrame(); }
    public void OnPointerUp(PointerEventData eventData) { PlayerInput.BlockWorldPointerThisFrame(); }
    public void OnPointerClick(PointerEventData eventData) { PlayerInput.BlockWorldPointerThisFrame(); }
}
