using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Adds hover effects to menu buttons for a more polished feel.
/// Attach to any UI Button element.
/// </summary>
public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale Effects")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float clickScale = 0.95f;
    [SerializeField] private float scaleSpeed = 10f;
    
    [Header("Color Effects")]
    [SerializeField] private bool changeColorOnHover = true;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(1f, 0.8f, 0.2f); // Golden yellow
    
    [Header("Audio")]
    [SerializeField] private AudioSource hoverSound;
    
    private Vector3 originalScale;
    private Vector3 targetScale;
    private TextMeshProUGUI buttonText;
    private Image buttonImage;
    private bool isHovering = false;
    private bool isPressed = false;
    
    private void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        
        // Get references
        buttonText = GetComponentInChildren<TextMeshProUGUI>();
        buttonImage = GetComponent<Image>();
        
        // Set initial color
        if (buttonText != null && changeColorOnHover)
            buttonText.color = normalColor;
    }
    
    private void Update()
    {
        // Smoothly interpolate to target scale
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        if (!isPressed)
            targetScale = originalScale * hoverScale;
        
        // Change color
        if (buttonText != null && changeColorOnHover)
            buttonText.color = hoverColor;
            
        // Play hover sound
        if (hoverSound != null)
            hoverSound.Play();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        if (!isPressed)
            targetScale = originalScale;
        
        // Reset color
        if (buttonText != null && changeColorOnHover)
            buttonText.color = normalColor;
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        targetScale = originalScale * clickScale;
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        targetScale = isHovering ? originalScale * hoverScale : originalScale;
    }
}
