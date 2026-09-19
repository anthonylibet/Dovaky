using System.Collections.Generic;
using Dovaky.Combat;
using Dovaky.Combat.Net;
using UnityEngine;

namespace Dovaky.Game
{
    /// <summary>
    /// Entrées du joueur : clic gauche pour se déplacer, clic droit pour lancer
    /// le sort sélectionné, barre d'espace pour passer son tour.
    ///
    /// La zone de déplacement affichée est calculée en local, mais ce n'est
    /// qu'un confort d'affichage : la décision appartient au serveur, qui peut
    /// refuser la commande. Le refus est affiché tel qu'il arrive, sans que le
    /// client ait à rejouer la règle.
    /// </summary>
    [RequireComponent(typeof(BattleView))]
    public sealed class BattleInputController : MonoBehaviour
    {
        [SerializeField] private int controlledFighterId;
        [SerializeField] private int selectedSpellId = 1;

        private BattleView _view;
        private BattleClient _client;
        private Camera _camera;

        public void Bind(BattleClient client, int controlledFighter, Camera viewCamera = null)
        {
            _client = client;
            controlledFighterId = controlledFighter;
            _camera = viewCamera != null ? viewCamera : Camera.main;

            _client.CommandRejected += OnCommandRejected;
            _client.EventReceived += OnEventReceived;
            _client.SnapshotApplied += RefreshHighlight;
        }

        private void Awake()
        {
            _view = GetComponent<BattleView>();
        }

        private void Update()
        {
            if (_client == null || _camera == null) return;
            if (_view.IsBusy) return;
            if (_client.State.IsOver) return;
            if (_client.State.ActiveFighterId != controlledFighterId) return;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _client.Send(new EndTurnCommand(controlledFighterId));
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Cell cell = IsoGrid.ScreenToCell(_camera, Input.mousePosition, _view.TileWidth, _view.TileHeight);
                if (_client.State.Contains(cell)) _client.Send(new MoveCommand(controlledFighterId, cell));
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                Cell cell = IsoGrid.ScreenToCell(_camera, Input.mousePosition, _view.TileWidth, _view.TileHeight);
                if (_client.State.Contains(cell))
                {
                    _client.Send(new CastSpellCommand(controlledFighterId, selectedSpellId, cell));
                }
            }
        }

        private void OnEventReceived(BattleEvent battleEvent) => RefreshHighlight();

        /// <summary>Éclaire les cases où le combattant contrôlé peut encore aller.</summary>
        private void RefreshHighlight()
        {
            if (_client == null || _view.Field == null) return;

            if (_client.State.IsOver || _client.State.ActiveFighterId != controlledFighterId)
            {
                _view.Field.ClearHighlights();
                return;
            }

            if (!_client.State.Fighters.TryGetValue(controlledFighterId, out ClientFighter fighter))
            {
                _view.Field.ClearHighlights();
                return;
            }

            BattleMap map = _client.State.BuildMap();
            var occupees = new List<Cell>();
            foreach (ClientFighter other in _client.State.Fighters.Values)
            {
                if (other.Id != fighter.Id && other.IsAlive) occupees.Add(other.Position);
            }

            Dictionary<Cell, int> atteignables = Pathfinder.ReachableCells(
                map, fighter.Position, fighter.MovementPoints, occupees);
            atteignables.Remove(fighter.Position);

            _view.Field.Highlight(atteignables.Keys);
        }

        private void OnCommandRejected(CommandError error)
        {
            Debug.Log("Action refusée par le serveur : " + error);
        }

        private void OnDestroy()
        {
            if (_client == null) return;

            _client.CommandRejected -= OnCommandRejected;
            _client.EventReceived -= OnEventReceived;
            _client.SnapshotApplied -= RefreshHighlight;
        }
    }
}
