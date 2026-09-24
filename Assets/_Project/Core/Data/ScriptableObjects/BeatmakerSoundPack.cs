using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Data.ScriptableObjects
{
    /// <summary>
    /// Pack de sonidos y tempo del minijuego Beatmaker. Cada pack define su propio BPM
    /// y el usuario puede cambiar de pack en mitad de la partida con el desplegable.
    ///
    /// Cada estilo (Kick/Hat/Clap/Snare) tiene 2 variaciones; cada variación es una fila
    /// (pista) de 8 steps en el secuenciador. Igual que en FL Studio, cada cuadrado es un
    /// step (semicorchea) y 4 steps = 1 beat, así que el patrón completo dura 2 beats y cada
    /// step dura 60 / (bpm * StepsPerBeat) segundos.
    /// </summary>
    [CreateAssetMenu(menuName = "Lutra/Beatmaker Sound Pack", fileName = "BeatmakerSoundPack")]
    public class BeatmakerSoundPack : ScriptableObject
    {
        public const int VariationsPerInstrument = 2;

        /// <summary>Steps (cuadrados) por beat, como en el Channel Rack de FL Studio.</summary>
        public const int StepsPerBeat = 4;

        public string packName;

        [Range(60, 160)]
        public int bpm = 90;

        [Header("2 variaciones por instrumento ([0] = fila 1, [1] = fila 2)")]
        public AudioClip[] kickClips  = new AudioClip[VariationsPerInstrument];
        public AudioClip[] hatClips   = new AudioClip[VariationsPerInstrument];
        public AudioClip[] clapClips  = new AudioClip[VariationsPerInstrument];
        public AudioClip[] snareClips = new AudioClip[VariationsPerInstrument];

        [Header("Loops instrumentales (solo uno activo; se reinician en el step 1)")]
        [Tooltip("El loop se reinicia en cada vuelta del patrón: 8 steps = 2 beats (a 80 BPM, 1,5 s).")]
        public AudioClip[] loopClips = new AudioClip[2];
        public string[]    loopNames = new string[2];

        /// <summary>Duración de un step (un cuadrado del secuenciador) en segundos.</summary>
        public double SecondsPerStep => 60.0 / (Mathf.Max(1, bpm) * StepsPerBeat);

        /// <summary>Clip de una variación (0-1) de un instrumento.</summary>
        public AudioClip GetClip(BeatmakerInstrument instrument, int variation)
        {
            AudioClip[] clips = instrument switch
            {
                BeatmakerInstrument.Kick  => kickClips,
                BeatmakerInstrument.Hat   => hatClips,
                BeatmakerInstrument.Clap  => clapClips,
                BeatmakerInstrument.Snare => snareClips,
                _ => null
            };

            if (clips == null || variation < 0 || variation >= clips.Length) return null;
            return clips[variation];
        }
    }
}
