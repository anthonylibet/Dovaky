using System;
using System.Collections;
using System.Collections.Generic;
using Dovaky.Combat;
using Dovaky.Combat.Net;
using UnityEngine;

namespace Dovaky.Game
{
    /// <summary>
    /// Met en scène ce que le client reçoit du serveur.
    ///
    /// Les événements arrivent tous ensemble et l'état miroir est mis à jour
    /// immédiatement, alors que l'animation prend du temps : la file ci-dessous
    /// rejoue les faits l'un après l'autre. L'affichage a donc un léger retard
    /// sur l'état — c'est voulu, et c'est pourquoi les entrées joueur sont
    /// bloquées tant que la file n'est pas vide.
    /// </summary>
    [RequireComponent(typeof(BattleFieldView))]
    public sealed class BattleView : MonoBehaviour
    {
        [SerializeField] private float tileWidth = 1f;
        [SerializeField] private float tileHeight = 0.5f;
        [SerializeField] private float moveCellsPerSecond = 6f;
        [SerializeField] private float pauseBetweenEvents = 0.15f;

        private readonly Dictionary<int, FighterView> _fighterViews = new Dictionary<int, FighterView>();
        private readonly Queue<BattleEvent> _pending = new Queue<BattleEvent>();

        private BattleClient _client;
        private BattleFieldView _field;
        private Coroutine _playback;

        /// <summary>Vrai tant qu'il reste des événements à animer.</summary>
        public bool IsBusy => _playback != null || _pending.Count > 0;

        public BattleFieldView Field => _field;

        public float TileWidth => tileWidth;

        public float TileHeight => tileHeight;

        /// <summary>Déclenché après l'animation d'un événement, une fois la scène à jour.</summary>
        public event Action<BattleEvent> EventPlayed;

        private void Awake()
        {
            _field = GetComponent<BattleFieldView>();
        }

        public void Bind(BattleClient client)
        {
            if (_client != null)
            {
                _client.SnapshotApplied -= OnSnapshotApplied;
                _client.EventReceived -= OnEventReceived;
            }

            _client = client ?? throw new ArgumentNullException(nameof(client));
            _client.SnapshotApplied += OnSnapshotApplied;
            _client.EventReceived += OnEventReceived;

            if (_client.State.Width > 0) OnSnapshotApplied();
        }

        private void OnSnapshotApplied()
        {
            _field.Build(_client.State, tileWidth, tileHeight);

            foreach (FighterView view in _fighterViews.Values)
            {
                if (view != null) Destroy(view.gameObject);
            }

            _fighterViews.Clear();

            foreach (ClientFighter fighter in _client.State.Fighters.Values)
            {
                var go = new GameObject("Fighter " + fighter.Name);
                go.transform.SetParent(transform, worldPositionStays: false);

                FighterView view = go.AddComponent<FighterView>();
                view.Initialise(fighter.Id, fighter.Position, ColorForTeam(fighter.TeamId), _field);
                if (!fighter.IsAlive) view.SetDead();

                _fighterViews[fighter.Id] = view;
            }
        }

        private void OnEventReceived(BattleEvent battleEvent)
        {
            _pending.Enqueue(battleEvent);
            if (_playback == null) _playback = StartCoroutine(PlayPendingEvents());
        }

        private IEnumerator PlayPendingEvents()
        {
            while (_pending.Count > 0)
            {
                BattleEvent battleEvent = _pending.Dequeue();
                yield return Play(battleEvent);

                EventPlayed?.Invoke(battleEvent);

                if (pauseBetweenEvents > 0f) yield return new WaitForSeconds(pauseBetweenEvents);
            }

            _playback = null;
        }

        private IEnumerator Play(BattleEvent battleEvent)
        {
            if (battleEvent is FighterMoved moved)
            {
                if (_fighterViews.TryGetValue(moved.FighterId, out FighterView view))
                {
                    yield return view.MoveAlong(moved.Path, moveCellsPerSecond);
                }

                yield break;
            }

            if (battleEvent is FighterDied died)
            {
                if (_fighterViews.TryGetValue(died.FighterId, out FighterView view)) view.SetDead();
                yield break;
            }

            if (battleEvent is DamageTaken damage)
            {
                // À remplacer par un nombre flottant et un impact ; en attendant,
                // la console suffit à vérifier que la chaîne complète fonctionne.
                Debug.Log("Dégâts : " + damage.Amount + " sur le combattant " + damage.FighterId
                          + " (reste " + damage.HealthLeft + ")");
                yield break;
            }

            yield break;
        }

        public FighterView ViewOf(int fighterId)
        {
            return _fighterViews.TryGetValue(fighterId, out FighterView view) ? view : null;
        }

        private static Color ColorForTeam(int teamId)
        {
            return teamId == 0 ? new Color(0.35f, 0.75f, 1f) : new Color(1f, 0.45f, 0.4f);
        }

        private void OnDestroy()
        {
            if (_client == null) return;

            _client.SnapshotApplied -= OnSnapshotApplied;
            _client.EventReceived -= OnEventReceived;
        }
    }
}
