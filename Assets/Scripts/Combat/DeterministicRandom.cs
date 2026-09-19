namespace Dovaky.Combat
{
    /// <summary>
    /// Générateur pseudo-aléatoire xorshift64*, volontairement indépendant de
    /// <see cref="System.Random"/> : ce dernier ne garantit pas la même suite
    /// d'une implémentation .NET à l'autre, ce qui ferait diverger le serveur
    /// et les clients. Ici la suite ne dépend que de la graine.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            // L'état nul est un point fixe de xorshift : on le remplace.
            _state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;
        }

        /// <summary>État courant, à transmettre pour resynchroniser une simulation.</summary>
        public ulong State => _state;

        public ulong NextULong()
        {
            unchecked
            {
                _state ^= _state >> 12;
                _state ^= _state << 25;
                _state ^= _state >> 27;
                return _state * 2685821657736338717UL;
            }
        }

        /// <summary>Entier uniforme dans [minInclusive, maxInclusive].</summary>
        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;

            // Le biais du modulo est négligeable pour des intervalles de dégâts
            // (quelques dizaines de valeurs face à 2^64).
            ulong span = (ulong)(maxInclusive - minInclusive) + 1UL;
            return minInclusive + (int)(NextULong() % span);
        }
    }
}
