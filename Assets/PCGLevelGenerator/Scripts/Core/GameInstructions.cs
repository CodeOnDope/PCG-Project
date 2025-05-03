using UnityEngine;
using UnityEngine.UI;

public class GameInstructions : MonoBehaviour
{
    [SerializeField] private Text instructionsText; // Reference to UI Text component
    [SerializeField] private bool allowHiding = true; // Whether player can hide instructions
    [SerializeField] private KeyCode toggleKey = KeyCode.H; // Key to toggle instructions visibility
    [SerializeField] private Vector2 screenPosition = new Vector2(20, 20); // Position on screen (pixels from top-left)
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.8f); // Text color with alpha
    [SerializeField] private int fontSize = 16; // Font size for instructions

    private bool isVisible = true;

    void Start()
    {
        // Check if Text component is assigned
        if (instructionsText == null)
        {
            Debug.LogError("Instructions Text component not assigned to GameInstructions script!");
            return;
        }

        // Configure the text component
        instructionsText.text = "CONTROLS:\n" +
                               "- Movement: Arrow Keys or WASD\n" +
                               "- Fire at enemies: Left Mouse Click" + "\r\nRestart the Level when the Player Dies";

        if (allowHiding)
        {
            instructionsText.text += "\n\nPress H to toggle instructions";
        }

        // Set text properties
        instructionsText.color = textColor;
        instructionsText.fontSize = fontSize;

        // Set position (adjust RectTransform)
        RectTransform rectTransform = instructionsText.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0, 1); // Anchor to top-left
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = screenPosition;
        }

        // Make sure instructions are visible
        instructionsText.gameObject.SetActive(true);
    }

    void Update()
    {
        // Toggle instructions visibility when player presses the toggle key
        if (allowHiding && Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
            instructionsText.gameObject.SetActive(isVisible);
        }
    }

    // Call this method to show/hide instructions programmatically
    public void SetInstructionsVisible(bool visible)
    {
        isVisible = visible;
        instructionsText.gameObject.SetActive(isVisible);
    }
}