using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Minigames
{
    /// <summary>
    /// Estado puro (sin MonoBehaviour) del patrón rítmico de Beatmaker: una rejilla de
    /// 8 pistas x 8 steps (estilo FL Studio), más el loop instrumental activo (exclusivo).
    ///
    /// Pista = instrumento * VariationsPerInstrument + variación, en el orden del enum:
    /// 0 Kick 1, 1 Kick 2, 2 Hat 1, 3 Hat 2, 4 Clap 1, 5 Clap 2, 6 Snare 1, 7 Snare 2.
    /// </summary>
    public class BeatmakerPatternState
    {
        public const int StepCount       = 8;
        public const int BeatCount       = StepCount / BeatmakerSoundPack.StepsPerBeat; // 2 beats por patrón
        public const int InstrumentCount = 4;
        public const int TrackCount      = InstrumentCount * BeatmakerSoundPack.VariationsPerInstrument;

        private readonly bool[,] _steps = new bool[TrackCount, StepCount];
        private int _activeLoop = -1; // -1 = ningún loop activo
        private int _activeStepCount;

        public int ActiveLoopIndex => _activeLoop;

        /// <summary>Número total de notas (steps activos) en la rejilla.</summary>
        public int ActiveStepCount => _activeStepCount;

        public static BeatmakerInstrument InstrumentOf(int track)
            => (BeatmakerInstrument)(track / BeatmakerSoundPack.VariationsPerInstrument);

        public static int VariationOf(int track) => track % BeatmakerSoundPack.VariationsPerInstrument;

        public bool IsStepActive(int track, int step) => _steps[track, step];

        /// <summary>Invierte el estado de un step y devuelve el nuevo valor.</summary>
        public bool ToggleStep(int track, int step)
        {
            bool next = !_steps[track, step];
            _steps[track, step] = next;
            _activeStepCount += next ? 1 : -1;
            return next;
        }

        /// <summary>
        /// Cuántos estilos de instrumento distintos (0-4) suenan en el step indicado.
        /// Kick 1 y Kick 2 a la vez cuentan como un solo instrumento.
        /// </summary>
        public int ActiveInstrumentCountAt(int step)
        {
            if (step < 0 || step >= StepCount) return 0;

            int count = 0;
            for (int instrument = 0; instrument < InstrumentCount; instrument++)
            {
                for (int v = 0; v < BeatmakerSoundPack.VariationsPerInstrument; v++)
                {
                    if (!_steps[instrument * BeatmakerSoundPack.VariationsPerInstrument + v, step]) continue;
                    count++;
                    break;
                }
            }

            return count;
        }

        /// <summary>
        /// Activa/desactiva un loop de forma exclusiva: pulsar el loop activo lo apaga,
        /// pulsar otro lo sustituye. Devuelve el índice que queda activo (-1 si ninguno).
        /// </summary>
        public int SetActiveLoop(int loopIndex)
        {
            _activeLoop = (_activeLoop == loopIndex) ? -1 : loopIndex;
            return _activeLoop;
        }

        public bool HasAnyStepActive() => _activeStepCount > 0;
    }
}
