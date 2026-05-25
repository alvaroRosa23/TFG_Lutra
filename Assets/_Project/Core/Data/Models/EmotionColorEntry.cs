using System;
using UnityEngine;

namespace Lutra.Core.Data.Models
{
    [Serializable]
    public class EmotionColorEntry
    {
        public EmotionType emotionType;
        public Color       primaryColor    = Color.white;
        public Color       backgroundColor = Color.black;
    }
}
