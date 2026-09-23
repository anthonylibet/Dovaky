using System.Collections.Generic;
using Dovaky.Combat;
using Dovaky.Combat.Net;
using UnityEngine;

namespace Dovaky.Game
{
    /// <summary>
    /// Point d'entrée jouable : monte un combat de démonstration en mode
    /// « host » — le serveur autoritaire et le client local tournent dans le
    /// même processus, reliés par <see cref="LoopbackNetwork"/>.
    ///
    /// Pour passer en réseau réel, seul le transport change : on remplace
    /// LoopbackNetwork par une implémentation de <see cref="IServerTransport"/>
    /// et <see cref="IClientTransport"/> adossée à Mirror, Netcode for
    /// GameObjects ou des sockets. Le serveur, le client et les règles ne
    /// bougent pas.
    ///
    /// À poser sur un GameObject vide de la scène : les autres composants sont
    /// ajoutés automatiquement.
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        private const int JoueurLocal = 1;
        private const int JoueurAdverse = 2;
        private const int DagueId = 1;

        // Unity ne sérialise pas les entiers non signés : on stocke en long,
        // et le cœur reçoit la graine non signée qu'il attend.
        [SerializeField] private long seed = 20250919L;
        [SerializeField] private bool placeCamera = true;

        private BattleServer _server;
        private BattleClient _localClient;

        public BattleServer Server => _server;

        public BattleClient LocalClient => _localClient;

        private void Start()
        {
            var dague = new Spell(DagueId, "Dague", actionPointCost: 3, minRange: 1, maxRange: 3,
                minDamage: 6, maxDamage: 11);

            BattleMap carte = BattleMap.FromRows(
                "..........",
                "..........",
                "...oo.....",
                "..........",
                ".....##...",
                "..........",
                "..........",
                "..........");

            var heros = new Fighter(1, "Héros", teamId: 0, position: new Cell(1, 1),
                maxHealth: 60, maxActionPoints: 6, maxMovementPoints: 3, initiative: 12).WithSpell(dague);
            var ennemi = new Fighter(2, "Bouftou", teamId: 1, position: new Cell(8, 6),
                maxHealth: 45, maxActionPoints: 6, maxMovementPoints: 3, initiative: 8).WithSpell(dague);

            var battle = new Battle(carte, new[] { heros, ennemi }, (ulong)seed);

            var reseau = new LoopbackNetwork();
            _localClient = new BattleClient(reseau.ConnectClient(JoueurLocal), JoueurLocal);
            var clientAdverse = new BattleClient(reseau.ConnectClient(JoueurAdverse), JoueurAdverse);

            var proprietes = new Dictionary<int, IReadOnlyList<int>>
            {
                { JoueurLocal, new[] { heros.Id } },
                { JoueurAdverse, new[] { ennemi.Id } },
            };

            _server = new BattleServer(battle, reseau.Server, proprietes);
            _server.CommandRejected += (joueur, erreur) => Debug.Log("Refusé pour le joueur " + joueur + " : " + erreur);

            // La vue doit être branchée avant le démarrage : c'est la
            // photographie initiale qui construit la scène.
            BattleFieldView field = gameObject.AddComponent<BattleFieldView>();
            BattleView view = gameObject.AddComponent<BattleView>();
            view.Bind(_localClient);

            BattleInputController input = gameObject.AddComponent<BattleInputController>();
            input.Bind(_localClient, heros.Id);

            ScriptedOpponent opponent = gameObject.AddComponent<ScriptedOpponent>();
            opponent.Bind(clientAdverse, ennemi.Id, view);

            _server.Start();

            if (placeCamera) CentrerCamera(carte, field);

            Diagnostiquer(field);
            Debug.Log("Combat prêt : clic gauche pour se déplacer, clic droit pour attaquer, espace pour passer le tour.");
        }

        /// <summary>
        /// Trace de quoi identifier un écran vide sans avoir à deviner :
        /// pipeline actif, nombre de tuiles réellement construites, état de la
        /// caméra. Trois causes d'écran noir, trois lignes de console.
        /// </summary>
        private static void Diagnostiquer(BattleFieldView field)
        {
            Debug.Log("[Dovaky] Pipeline de rendu : " + IsoGrid.ActivePipelineName());

            if (field.TileCount == 0)
            {
                Debug.LogError("[Dovaky] Aucune tuile construite : le client n'a pas reçu la photographie du combat.");
            }
            else
            {
                Debug.Log("[Dovaky] Tuiles construites : " + field.TileCount);
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[Dovaky] Aucune caméra taguée MainCamera : la scène ne peut rien afficher.");
                return;
            }

            if (!camera.orthographic)
            {
                Debug.LogWarning("[Dovaky] La caméra n'est pas orthographique : la visée à la souris sera fausse.");
            }

            Debug.Log("[Dovaky] Caméra « " + camera.name + " » en " + camera.transform.position
                      + ", taille orthographique " + camera.orthographicSize);
        }

        private static void CentrerCamera(BattleMap carte, BattleFieldView field)
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 centre = field.WorldPositionOf(new Cell(carte.Width / 2, carte.Height / 2), z: 0f);
            camera.transform.position = new Vector3(centre.x, centre.y, camera.transform.position.z);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(carte.Width, carte.Height) * 0.35f;
        }
    }
}
