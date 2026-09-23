# Dovaky

Projet Unity vide, prêt à être ouvert et versionné.

| | |
|---|---|
| Éditeur | Unity **6000.0** LTS (Unity 6) |
| Orientation | **2D** (`Default Behavior Mode` = 2D) |
| Rendu | **URP** (`com.unity.render-pipelines.universal` 17.0.3) |
| Entrées | Input System + Input Manager (`activeInputHandler` = Both) |

## Ouvrir le projet

1. Dans Unity Hub : **Add → Add project from disk**, sélectionner ce dossier.
2. Ouvrir avec un éditeur **6000.0.x**. Le Package Manager résout les packages au premier lancement (prévoir quelques minutes).

## Étape restante : créer le URP Asset 2D

Le package URP est installé, mais aucun *Render Pipeline Asset* n'est commité : ces fichiers
contiennent des références (GUID) vers des scripts internes au package URP, qui ne peuvent pas être
écrites correctement à la main. Unity les génère proprement en deux clics :

1. **Assets → Create → Rendering → URP Asset (with 2D Renderer)** — enregistrer dans `Assets/Settings/`.
2. **Edit → Project Settings → Graphics** : affecter l'asset créé à *Default Render Pipeline*.
3. **Edit → Project Settings → Quality** : affecter le même asset au niveau de qualité actif (`Render Pipeline Asset`).
4. Committer les fichiers générés (`Assets/Settings/*`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/QualitySettings.asset`).

Sans cette étape, le projet s'ouvre et fonctionne normalement — il utilise simplement le pipeline de rendu intégré.

## Arborescence

```
Assets/
  Scenes/
    SampleScene.unity      scène vide : Main Camera orthographique + AudioListener
  Scripts/
    Combat/                cœur de combat, C# pur (assembly Dovaky.Combat)
      Net/                 serveur autoritaire, client, protocole binaire
    Game/                  présentation Unity (assembly Dovaky.Game)
  Tests/
    EditMode/              tests NUnit du cœur, du réseau et de la projection
Packages/
  manifest.json            dépendances du projet
ProjectSettings/           réglages versionnés (version d'éditeur, 2D, physique 2D, tags, build)
.gitignore                 Library/, Temp/, Build/, Logs/, fichiers IDE…
.gitattributes             fins de ligne + UnityYAMLMerge pour les assets YAML
```

Unity génère au premier lancement les fichiers de réglages absents (`GraphicsSettings.asset`,
`QualitySettings.asset`, `InputManager.asset`, `AudioManager.asset`, …) avec leurs valeurs par
défaut. Ils sont à committer après la première ouverture.

## Architecture du combat

Le jeu vise du **multijoueur à serveur autoritaire**. La conséquence directe :
l'assembly `Dovaky.Combat` est du **C# pur, sans aucune référence à UnityEngine**
(`noEngineReferences: true`). La même simulation tourne dans l'éditeur, dans le
build client et dans un serveur dédié sans Unity.

Le cœur ne s'anime pas tout seul : il se pilote par **commandes**, et répond par
des **événements**.

```
            commande (intention, vient du réseau, non fiable)
client  ────────────────────────────────────────────────►  serveur
                                                              │ Battle.Execute
                                                              │  valide tout
        ◄────────────────────────────────────────────────────┘
            événements (faits accomplis) — ou un CommandError
```

- `Battle.Execute(BattleCommand)` valide intégralement la commande avant le
  moindre effet, et renvoie soit les événements produits, soit un
  `CommandError` (`NotYourTurn`, `NotEnoughActionPoints`, `NoLineOfSight`, …).
  Le client n'a pas à rejouer la règle pour savoir quoi afficher.
- Le client ne fait qu'**animer les événements** reçus (`FighterMoved`,
  `DamageTaken`, `FighterDied`, …) : aucun état de jeu ne s'y décide.
- La simulation est **déterministe à graine égale** : ordre des voisins fixe
  dans le pathfinding, et générateur xorshift maison plutôt que `System.Random`,
  dont la suite n'est pas garantie identique d'un runtime .NET à l'autre.

### Serveur, client et transport

`Dovaky.Combat.Net` implémente cette boucle, toujours sans Unity :

| Type | Rôle |
|---|---|
| `BattleServer` | autorité : vérifie **la propriété du combattant** puis délègue les règles à `Battle`, diffuse les événements, refuse en privé |
| `BattleClient` + `ClientBattleState` | miroir du combat, reconstruit depuis une photographie initiale puis les événements |
| `BattleCodec` | protocole binaire ; tout message tronqué ou d'un type inconnu ressort en `ProtocolException`, jamais en exception de bas niveau |
| `IServerTransport` / `IClientTransport` | la seule surface à réimplémenter pour un vrai réseau |
| `LoopbackNetwork` | transport en mémoire : tests, et mode « host » où serveur et client cohabitent |

Deux contrôles vivent dans le serveur et nulle part ailleurs : **ce joueur
pilote-t-il bien ce combattant**, et **ce message est-il exploitable**. Tout le
reste appartient à `Battle`. Un client bricolé n'obtient donc rien de plus
qu'un client honnête.

Brancher **Mirror**, **Netcode for GameObjects** ou des sockets ne demande
qu'une implémentation des deux interfaces de transport : ni le serveur, ni le
client, ni les règles ne bougent. Ces packages ne sont volontairement pas
encore installés — le choix reste ouvert, et rien dans le code n'en dépend.

## Présentation Unity

`Dovaky.Game` met en scène ce que le client reçoit. Aucun asset d'art n'est
nécessaire : la grille et les combattants sont des losanges générés en mesh à
l'exécution, pour que le projet soit jouable avant d'avoir des graphismes.

| Composant | Rôle |
|---|---|
| `IsoGrid` | case ↔ monde ↔ écran, en s'appuyant sur `IsoProjection` (testé côté C# pur) |
| `BattleFieldView` | tuiles du décor et surbrillance de la zone de déplacement |
| `FighterView` | un combattant, et son déplacement case par case |
| `BattleView` | rejoue les événements **un par un** : l'état miroir est instantané, l'animation non |
| `BattleInputController` | clic gauche déplacer, clic droit attaquer, espace passer le tour |
| `ScriptedOpponent` | adversaire rudimentaire, pour jouer le combat en solo dès Play |
| `BattleBootstrap` | monte un combat de démonstration en mode host ; déjà posé sur l'objet `Battle` de `SampleScene` |

La zone de déplacement affichée est calculée en local, mais ce n'est qu'un
confort : le serveur peut refuser la commande, et c'est son refus qui est
affiché.

Le rendu passe par des `SpriteRenderer` et non par des meshes avec un shader
cherché par son nom. C'est délibéré : `Sprites/Default` existe en URP mais n'a
pas de passe `Universal2D`, donc le 2D Renderer ne le dessine pas — de quoi
obtenir un écran noir sans la moindre erreur en console. Un `SpriteRenderer`
reçoit automatiquement le matériau sprite du pipeline actif, qu'on soit en
built-in, en URP 3D ou en URP 2D.

Au démarrage, `BattleBootstrap` écrit trois diagnostics préfixés `[Dovaky]` :
pipeline de rendu actif, nombre de tuiles construites, état de la caméra. Si
l'écran reste vide, ces lignes disent laquelle des trois causes est en jeu —
et leur absence totale signifie que la scène ouverte n'est pas `SampleScene`.

Les entrées utilisent l'ancien `Input` (le projet active les deux systèmes
d'entrée), et la caméra doit rester orthographique et de face pour que la
visée à la souris tombe juste.

### Règles couvertes

Grille carrée affichée en isométrique, 4 directions de déplacement · PA/PM
restaurés en début de tour · ordre du tour par initiative · déplacement au plus
court (1 PM par case, les combattants bloquent le passage) · sorts avec coût en
PA, portée min/max, ligne de vue, relance et limite de lancers par tour ·
dégâts, mort, fin de combat quand une seule équipe reste debout.

Pas encore là : zones d'effet, états et buffs, invocations, et un transport
réseau réel (seul le transport en mémoire existe).

### Lancer les tests

Dans Unity : **Window → General → Test Runner → EditMode → Run All** (63 tests).

Comme le cœur ne dépend pas de Unity, ces mêmes tests peuvent aussi être
compilés et exécutés par un simple `dotnet` en dehors de l'éditeur, ce qui est
la voie prévue pour une CI.

## Conventions de versionnage

- Sérialisation **Force Text** et **Visible Meta Files** : les assets fusionnent en texte.
- Pour des fusions propres sur les `.unity` / `.prefab`, configurer *UnityYAMLMerge* comme
  `mergetool` Git (voir la doc Unity « Smart merge »).
- Git LFS n'est pas activé ; les règles binaires sont dans `.gitattributes` et peuvent être
  converties en `filter=lfs` si le projet accumule des assets lourds.
