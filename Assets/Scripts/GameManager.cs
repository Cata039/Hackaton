using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;  // <-- ADAUGĂ ASTA

public class GameManager : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Drag a component here that implements IInputSource (KeyboardInputSource for now).")]
    public MonoBehaviour inputSourceBehaviour;

    [Header("Scene References")]
    public SpriteRenderer bartenderRenderer;
   public TMP_Text recipeText;
public TMP_Text statusText;
public TMP_Text resultText;

    [Header("Tuning")]
    public float recipeShowSeconds = 10f;
    public float shakeSeconds = 3f;
    public float bartenderFlashSeconds = 0.2f;
    [Tooltip("Vertical jiggle (local Y), in world units.")]
    public float shakeJiggleAmplitude = 0.12f;
    [Tooltip("How fast the bartender bobs up and down (cycles per second).")]
    public float shakeJiggleFrequency = 9f;
    [Tooltip("How fast the sprite mirrors left/right (approx. flips per second).")]
    public float shakeHorizontalFlipFrequency = 8f;

    private IInputSource inputSource;

    // Current level recipe (ingredient counts for 1..6)
    private int[] recipe = new int[6];

    // Player’s current drink
    private int[] current = new int[6];

    private bool recipeVisible;
    private bool isShaking;
    private Color bartenderBaseColor;
    private Vector3 bartenderBaseLocalPos;
    private Vector3 bartenderBaseLocalScale;
    private bool bartenderPoseCached;

    private void Awake()
    {
        if (inputSourceBehaviour != null)
            inputSource = inputSourceBehaviour as IInputSource;

        if (bartenderRenderer != null)
        {
            bartenderBaseColor = bartenderRenderer.color;
            bartenderBaseLocalPos = bartenderRenderer.transform.localPosition;
            bartenderBaseLocalScale = bartenderRenderer.transform.localScale;
            bartenderPoseCached = true;
        }

        if (resultText != null)
            resultText.text = "";
    }

    private void Start()
    {
        StartNewLevel();
    }

    private void Update()
    {
        if (inputSource == null)
        {
            if (statusText != null)
                statusText.text = "ERROR: Input Source not set. Select GameManager and drag KeyboardInputSource into Input Source Behaviour.";
            return;
        }

        // Ingredient keys 1..6
        if (!isShaking)
        {
            for (int i = 1; i <= 6; i++)
            {
                if (inputSource.GetIngredientPressed(i))
                {
                    AddIngredient(i);
                }
            }
        }

        // Shake
        if (!isShaking && inputSource.GetShakePressed())
        {
            StartCoroutine(ShakeRoutine());
        }

        // Serve
        if (!isShaking && inputSource.GetServePressed())
        {
            ServeDrink();
        }

        UpdateStatusUI();
    }

    private void StartNewLevel()
    {
        isShaking = false;
        ResetBartenderPose();

        // Clear player drink
        for (int i = 0; i < 6; i++)
            current[i] = 0;

        // Build a very simple random recipe:
        // Pick 3 random ingredients (can repeat) each count +1
        for (int i = 0; i < 6; i++)
            recipe[i] = 0;

        int picks = 3;
        for (int p = 0; p < picks; p++)
        {
            int idx = Random.Range(0, 6); // 0..5
            recipe[idx] += 1;
        }

        if (resultText != null)
            resultText.text = "";

        recipeVisible = true;
        UpdateRecipeUI();
        StopAllCoroutines();
        StartCoroutine(HideRecipeAfterDelay());

        UpdateStatusUI();
    }

    private IEnumerator HideRecipeAfterDelay()
    {
        yield return new WaitForSeconds(recipeShowSeconds);
        recipeVisible = false;
        UpdateRecipeUI();
    }

    private void UpdateRecipeUI()
    {
        if (recipeText == null) return;

        if (recipeVisible)
        {
            recipeText.text = "RECIPE (memorize in 10s):\n" + FormatCounts(recipe);
        }
        else
        {
            recipeText.text = "RECIPE: (hidden)";
        }
    }

    private void AddIngredient(int ingredientIndex1to6)
    {
        int idx = ingredientIndex1to6 - 1;
        current[idx] += 1;

        // Bartender simple “animation”: quick color flash
        if (bartenderRenderer != null)
            StartCoroutine(BartenderFlashRoutine());

        if (statusText != null)
            statusText.text = $"Added Bottle {ingredientIndex1to6}";
    }

    private IEnumerator BartenderFlashRoutine()
    {
        bartenderRenderer.color = Color.white;
        yield return new WaitForSeconds(bartenderFlashSeconds);
        bartenderRenderer.color = bartenderBaseColor;
    }

    private void ResetBartenderPose()
    {
        if (bartenderRenderer == null) return;
        if (bartenderPoseCached)
        {
            bartenderRenderer.transform.localPosition = bartenderBaseLocalPos;
            bartenderRenderer.transform.localScale = bartenderBaseLocalScale;
        }
        bartenderRenderer.color = bartenderBaseColor;
    }

    private IEnumerator ShakeRoutine()
    {
        isShaking = true;
        if (statusText != null)
            statusText.text = "Shaking...";

        float t = 0f;
        while (t < shakeSeconds)
        {
            t += Time.deltaTime;
            if (bartenderRenderer != null && bartenderPoseCached)
            {
                Transform tr = bartenderRenderer.transform;
                float y = Mathf.Sin(t * 2f * Mathf.PI * shakeJiggleFrequency) * shakeJiggleAmplitude;
                tr.localPosition = bartenderBaseLocalPos + new Vector3(0f, y, 0f);
                // sign(sin(π f t)) flips f times per second (left/right mirror of the sprite)
                float flip = Mathf.Sin(t * Mathf.PI * shakeHorizontalFlipFrequency) >= 0f ? 1f : -1f;
                float absX = Mathf.Abs(bartenderBaseLocalScale.x);
                tr.localScale = new Vector3(absX * flip, bartenderBaseLocalScale.y, bartenderBaseLocalScale.z);

                float ping = Mathf.PingPong(t * 8f, 1f);
                bartenderRenderer.color = Color.Lerp(bartenderBaseColor, Color.gray, ping);
            }
            else if (bartenderRenderer != null)
            {
                float ping = Mathf.PingPong(t * 8f, 1f);
                bartenderRenderer.color = Color.Lerp(bartenderBaseColor, Color.gray, ping);
            }
            yield return null;
        }

        ResetBartenderPose();

        isShaking = false;

        if (statusText != null)
            statusText.text = "Done shaking. Press Enter to serve.";
    }

    private void ServeDrink()
    {
        bool correct = IsCorrect();

        if (resultText != null)
        {
            resultText.text = correct ? "CORRECT!" : "WRONG!";
        }

        // Start next level after a short delay
        StartCoroutine(NextLevelAfterDelay(2f));
    }

    private IEnumerator NextLevelAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        StartNewLevel();
    }

    private bool IsCorrect()
    {
        for (int i = 0; i < 6; i++)
        {
            if (current[i] != recipe[i])
                return false;
        }
        return true;
    }

    private void UpdateStatusUI()
    {
        if (statusText == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Your Drink:");
        sb.AppendLine(FormatCounts(current));
        sb.AppendLine(isShaking ? "State: Shaking..." : "State: Idle");
        sb.AppendLine("Controls: 1-6 add ingredients | Space = Shake | Enter = Serve");

        statusText.text = sb.ToString();
    }

    private string FormatCounts(int[] counts)
    {
        // counts[0] is Bottle1
        // Show only non-zero entries
        StringBuilder sb = new StringBuilder();
        bool any = false;
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] > 0)
            {
                any = true;
                sb.AppendLine($"- Bottle {i + 1}: x{counts[i]}");
            }
        }
        if (!any) sb.AppendLine("- (nothing yet)");
        return sb.ToString();
    }
}