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
Packages/
  manifest.json            dépendances du projet
ProjectSettings/           réglages versionnés (version d'éditeur, 2D, physique 2D, tags, build)
.gitignore                 Library/, Temp/, Build/, Logs/, fichiers IDE…
.gitattributes             fins de ligne + UnityYAMLMerge pour les assets YAML
```

Unity génère au premier lancement les fichiers de réglages absents (`GraphicsSettings.asset`,
`QualitySettings.asset`, `InputManager.asset`, `AudioManager.asset`, …) avec leurs valeurs par
défaut. Ils sont à committer après la première ouverture.

## Conventions de versionnage

- Sérialisation **Force Text** et **Visible Meta Files** : les assets fusionnent en texte.
- Pour des fusions propres sur les `.unity` / `.prefab`, configurer *UnityYAMLMerge* comme
  `mergetool` Git (voir la doc Unity « Smart merge »).
- Git LFS n'est pas activé ; les règles binaires sont dans `.gitattributes` et peuvent être
  converties en `filter=lfs` si le projet accumule des assets lourds.
