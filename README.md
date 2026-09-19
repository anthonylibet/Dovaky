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
  Tests/
    EditMode/              tests NUnit du cœur de combat
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

Le choix du transport réseau (Mirror, Netcode for GameObjects, serveur custom)
reste ouvert : il s'agit de véhiculer des commandes et des événements, ce qui
ne change rien au cœur.

### Règles couvertes

Grille carrée affichée en isométrique, 4 directions de déplacement · PA/PM
restaurés en début de tour · ordre du tour par initiative · déplacement au plus
court (1 PM par case, les combattants bloquent le passage) · sorts avec coût en
PA, portée min/max, ligne de vue, relance et limite de lancers par tour ·
dégâts, mort, fin de combat quand une seule équipe reste debout.

Pas encore là : zones d'effet, états et buffs, invocations, couche de
présentation Unity, transport réseau.

### Lancer les tests

Dans Unity : **Window → General → Test Runner → EditMode → Run All** (37 tests).

Comme le cœur ne dépend pas de Unity, ces mêmes tests peuvent aussi être
compilés et exécutés par un simple `dotnet` en dehors de l'éditeur, ce qui est
la voie prévue pour une CI.

## Conventions de versionnage

- Sérialisation **Force Text** et **Visible Meta Files** : les assets fusionnent en texte.
- Pour des fusions propres sur les `.unity` / `.prefab`, configurer *UnityYAMLMerge* comme
  `mergetool` Git (voir la doc Unity « Smart merge »).
- Git LFS n'est pas activé ; les règles binaires sont dans `.gitattributes` et peuvent être
  converties en `filter=lfs` si le projet accumule des assets lourds.
