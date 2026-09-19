using System.Collections.Generic;
using Dovaky.Combat;
using Dovaky.Combat.Net;
using UnityEngine;

namespace Dovaky.Game
{
    /// <summary>
    /// Adversaire rudimentaire, juste assez pour que le combat se joue à deux
    /// en appuyant sur Play : il frappe si la cible est à portée, sinon il
    /// s'approche, puis passe son tour.
    ///
    /// Il agit depuis <c>Update</c> et non directement dans le callback
    /// d'événement : répondre pendant la diffusion ferait rentrer le serveur
    /// dans sa propre boucle d'envoi, et les clients recevraient des événements
    /// imbriqués.
    /// </summary>
    public sealed class ScriptedOpponent : MonoBehaviour
    {
        [SerializeField] private int spellId = 1;
        [SerializeField] private int spellMaxRange = 3;

        private BattleClient _client;
        private BattleView _view;
        private int _fighterId;
        private bool _shouldAct;

        public void Bind(BattleClient client, int fighterId, BattleView view)
        {
            _client = client;
            _fighterId = fighterId;
            _view = view;
            _client.EventReceived += OnEventReceived;
        }

        private void OnEventReceived(BattleEvent battleEvent)
        {
            if (battleEvent is TurnStarted started && started.FighterId == _fighterId) _shouldAct = true;
        }

        private void Update()
        {
            if (!_shouldAct || _client == null) return;
            if (_view != null && _view.IsBusy) return;
            if (_client.State.IsOver || _client.State.ActiveFighterId != _fighterId) return;

            _shouldAct = false;
            Act();
        }

        private void Act()
        {
            if (!_client.State.Fighters.TryGetValue(_fighterId, out ClientFighter moi)) return;

            ClientFighter cible = TrouverCible(moi);
            if (cible == null)
            {
                _client.Send(new EndTurnCommand(_fighterId));
                return;
            }

            if (Cell.Distance(moi.Position, cible.Position) <= spellMaxRange)
            {
                _client.Send(new CastSpellCommand(_fighterId, spellId, cible.Position));
                _client.Send(new EndTurnCommand(_fighterId));
                return;
            }

            Cell approche = MeilleureApproche(moi, cible);
            if (approche != moi.Position) _client.Send(new MoveCommand(_fighterId, approche));

            _client.Send(new EndTurnCommand(_fighterId));
        }

        private ClientFighter TrouverCible(ClientFighter moi)
        {
            ClientFighter meilleure = null;
            int meilleureDistance = int.MaxValue;

            foreach (ClientFighter autre in _client.State.Fighters.Values)
            {
                if (!autre.IsAlive || autre.TeamId == moi.TeamId) continue;

                int distance = Cell.Distance(moi.Position, autre.Position);
                if (distance >= meilleureDistance) continue;

                meilleureDistance = distance;
                meilleure = autre;
            }

            return meilleure;
        }

        /// <summary>Case accessible qui rapproche le plus de la cible.</summary>
        private Cell MeilleureApproche(ClientFighter moi, ClientFighter cible)
        {
            BattleMap carte = _client.State.BuildMap();
            var occupees = new List<Cell>();
            foreach (ClientFighter autre in _client.State.Fighters.Values)
            {
                if (autre.Id != moi.Id && autre.IsAlive) occupees.Add(autre.Position);
            }

            Dictionary<Cell, int> atteignables = Pathfinder.ReachableCells(
                carte, moi.Position, moi.MovementPoints, occupees);

            Cell meilleure = moi.Position;
            int meilleureDistance = Cell.Distance(moi.Position, cible.Position);

            foreach (KeyValuePair<Cell, int> candidate in atteignables)
            {
                int distance = Cell.Distance(candidate.Key, cible.Position);
                if (distance < meilleureDistance)
                {
                    meilleureDistance = distance;
                    meilleure = candidate.Key;
                }
            }

            return meilleure;
        }

        private void OnDestroy()
        {
            if (_client != null) _client.EventReceived -= OnEventReceived;
        }
    }
}
