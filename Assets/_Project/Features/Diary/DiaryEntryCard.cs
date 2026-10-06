using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Diary
{
    /// <summary>
    /// Tarjeta de una entrada en la lista del diario. La tarjeta no es pulsable: la edición y el
    /// borrado se hacen con sus dos botones, a la izquierda del punto de emoción.
    /// </summary>
    public class DiaryEntryCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _dateLabel;
        [SerializeField] private TextMeshProUGUI _previewLabel;
        [SerializeField] private Image           _emotionDot;
        [SerializeField] private Button          _editButton;
        [SerializeField] private Button          _deleteButton;

        public Action<DiaryEntry> OnEditClicked;
        public Action<DiaryEntry> OnDeleteClicked;

        private DiaryEntry _entry;

        private void Awake()
        {
            _editButton?.onClick.AddListener(() => OnEditClicked?.Invoke(_entry));
            _deleteButton?.onClick.AddListener(() => OnDeleteClicked?.Invoke(_entry));
        }

        private void OnDestroy()
        {
            _editButton?.onClick.RemoveAllListeners();
            _deleteButton?.onClick.RemoveAllListeners();
        }

        public void SetupCard(DiaryEntry entry, Color emotionColor)
        {
            _entry = entry;

            if (_titleLabel != null)
                _titleLabel.text = string.IsNullOrEmpty(entry.Title) ? "Sin título" : entry.Title;

            if (_dateLabel != null)
            {
                // Con el año si la entrada no es de este año
                string format  = entry.Date.Year == DateTime.Today.Year ? "dddd, dd MMMM" : "dddd, dd MMMM yyyy";
                string dateStr = entry.Date.ToString(format, new CultureInfo("es-ES"));
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
