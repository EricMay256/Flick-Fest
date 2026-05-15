using System.IO;
using FlickFest.Core;
using FlickFest.Presentation;
using TMPro;
using UBear.Leaderboard;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlickFest.Editor
{
  /// <summary>
  /// Builds the Flick Fest demo scene programmatically: configs, prefabs,
  /// gameobjects, references wired up. Idempotent — re-running the menu
  /// item recreates assets in place and overwrites the scene file.
  /// </summary>
  public static class FlickFestSceneBuilder
  {
    private const string ScenePath = "Assets/Scenes/FlickFest.unity";
    private const string GeneratedRoot = "Assets/Generated";
    private const string ModesDir = GeneratedRoot + "/GameModes";
    private const string PrefabsDir = GeneratedRoot + "/Prefabs";
    private const string TargetSpritePath = GeneratedRoot + "/TargetSprite.png";
    private const string LeaderboardConfigPath = GeneratedRoot + "/LeaderboardConfig.asset";
    private const string TargetPrefabPath = PrefabsDir + "/Target.prefab";
    private const string ModeButtonPrefabPath = PrefabsDir + "/ModeButton.prefab";
    private const string LeaderboardRowPrefabPath = PrefabsDir + "/LeaderboardRow.prefab";

    [MenuItem("FlickFest/Build Demo Scene")]
    public static void BuildDemoScene()
    {
      EnsureFolder("Assets/Scenes");
      EnsureFolder(GeneratedRoot);
      EnsureFolder(ModesDir);
      EnsureFolder(PrefabsDir);

      Sprite targetSprite = CreateOrLoadTargetSprite();
      LeaderboardConfig leaderboardConfig = CreateOrLoadLeaderboardConfig();
      GameModeDefinition precision = CreateOrLoadMode("Precision", BuildPrecision);
      GameModeDefinition blitz = CreateOrLoadMode("Blitz", BuildBlitz);
      GameModeDefinition flood = CreateOrLoadMode("Flood", BuildFlood);

      GameObject targetPrefab = BuildTargetPrefab(targetSprite);
      GameObject modeButtonPrefab = BuildModeButtonPrefab();
      GameObject leaderboardRowPrefab = BuildLeaderboardRowPrefab();

      Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
      scene.name = "FlickFest";

      BuildSceneContents(
          scene,
          leaderboardConfig,
          new[] { precision, blitz, flood },
          targetPrefab,
          modeButtonPrefab,
          leaderboardRowPrefab,
          targetSprite);

      EditorSceneManager.SaveScene(scene, ScenePath);
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
      Debug.Log($"[FlickFestSceneBuilder] Scene saved to {ScenePath}.");
    }

    #region Asset Generation

    private static Sprite CreateOrLoadTargetSprite()
    {
      var existing = AssetDatabase.LoadAssetAtPath<Sprite>(TargetSpritePath);
      if (existing != null)
      {
        return existing;
      }

      const int size = 128;
      var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
      var center = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
      float radius = size * 0.5f;
      for (int y = 0; y < size; y++)
      {
        for (int x = 0; x < size; x++)
        {
          float d = Vector2.Distance(new Vector2(x, y), center);
          float t = Mathf.Clamp01(radius - d);
          var c = new Color(1f, 1f, 1f, t > 0.5f ? 1f : 0f);
          tex.SetPixel(x, y, c);
        }
      }
      tex.Apply();

      File.WriteAllBytes(TargetSpritePath, tex.EncodeToPNG());
      Object.DestroyImmediate(tex);
      AssetDatabase.ImportAsset(TargetSpritePath, ImportAssetOptions.ForceSynchronousImport);

      var importer = (TextureImporter)AssetImporter.GetAtPath(TargetSpritePath);
      importer.textureType = TextureImporterType.Sprite;
      importer.spriteImportMode = SpriteImportMode.Single;
      importer.spritePixelsPerUnit = size;
      importer.alphaIsTransparency = true;
      importer.SaveAndReimport();

      return AssetDatabase.LoadAssetAtPath<Sprite>(TargetSpritePath);
    }

    private static LeaderboardConfig CreateOrLoadLeaderboardConfig()
    {
      var existing = AssetDatabase.LoadAssetAtPath<LeaderboardConfig>(LeaderboardConfigPath);
      if (existing != null)
      {
        return existing;
      }
      var config = ScriptableObject.CreateInstance<LeaderboardConfig>();
      config.BaseUrl = "https://your-app.example.com";
      config.GameModes = new[] { "precision", "blitz", "flood" };
      AssetDatabase.CreateAsset(config, LeaderboardConfigPath);
      return config;
    }

    private static GameModeDefinition CreateOrLoadMode(string fileName, System.Action<SerializedObject> configure)
    {
      string path = $"{ModesDir}/{fileName}.asset";
      var existing = AssetDatabase.LoadAssetAtPath<GameModeDefinition>(path);
      if (existing == null)
      {
        existing = ScriptableObject.CreateInstance<GameModeDefinition>();
        AssetDatabase.CreateAsset(existing, path);
      }

      var so = new SerializedObject(existing);
      configure(so);
      so.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(existing);
      return existing;
    }

    private static void BuildPrecision(SerializedObject so)
    {
      so.FindProperty("_gameModeName").stringValue = "precision";
      so.FindProperty("_displayLabel").stringValue = "Precision";
      so.FindProperty("_endCondition").enumValueIndex = (int)EndCondition.TimeLimit;
      so.FindProperty("_duration").floatValue = 30f;
      so.FindProperty("_targetGoal").intValue = 20;
      so.FindProperty("_maxActiveTargets").intValue = 1;
      so.FindProperty("_spawnInterval").floatValue = 0.5f;
      so.FindProperty("_targetLifetime").floatValue = 2.0f;
      so.FindProperty("_targetRadius").floatValue = 0.05f;
      so.FindProperty("_hitPoints").intValue = 100;
      so.FindProperty("_missPenalty").intValue = 50;
      so.FindProperty("_negativePenalty").intValue = 0;
      so.FindProperty("_comboEnabled").boolValue = true;
      so.FindProperty("_negativeTargetChance").floatValue = 0f;
    }

    private static void BuildBlitz(SerializedObject so)
    {
      so.FindProperty("_gameModeName").stringValue = "blitz";
      so.FindProperty("_displayLabel").stringValue = "Blitz";
      so.FindProperty("_endCondition").enumValueIndex = (int)EndCondition.TargetCount;
      so.FindProperty("_duration").floatValue = 30f;
      so.FindProperty("_targetGoal").intValue = 20;
      so.FindProperty("_maxActiveTargets").intValue = 1;
      so.FindProperty("_spawnInterval").floatValue = 0.5f;
      so.FindProperty("_targetLifetime").floatValue = 2.0f;
      so.FindProperty("_targetRadius").floatValue = 0.05f;
      so.FindProperty("_hitPoints").intValue = 0;
      so.FindProperty("_missPenalty").intValue = 0;
      so.FindProperty("_negativePenalty").intValue = 0;
      so.FindProperty("_comboEnabled").boolValue = false;
      so.FindProperty("_negativeTargetChance").floatValue = 0f;
    }

    private static void BuildFlood(SerializedObject so)
    {
      so.FindProperty("_gameModeName").stringValue = "flood";
      so.FindProperty("_displayLabel").stringValue = "Flood";
      so.FindProperty("_endCondition").enumValueIndex = (int)EndCondition.TimeLimit;
      so.FindProperty("_duration").floatValue = 30f;
      so.FindProperty("_targetGoal").intValue = 20;
      so.FindProperty("_maxActiveTargets").intValue = 6;
      so.FindProperty("_spawnInterval").floatValue = 0.5f;
      so.FindProperty("_targetLifetime").floatValue = 1.5f;
      so.FindProperty("_targetRadius").floatValue = 0.05f;
      so.FindProperty("_hitPoints").intValue = 100;
      so.FindProperty("_missPenalty").intValue = 50;
      so.FindProperty("_negativePenalty").intValue = 200;
      so.FindProperty("_comboEnabled").boolValue = true;
      so.FindProperty("_negativeTargetChance").floatValue = 0.18f;
    }

    private static GameObject BuildTargetPrefab(Sprite sprite)
    {
      var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);
      if (existing != null)
      {
        return existing;
      }

      var go = new GameObject("Target");
      var renderer = go.AddComponent<SpriteRenderer>();
      renderer.sprite = sprite;
      renderer.sortingOrder = 10;
      var collider = go.AddComponent<CircleCollider2D>();
      collider.radius = 0.5f;
      go.AddComponent<TargetView>();

      var prefab = PrefabUtility.SaveAsPrefabAsset(go, TargetPrefabPath);
      Object.DestroyImmediate(go);
      return prefab;
    }

    private static GameObject BuildModeButtonPrefab()
    {
      var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ModeButtonPrefabPath);
      if (existing != null)
      {
        return existing;
      }

      var go = new GameObject("ModeButton", typeof(RectTransform));
      var rect = go.GetComponent<RectTransform>();
      rect.sizeDelta = new Vector2(180, 60);

      var image = go.AddComponent<Image>();
      image.color = new Color(0.2f, 0.25f, 0.32f, 1f);

      var button = go.AddComponent<Button>();
      var colors = button.colors;
      colors.normalColor = new Color(1f, 1f, 1f, 1f);
      colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
      colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
      button.colors = colors;

      var modeButton = go.AddComponent<ModeButton>();
      UnityEventTools.AddPersistentListener(button.onClick, modeButton.OnClick);

      var labelGo = CreateTmpChild(go.transform, "Label", "Mode", 28, TextAlignmentOptions.Center, stretch: true);
      SetSerializedReference(modeButton, "_label", labelGo.GetComponent<TextMeshProUGUI>());

      var prefab = PrefabUtility.SaveAsPrefabAsset(go, ModeButtonPrefabPath);
      Object.DestroyImmediate(go);
      return prefab;
    }

    private static GameObject BuildLeaderboardRowPrefab()
    {
      var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LeaderboardRowPrefabPath);
      if (existing != null)
      {
        return existing;
      }

      var go = new GameObject("LeaderboardRow", typeof(RectTransform));
      var rect = go.GetComponent<RectTransform>();
      rect.sizeDelta = new Vector2(500, 36);

      var row = go.AddComponent<LeaderboardRow>();

      GameObject rank = CreateTmpChild(go.transform, "Rank", "#1", 22, TextAlignmentOptions.Left);
      SetRect(rank, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(80, 36));

      GameObject player = CreateTmpChild(go.transform, "Player", "Player", 22, TextAlignmentOptions.Left);
      SetRect(player, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(240, 36));

      GameObject score = CreateTmpChild(go.transform, "Score", "0", 22, TextAlignmentOptions.Right);
      SetRect(score, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-60, 0), new Vector2(140, 36));

      SetSerializedReference(row, "_rankLabel", rank.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(row, "_playerLabel", player.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(row, "_scoreLabel", score.GetComponent<TextMeshProUGUI>());

      var prefab = PrefabUtility.SaveAsPrefabAsset(go, LeaderboardRowPrefabPath);
      Object.DestroyImmediate(go);
      return prefab;
    }

    #endregion
    #region Scene Construction

    private static void BuildSceneContents(
        Scene scene,
        LeaderboardConfig leaderboardConfig,
        GameModeDefinition[] modes,
        GameObject targetPrefab,
        GameObject modeButtonPrefab,
        GameObject leaderboardRowPrefab,
        Sprite targetSprite)
    {
      GameObject camera = BuildCamera();
      SceneManager.MoveGameObjectToScene(camera, scene);

      GameObject leaderboardService = BuildLeaderboardService(leaderboardConfig);
      SceneManager.MoveGameObjectToScene(leaderboardService, scene);

      GameObject session = BuildGameSession(leaderboardService.GetComponent<LeaderboardService>());
      SceneManager.MoveGameObjectToScene(session, scene);

      GameObject playArea = BuildPlayArea(session.GetComponent<GameSession>(), targetPrefab, targetSprite);
      SceneManager.MoveGameObjectToScene(playArea, scene);

      GameObject canvas = BuildUiCanvas(
          session.GetComponent<GameSession>(),
          leaderboardService.GetComponent<LeaderboardService>(),
          modes,
          modeButtonPrefab,
          leaderboardRowPrefab);
      SceneManager.MoveGameObjectToScene(canvas, scene);

      GameObject eventSystem = BuildEventSystem();
      SceneManager.MoveGameObjectToScene(eventSystem, scene);
    }

    private static GameObject BuildCamera()
    {
      var go = new GameObject("Main Camera");
      go.tag = "MainCamera";
      var cam = go.AddComponent<Camera>();
      cam.clearFlags = CameraClearFlags.SolidColor;
      cam.backgroundColor = new Color(0.07f, 0.09f, 0.13f, 1f);
      cam.orthographic = true;
      cam.orthographicSize = 5f;
      cam.transform.position = new Vector3(0, 0, -10);
      go.AddComponent<AudioListener>();
      return go;
    }

    private static GameObject BuildLeaderboardService(LeaderboardConfig config)
    {
      var go = new GameObject("LeaderboardService");
      var service = go.AddComponent<LeaderboardService>();
      SetSerializedReference(service, "_config", config);
      return go;
    }

    private static GameObject BuildGameSession(LeaderboardService service)
    {
      var go = new GameObject("GameSession");
      var session = go.AddComponent<GameSession>();
      SetSerializedReference(session, "_leaderboardService", service);
      return go;
    }

    private static GameObject BuildPlayArea(GameSession session, GameObject targetPrefab, Sprite targetSprite)
    {
      var go = new GameObject("PlayArea");
      go.transform.position = Vector3.zero;

      var renderer = go.AddComponent<SpriteRenderer>();
      renderer.sprite = targetSprite;
      renderer.color = new Color(0.12f, 0.14f, 0.18f, 1f);
      renderer.sortingOrder = -10;
      go.transform.localScale = new Vector3(14f, 8f, 1f);

      var collider = go.AddComponent<BoxCollider2D>();
      collider.size = new Vector2(1f, 1f);

      var spawn = go.AddComponent<SpawnManager>();
      SetSerializedReference(spawn, "_session", session);
      SetSerializedReference(spawn, "_targetPrefab", targetPrefab);

      var misclick = go.AddComponent<MisclickCatcher>();
      SetSerializedReference(misclick, "_session", session);

      var router = go.AddComponent<ClickRouter>();
      SetSerializedReference(router, "_session", session);
      SetSerializedReference(router, "_misclickCatcher", misclick);

      return go;
    }

    private static GameObject BuildEventSystem()
    {
      // InputSystemUIInputModule pairs with the new Input System; using
      // StandaloneInputModule throws under activeInputHandler = 1.
      var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
      return go;
    }

    private static GameObject BuildUiCanvas(
        GameSession session,
        LeaderboardService service,
        GameModeDefinition[] modes,
        GameObject modeButtonPrefab,
        GameObject leaderboardRowPrefab)
    {
      var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
      var canvas = canvasGo.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 100;
      var scaler = canvasGo.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920, 1080);
      scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
      scaler.matchWidthOrHeight = 0.5f;

      BuildHud(canvasGo.transform, session);
      MainMenu menu = BuildMainMenu(canvasGo.transform, session, service, modes, modeButtonPrefab);
      BuildGameOverPanel(canvasGo.transform, session, leaderboardRowPrefab, menu);

      return canvasGo;
    }

    private static void BuildHud(Transform parent, GameSession session)
    {
      var hudGo = new GameObject("HUD", typeof(RectTransform));
      hudGo.transform.SetParent(parent, worldPositionStays: false);
      StretchFill(hudGo.GetComponent<RectTransform>());
      var hud = hudGo.AddComponent<HUD>();
      SetSerializedReference(hud, "_session", session);

      GameObject scoreLabel = CreateTmpChild(hudGo.transform, "ScoreLabel", "0", 56, TextAlignmentOptions.Center);
      SetRect(scoreLabel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(400, 80));

      GameObject comboLabel = CreateTmpChild(hudGo.transform, "ComboLabel", "x2", 40, TextAlignmentOptions.Center);
      SetRect(comboLabel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(300, 50));

      GameObject timerLabel = CreateTmpChild(hudGo.transform, "TimerLabel", "30.0", 48, TextAlignmentOptions.Right);
      SetRect(timerLabel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-160, -80), new Vector2(280, 80));

      GameObject countdownLabel = CreateTmpChild(hudGo.transform, "CountdownLabel", "3", 200, TextAlignmentOptions.Center);
      SetRect(countdownLabel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 240));

      GameObject modeLabel = CreateTmpChild(hudGo.transform, "ModeLabel", "Precision", 36, TextAlignmentOptions.Left);
      SetRect(modeLabel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160, -80), new Vector2(360, 80));

      SetSerializedReference(hud, "_scoreLabel", scoreLabel.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(hud, "_comboLabel", comboLabel.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(hud, "_timerLabel", timerLabel.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(hud, "_countdownLabel", countdownLabel.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(hud, "_modeLabel", modeLabel.GetComponent<TextMeshProUGUI>());

      scoreLabel.SetActive(false);
      comboLabel.SetActive(false);
      timerLabel.SetActive(false);
      countdownLabel.SetActive(false);
      modeLabel.SetActive(false);
    }

    private static MainMenu BuildMainMenu(
        Transform parent,
        GameSession session,
        LeaderboardService service,
        GameModeDefinition[] modes,
        GameObject modeButtonPrefab)
    {
      var hostGo = new GameObject("MainMenuHost", typeof(RectTransform));
      hostGo.transform.SetParent(parent, worldPositionStays: false);
      StretchFill(hostGo.GetComponent<RectTransform>());
      var menu = hostGo.AddComponent<MainMenu>();

      var panelGo = new GameObject("Panel", typeof(RectTransform));
      panelGo.transform.SetParent(hostGo.transform, worldPositionStays: false);
      StretchFill(panelGo.GetComponent<RectTransform>());
      var bg = panelGo.AddComponent<Image>();
      bg.color = new Color(0f, 0f, 0f, 0.55f);

      GameObject title = CreateTmpChild(panelGo.transform, "Title", "Flick Fest", 120, TextAlignmentOptions.Center);
      SetRect(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(900, 180));

      GameObject status = CreateTmpChild(panelGo.transform, "Status", "Connecting...", 28, TextAlignmentOptions.Center);
      SetRect(status, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -340), new Vector2(700, 50));

      var modeContainer = new GameObject("ModeContainer", typeof(RectTransform));
      modeContainer.transform.SetParent(panelGo.transform, worldPositionStays: false);
      SetRect(modeContainer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(700, 80));
      var modeLayout = modeContainer.AddComponent<HorizontalLayoutGroup>();
      modeLayout.spacing = 24;
      modeLayout.childAlignment = TextAnchor.MiddleCenter;
      modeLayout.childForceExpandHeight = true;
      modeLayout.childForceExpandWidth = false;

      GameObject playButton = CreatePrimaryButton(panelGo.transform, "PlayButton", "Play");
      SetRect(playButton, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(260, 80));
      UnityEventTools.AddPersistentListener(playButton.GetComponent<Button>().onClick, menu.OnPlayPressed);

      SetSerializedReference(menu, "_session", session);
      SetSerializedReference(menu, "_leaderboardService", service);
      SetSerializedReferenceArray(menu, "_modes", modes);
      SetSerializedReference(menu, "_modeButtonContainer", modeContainer.transform);
      SetSerializedReference(menu, "_modeButtonPrefab", modeButtonPrefab.GetComponent<ModeButton>());
      SetSerializedReference(menu, "_titleLabel", title.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(menu, "_statusLabel", status.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(menu, "_playButton", playButton);
      SetSerializedReference(menu, "_panelRoot", panelGo);
      return menu;
    }

    private static void BuildGameOverPanel(
        Transform parent,
        GameSession session,
        GameObject leaderboardRowPrefab,
        MainMenu mainMenu)
    {
      var hostGo = new GameObject("GameOverHost", typeof(RectTransform));
      hostGo.transform.SetParent(parent, worldPositionStays: false);
      StretchFill(hostGo.GetComponent<RectTransform>());
      var panel = hostGo.AddComponent<GameOverPanel>();

      var panelGo = new GameObject("Panel", typeof(RectTransform));
      panelGo.transform.SetParent(hostGo.transform, worldPositionStays: false);
      StretchFill(panelGo.GetComponent<RectTransform>());
      var bg = panelGo.AddComponent<Image>();
      bg.color = new Color(0f, 0f, 0f, 0.7f);

      GameObject final = CreateTmpChild(panelGo.transform, "FinalScore", "0", 110, TextAlignmentOptions.Center);
      SetRect(final, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(900, 160));

      GameObject context = CreateTmpChild(panelGo.transform, "ScoreContext", "Precision mode", 32, TextAlignmentOptions.Center);
      SetRect(context, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -280), new Vector2(900, 60));

      GameObject rank = CreateTmpChild(panelGo.transform, "Rank", "", 40, TextAlignmentOptions.Center);
      SetRect(rank, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-200, -360), new Vector2(360, 60));

      GameObject percentile = CreateTmpChild(panelGo.transform, "Percentile", "", 40, TextAlignmentOptions.Center);
      SetRect(percentile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(200, -360), new Vector2(360, 60));

      GameObject best = CreateTmpChild(panelGo.transform, "PersonalBest", "", 32, TextAlignmentOptions.Center);
      SetRect(best, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -430), new Vector2(700, 50));

      GameObject status = CreateTmpChild(panelGo.transform, "Status", "", 28, TextAlignmentOptions.Center);
      SetRect(status, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -490), new Vector2(700, 50));

      var lbContainer = new GameObject("LeaderboardContainer", typeof(RectTransform));
      lbContainer.transform.SetParent(panelGo.transform, worldPositionStays: false);
      SetRect(lbContainer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(640, 320));
      var v = lbContainer.AddComponent<VerticalLayoutGroup>();
      v.spacing = 4;
      v.childForceExpandHeight = false;
      v.childForceExpandWidth = true;
      v.childAlignment = TextAnchor.UpperCenter;

      GameObject lbError = CreateTmpChild(panelGo.transform, "LeaderboardError", "", 26, TextAlignmentOptions.Center);
      SetRect(lbError, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -220), new Vector2(700, 50));

      GameObject playAgain = CreatePrimaryButton(panelGo.transform, "PlayAgain", "Play Again");
      SetRect(playAgain, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160, 120), new Vector2(260, 80));
      UnityEventTools.AddPersistentListener(playAgain.GetComponent<Button>().onClick, panel.OnPlayAgainPressed);

      GameObject mainMenuBtn = CreatePrimaryButton(panelGo.transform, "MainMenuButton", "Main Menu");
      SetRect(mainMenuBtn, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160, 120), new Vector2(260, 80));
      UnityEventTools.AddPersistentListener(mainMenuBtn.GetComponent<Button>().onClick, panel.OnMainMenuButtonPressed);

      SetSerializedReference(panel, "_session", session);
      SetSerializedReference(panel, "_finalScoreLabel", final.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(panel, "_scoreContextLabel", context.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(panel, "_rankLabel", rank.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(panel, "_percentileLabel", percentile.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(panel, "_personalBestLabel", best.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(panel, "_statusLabel", status.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(panel, "_leaderboardContainer", lbContainer.transform);
      SetSerializedReference(panel, "_rowPrefab", leaderboardRowPrefab.GetComponent<LeaderboardRow>());
      SetSerializedReference(panel, "_leaderboardError", lbError.GetComponent<TextMeshProUGUI>());
      SetSerializedReference(panel, "_playAgainButton", playAgain);
      SetSerializedReference(panel, "_mainMenuButton", mainMenuBtn);
      SetSerializedReference(panel, "_mainMenu", mainMenu);
      SetSerializedReference(panel, "_panelRoot", panelGo);

      panelGo.SetActive(false);
    }

    #endregion
    #region UI Helpers

    private static GameObject CreatePrimaryButton(Transform parent, string name, string label)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, worldPositionStays: false);
      var image = go.AddComponent<Image>();
      image.color = new Color(0.25f, 0.45f, 0.75f, 1f);
      var button = go.AddComponent<Button>();
      var labelGo = CreateTmpChild(go.transform, "Label", label, 32, TextAlignmentOptions.Center, stretch: true);
      labelGo.GetComponent<TextMeshProUGUI>().color = Color.white;
      return go;
    }

    private static GameObject CreateTmpChild(
        Transform parent,
        string name,
        string text,
        int fontSize,
        TextAlignmentOptions alignment,
        bool stretch = false)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, worldPositionStays: false);
      var tmp = go.AddComponent<TextMeshProUGUI>();
      tmp.text = text;
      tmp.fontSize = fontSize;
      tmp.alignment = alignment;
      tmp.color = Color.white;
      tmp.raycastTarget = false;
      tmp.font = TMP_Settings.defaultFontAsset;
      if (stretch)
      {
        StretchFill(go.GetComponent<RectTransform>());
      }
      return go;
    }

    private static void StretchFill(RectTransform rect)
    {
      rect.anchorMin = Vector2.zero;
      rect.anchorMax = Vector2.one;
      rect.offsetMin = Vector2.zero;
      rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
      var rect = go.GetComponent<RectTransform>();
      rect.anchorMin = anchorMin;
      rect.anchorMax = anchorMax;
      rect.anchoredPosition = anchoredPos;
      rect.sizeDelta = size;
    }

    #endregion
    #region Serialization Helpers

    private static void SetSerializedReference(Object target, string field, Object value)
    {
      var so = new SerializedObject(target);
      so.FindProperty(field).objectReferenceValue = value;
      so.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(target);
    }

    private static void SetSerializedReferenceArray(Object target, string field, Object[] values)
    {
      var so = new SerializedObject(target);
      SerializedProperty list = so.FindProperty(field);
      list.arraySize = values.Length;
      for (int i = 0; i < values.Length; i++)
      {
        list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
      }
      so.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(target);
    }

    private static void EnsureFolder(string path)
    {
      if (AssetDatabase.IsValidFolder(path))
      {
        return;
      }
      string parent = Path.GetDirectoryName(path).Replace('\\', '/');
      string leaf = Path.GetFileName(path);
      if (!AssetDatabase.IsValidFolder(parent))
      {
        EnsureFolder(parent);
      }
      AssetDatabase.CreateFolder(parent, leaf);
    }

    #endregion
  }
}
