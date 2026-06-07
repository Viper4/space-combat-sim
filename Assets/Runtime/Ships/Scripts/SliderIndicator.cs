using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class SliderIndicator : MonoBehaviour
{
    [SerializeField] private List<Slider> sliders = new List<Slider>();
    private List<Image> fillImages = new List<Image>();
    [SerializeField] private List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>();
    
    private enum TextType
    {
        Raw,
        Percent,
        Decimal,
        Division
    }
    [SerializeField] private TextType textType = TextType.Raw;
    [SerializeField] private string textFormat = "F2";

    [SerializeField] private float[] thresholds;
    [SerializeField] private Color[] thresholdColors;

    private void Awake()
    {
        foreach (Slider slider in sliders)
        {
            fillImages.Add(slider.fillRect.GetComponent<Image>());
        }
        if (thresholds.Length != thresholdColors.Length)
        {
            Debug.LogWarning($"{name}'s SliderIndicator has {thresholds.Length} thresholds but {thresholdColors.Length} threshold colors.");
        }
    }

    public void UpdateUI(float numerator, float denominator)
    {
        float percent = numerator / denominator;
        Color color = Color.clear;
        for (int i = 0; i < thresholds.Length; i++)
        {
            if (percent <= thresholds[i])
            {
                color = thresholdColors[i];
                break;
            }
        }
        if (sliders.Count > 0)
        {
            for (int i = 0; i < sliders.Count; i++)
            {
                sliders[i].value = percent;
                if (color != Color.clear)
                {
                    fillImages[i].color = color;
                }
            }
        }
        if (texts.Count > 0)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                switch(textType)
                {
                    case TextType.Raw:
                        texts[i].text = numerator.ToString(textFormat);
                        break;
                    case TextType.Percent:
                        texts[i].text = (percent * 100f).ToString(textFormat) + "%";
                        break;
                    case TextType.Decimal:
                        texts[i].text = percent.ToString(textFormat);
                        break;
                    case TextType.Division:
                        texts[i].text = numerator.ToString(textFormat) + " / " + denominator.ToString(textFormat);
                        break;
                }
                if (color != Color.clear)
                {
                    texts[i].color = color;
                }
            }
        }
    }

    public void AddSlider(Slider slider)
    {
        sliders.Add(slider);
        fillImages.Add(slider.fillRect.GetComponent<Image>());
    }

    public void AddText(TextMeshProUGUI text)
    {
        texts.Add(text);
    }
}
