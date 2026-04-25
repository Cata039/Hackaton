using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
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
    [Tooltip("Root GameObject: scroll/background + recipe line art; toggled with recipe visibility.")]
    public GameObject recipeScrollPanel;
    public TMP_Text statusText;
    public TMP_Text resultText;
    [Tooltip("Single Image: show trofeu via sprite swap (uses Trophy Sprite Good / Bad). Ignored if both Good and Bad Images below are set.")]
    public Image resultTrophyImage;
    [Tooltip("Optional: separate UI for correct (e.g. t1 with trofeu_good). If both Good and Bad are set, that pair is used instead of Result Trophy Image.")]
    public Image resultTrophyImageGood;
    [Tooltip("Optional: separate UI for wrong (e.g. t2 with trofeu_bad).")]
    public Image resultTrophyImageBad;
    [Header("Recipe typography")]
    [Tooltip("If set, the recipe line uses this TMP font (highest priority).")]
    public TMP_FontAsset recipeFontOverride;
    [Tooltip("If no TMP override, assign a .ttf from the project (e.g. Thaleah → ThaleahFat_TTF.ttf). Converted to TMP at runtime.")]
    public Font recipeTtfFont;
    [Tooltip("If no override and no TTF above, build TMP from Resources/Fonts/PressStart2P (SIL OFL).")]
    public bool useRuntimePixelRecipeFont = true;
    [Range(8f, 500f)]
    [Tooltip("Font size for the recipe line when using the built-in pixel font (or after override is applied).")]
    public float recipePixelFontSize = 30f;

    [Header("Tuning")]
    [Tooltip("How long the full recipe (on the scroll) stays visible at the start of each level.")]
    public float recipeShowSeconds = 10f;
    public float shakeSeconds = 3f;
    public float bartenderFlashSeconds = 0.2f;
    [Tooltip("Vertical jiggle (local Y), in world units.")]
    public float shakeJiggleAmplitude = 0.12f;
    [Tooltip("How fast the bartender bobs up and down (cycles per second).")]
    public float shakeJiggleFrequency = 9f;
    [Tooltip("How fast the sprite mirrors left/right (approx. flips per second).")]
    public float shakeHorizontalFlipFrequency = 8f;
    [Header("Result feedback (serve)")]
    [Tooltip("Time for one half of a crossfade (normal→transparent, then swap sprite, then→opaque).")]
    public float resultFeedbackCrossfadeHalfSeconds = 0.2f;
    [Tooltip("After Enter: show pouring sprite (pisicu_toarna) for this many seconds, then normal cat, then pause before result.")]
    public float servePourSeconds = 3f;
    [Tooltip("After returning to normal cat, wait this long before crossfading to happy/dead cat + trophy.")]
    public float servePauseBeforeResultSeconds = 1f;
    [Tooltip("How long the trophy + happy/dead cat stay visible before returning to normal and next level.")]
    public float resultFeedbackHoldSeconds = 1.6f;
    [Header("Result sprites (assign in Inspector)")]
    [Tooltip("pisicu_toarna — pouring drink (shown first after serve).")]
    public Sprite bartenderSpritePour;
    [Tooltip("pisicu_mort — wrong answer.")]
    public Sprite bartenderSpriteMort;
    [Tooltip("pisicu_inger — correct answer.")]
    public Sprite bartenderSpriteInger;
    public Sprite trophySpriteGood;
    public Sprite trophySpriteBad;

    private IInputSource inputSource;

    // Current level recipe (ingredient counts for 1..6)
    private int[] recipe = new int[6];

    // Player’s current drink
    private int[] current = new int[6];

    private bool recipeVisible;
    private bool isShaking;
    private bool isShowingResultFeedback;
    private Color bartenderBaseColor;
    private Sprite bartenderBaseSprite;
    private Vector3 bartenderBaseLocalPos;
    private Vector3 bartenderBaseLocalScale;
    private bool bartenderPoseCached;
    private TMP_FontAsset _tmpRecipeFromResources;
    private TMP_FontAsset _tmpRecipeFromTtf;
    private Font _lastTtfUsedForRecipe;

    private void Awake()
    {
        if (inputSourceBehaviour != null)
            inputSource = inputSourceBehaviour as IInputSource;

        if (bartenderRenderer != null)
        {
            bartenderBaseColor = bartenderRenderer.color;
            bartenderBaseSprite = bartenderRenderer.sprite;
            bartenderBaseLocalPos = bartenderRenderer.transform.localPosition;
            bartenderBaseLocalScale = bartenderRenderer.transform.localScale;
            bartenderPoseCached = true;
        }

        HideAllTrophyUI();

        if (resultText != null)
            resultText.text = "";

        ApplyRecipeFont();
    }

    private void ApplyRecipeFont()
    {
        if (recipeText == null) return;

        if (recipeFontOverride != null)
        {
            recipeText.font = recipeFontOverride;
            recipeText.fontSize = recipePixelFontSize;
            return;
        }

        if (recipeTtfFont != null)
        {
            if (_tmpRecipeFromTtf == null || _lastTtfUsedForRecipe != recipeTtfFont)
            {
                _lastTtfUsedForRecipe = recipeTtfFont;
                _tmpRecipeFromTtf = TMP_FontAsset.CreateFontAsset(
                    recipeTtfFont,
                    12,
                    2,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic);
            }
            if (_tmpRecipeFromTtf != null)
            {
                recipeText.font = _tmpRecipeFromTtf;
                recipeText.fontSize = recipePixelFontSize;
            }
            return;
        }

        if (!useRuntimePixelRecipeFont) return;

        if (_tmpRecipeFromResources == null)
        {
            Font source = Resources.Load<Font>("Fonts/PressStart2P");
            if (source == null)
            {
                Debug.LogWarning("GameManager: could not load Resources/Fonts/PressStart2P. Recipe text keeps the scene font.");
                return;
            }
            _tmpRecipeFromResources = TMP_FontAsset.CreateFontAsset(
                source,
                12,
                2,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic);
        }

        if (_tmpRecipeFromResources == null) return;
        recipeText.font = _tmpRecipeFromResources;
        recipeText.fontSize = recipePixelFontSize;
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

        if (isShowingResultFeedback)
        {
            UpdateStatusUI();
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
        isShowingResultFeedback = false;
        HideAllTrophyUI();
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

        if (recipeScrollPanel != null)
            recipeScrollPanel.SetActive(recipeVisible);

        if (recipeVisible)
        {
            recipeText.text = $"RECIPE (memorize for {Mathf.CeilToInt(recipeShowSeconds)}s):\n" + FormatCounts(recipe);
        }
        else
        {
            recipeText.text = "";
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
        if (bartenderBaseSprite != null)
            bartenderRenderer.sprite = bartenderBaseSprite;
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
            resultText.text = "";
        StartCoroutine(ResultFeedbackAndNextLevelRoutine(correct));
    }

    private bool UseDualTrophyImages()
    {
        return resultTrophyImageGood != null && resultTrophyImageBad != null;
    }

    private void HideAllTrophyUI()
    {
        if (resultTrophyImage != null)
            resultTrophyImage.gameObject.SetActive(false);
        if (resultTrophyImageGood != null)
            resultTrophyImageGood.gameObject.SetActive(false);
        if (resultTrophyImageBad != null)
            resultTrophyImageBad.gameObject.SetActive(false);
    }

    private IEnumerator ResultFeedbackAndNextLevelRoutine(bool correct)
    {
        isShowingResultFeedback = true;
        if (bartenderSpriteMort == null || bartenderSpriteInger == null)
        {
            if (resultText != null)
                resultText.text = correct ? "CORRECT! (assign bartender result sprites on GameManager)" : "WRONG! (assign bartender result sprites on GameManager)";
            yield return new WaitForSeconds(2f);
            StartNewLevel();
            yield break;
        }

        float half = Mathf.Max(0.02f, resultFeedbackCrossfadeHalfSeconds);
        Sprite endCatSprite = correct ? bartenderSpriteInger : bartenderSpriteMort;

        if (bartenderRenderer != null && bartenderSpritePour != null)
        {
            if (statusText != null)
                statusText.text = "Pouring...";
            yield return StartCoroutine(CrossfadeBartenderToSprite(bartenderSpritePour, half));
            yield return new WaitForSeconds(Mathf.Max(0f, servePourSeconds));
            yield return StartCoroutine(CrossfadeBartenderToSprite(bartenderBaseSprite, half));
            yield return new WaitForSeconds(Mathf.Max(0f, servePauseBeforeResultSeconds));
        }

        if (bartenderRenderer != null)
            yield return StartCoroutine(CrossfadeBartenderToSprite(endCatSprite, half));

        if (UseDualTrophyImages())
        {
            resultTrophyImageGood.gameObject.SetActive(correct);
            resultTrophyImageBad.gameObject.SetActive(!correct);
        }
        else if (resultTrophyImage != null)
        {
            Sprite t = correct ? trophySpriteGood : trophySpriteBad;
            if (t != null)
            {
                resultTrophyImage.sprite = t;
                resultTrophyImage.gameObject.SetActive(true);
            }
            else
                resultTrophyImage.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, resultFeedbackHoldSeconds));

        HideAllTrophyUI();

        if (bartenderRenderer != null)
            yield return StartCoroutine(CrossfadeBartenderToSprite(bartenderBaseSprite, half));

        isShowingResultFeedback = false;
        StartNewLevel();
    }

    private IEnumerator CrossfadeBartenderToSprite(Sprite targetSprite, float halfDuration)
    {
        if (bartenderRenderer == null || !bartenderPoseCached) yield break;
        if (targetSprite == null) yield break;

        float t;
        t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / halfDuration);
            Color c = bartenderBaseColor;
            c.a = a;
            bartenderRenderer.color = c;
            yield return null;
        }
        bartenderRenderer.sprite = targetSprite;
        t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / halfDuration);
            Color c = bartenderBaseColor;
            c.a = a;
            bartenderRenderer.color = c;
            yield return null;
        }
        bartenderRenderer.color = bartenderBaseColor;
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