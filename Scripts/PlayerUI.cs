using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;

    public void UpdateScoreUI(int newValue, int oldValue)
    {
        scoreText.text = "Score: " + oldValue;
    }
}
