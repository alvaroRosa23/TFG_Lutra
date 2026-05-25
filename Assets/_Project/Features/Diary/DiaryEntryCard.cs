using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Diary
{
    public class DiaryEntryCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _dateLabel;
        [SerializeField] private TextMeshProUGUI _previewLabel;
        [SerializeField] private Image           _emotionDot;
        [SerializeField] private Button          _button;

        public Action<DiaryEntry> OnCardClicked;

        private DiaryEntry _entry;

        private void Awake()
        {
            _button?.onClick.AddListener(() => OnCardClicked?.Invoke(_entry));
        }

        private void OnDestroy()
        {
            _button?.onClick.RemoveAllListeners();
        }

        public void SetupCard(DiaryEntry entry, Color emotionColor)
        {
            _entry = entry;

            if (_titleLabel != null)
                _titleLabel.text = string.IsNullOrEmpty(entry.Title) ? "Sin título" : entry.Title;

            if (_dateLabel != null)
            {
                string dateStr = entry.Date.ToString("dddd, dd MMMM", new CultureInfo("es-ES"));
                if (!string.IsNullOrEmpty(dateStr))
                    dateStr = char.ToUpper(dateStr[0]) + dateStr.Substring(1);
                _dateLabel.text = dateStr;
            }

            if (_previewLabel != null)
                _previewLabel.text = entry.Content?.Length > 80
                    ? entry.Content.Substring(0, 80) + "..."
                    : entry.Content ?? "";

            if (_emotionDot != null)
                _emotionDot.color = emotionColor;
        }
    }
}
