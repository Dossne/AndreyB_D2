using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class VoidBastionBootstrap : MonoBehaviour
{
    private const float MapWidth = 60f;
    private const float MapHeight = 44f;
    private const float CastleZoneRadius = 3.25f;
    private const float SprintDuration = 1.5f;
    private const float SprintCooldown = 5f;
    private const float WaveBreakDuration = 30f;
    private const float ResourceRespawnInterval = 2.5f;
    private const float CameraFollowHeight = 12f;
    private const float CameraFollowDepthOffset = -12f;
    private const float HoleCenterY = 0.15f;
    private const float GroundSurfaceY = -0.05f;
    private const int ResourceSpawnAttempts = 24;
    private const int TotalWaves = 15;

    private static readonly Vector3 CastlePosition = new Vector3(1.5f, 0f, 0f);
    private static readonly Vector3 HoleStartPosition = new Vector3(4.5f, HoleCenterY, -1.5f);

    private readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();
    private readonly List<ResourceNode> resourceNodes = new List<ResourceNode>();
    private readonly List<EnemyUnit> enemies = new List<EnemyUnit>();
    private readonly List<DefenseTurret> turrets = new List<DefenseTurret>();
    private readonly List<TemporaryVisual> temporaryVisuals = new List<TemporaryVisual>();
    private readonly List<Bounds> mountainBounds = new List<Bounds>();
    private readonly List<Vector3> roadWaypoints = new List<Vector3>();

    private Camera mainCamera;
    private Transform holeTransform;
    private Transform castleTransform;
    private Transform worldRoot;
    private Canvas uiCanvas;
    private GameObject menuPanel;
    private GameObject hudPanel;
    private GameObject upgradePanel;
    private GameObject endPanel;
    private Text resourceText;
    private Text statusText;
    private Text waveText;
    private Text castleHpText;
    private Text endTitleText;
    private Text endBodyText;
    private Button sprintButton;
    private Text sprintButtonText;
    private Button castleUpgradeButton;
    private Button holeUpgradeButton;
    private Button towerBuildButton;
    private Text castleUpgradeButtonText;
    private Text holeUpgradeButtonText;
    private Text towerBuildButtonText;

    private bool soundEnabled = true;
    private bool gameStarted;
    private bool gameEnded;
    private bool isDragging;
    private bool sprintActive;
    private bool waveActive;
    private bool waveSpawning;
    private int waveNumber;
    private int enemiesLeftToSpawn;
    private int castleUpgradeLevel;
    private int holeUpgradeLevel;
    private int builtTowerCount;
    private float holeRadius = 1.2f;
    private float holeSpeed = 4.5f;
    private float currentSprintTime;
    private float sprintCooldownRemaining;
    private float castleMaxHp = 120f;
    private float castleHp = 120f;
    private float castleDamage = 2.2f;
    private float castleRange = 7f;
    private float castleCooldown = 0.85f;
    private float nextSpawnTimer;
    private float waveBreakTimer = 3f;
    private float resourceRespawnTimer;
    private Vector3 holeTarget;
    private ToggleableButton soundToggle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateBootstrap()
    {
        if (FindObjectOfType<VoidBastionBootstrap>() != null)
        {
            return;
        }

        var bootstrapObject = new GameObject("Void Bastion Bootstrap");
        bootstrapObject.AddComponent<VoidBastionBootstrap>();
    }

    private void Awake()
    {
        Application.targetFrameRate = 60;
        resources[ResourceType.Wood] = 0;
        resources[ResourceType.Stone] = 0;
        resources[ResourceType.Iron] = 0;

        SetupCamera();
        SetupUi();
        ShowMenu();
    }

    private void Update()
    {
        if (!gameStarted || gameEnded)
        {
            return;
        }

        HandlePointerInput();
        UpdateHoleMovement();
        UpdateCameraFollow();
        UpdateSprintState();
        UpdateAbsorption();
        UpdateResourceRespawn();
        UpdateWaveLoop();
        UpdateEnemies();
        UpdateDefenses();
        UpdateTemporaryVisuals();
        UpdateUpgradePanel();
        RefreshHud();
    }

    private void SetupCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        mainCamera.orthographic = false;
        mainCamera.fieldOfView = 60f;
        mainCamera.transform.position = new Vector3(CastlePosition.x, CameraFollowHeight, CameraFollowDepthOffset);
        mainCamera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        mainCamera.backgroundColor = new Color(0.72f, 0.88f, 0.96f);
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
    }

    private void SetupUi()
    {
        uiCanvas = FindObjectOfType<Canvas>();
        if (uiCanvas == null)
        {
            var canvasObject = new GameObject("Canvas");
            uiCanvas = canvasObject.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080f, 1920f);
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (FindObjectOfType<EventSystem>() == null)
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        menuPanel = CreatePanel("Menu Panel", new Vector2(0.5f, 0.5f), new Vector2(680f, 820f), new Color(0.07f, 0.11f, 0.14f, 0.84f));
        CreateText(menuPanel.transform, "Void Bastion", 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.82f), new Vector2(560f, 120f));
        CreateText(menuPanel.transform, "Guide the living void, feed the castle and survive every wave.", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.62f), new Vector2(560f, 140f));

        var playButton = CreateButton(menuPanel.transform, "Play", new Vector2(0.5f, 0.38f), new Vector2(320f, 110f));
        playButton.onClick.AddListener(StartGame);

        var settingsButton = CreateButton(menuPanel.transform, "Sound: On", new Vector2(0.5f, 0.2f), new Vector2(320f, 100f));
        soundToggle = new ToggleableButton
        {
            Button = settingsButton,
            Label = settingsButton.GetComponentInChildren<Text>()
        };
        settingsButton.onClick.AddListener(ToggleSound);

        hudPanel = CreatePanel("HUD Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0f, 0f, 0f, 0f));
        resourceText = CreateText(hudPanel.transform, string.Empty, 30, TextAnchor.UpperLeft, new Vector2(0.06f, 0.96f), new Vector2(420f, 200f));
        waveText = CreateText(hudPanel.transform, "Wave: 0/15", 30, TextAnchor.UpperCenter, new Vector2(0.5f, 0.96f), new Vector2(260f, 70f));
        castleHpText = CreateText(hudPanel.transform, "Castle HP: 120/120", 30, TextAnchor.UpperRight, new Vector2(0.94f, 0.96f), new Vector2(360f, 70f));
        statusText = CreateText(hudPanel.transform, "Gather resources to empower the bastion.", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.88f), new Vector2(860f, 80f));

        sprintButton = CreateButton(hudPanel.transform, "Sprint", new Vector2(0.17f, 0.12f), new Vector2(220f, 110f));
        sprintButton.onClick.AddListener(TriggerSprint);
        sprintButtonText = sprintButton.GetComponentInChildren<Text>();

        upgradePanel = CreatePanel("Upgrade Panel", new Vector2(0.5f, 0.12f), new Vector2(940f, 190f), new Color(0.1f, 0.18f, 0.12f, 0.88f));
        castleUpgradeButton = CreateButton(upgradePanel.transform, "Castle Upgrade", new Vector2(0.2f, 0.5f), new Vector2(250f, 94f));
        holeUpgradeButton = CreateButton(upgradePanel.transform, "Hole Upgrade", new Vector2(0.5f, 0.5f), new Vector2(250f, 94f));
        towerBuildButton = CreateButton(upgradePanel.transform, "Build Tower", new Vector2(0.8f, 0.5f), new Vector2(250f, 94f));
        castleUpgradeButton.onClick.AddListener(UpgradeCastle);
        holeUpgradeButton.onClick.AddListener(UpgradeHole);
        towerBuildButton.onClick.AddListener(BuildTower);
        castleUpgradeButtonText = castleUpgradeButton.GetComponentInChildren<Text>();
        holeUpgradeButtonText = holeUpgradeButton.GetComponentInChildren<Text>();
        towerBuildButtonText = towerBuildButton.GetComponentInChildren<Text>();

        endPanel = CreatePanel("End Panel", new Vector2(0.5f, 0.5f), new Vector2(720f, 620f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
        endTitleText = CreateText(endPanel.transform, string.Empty, 68, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.72f), new Vector2(520f, 100f));
        endBodyText = CreateText(endPanel.transform, string.Empty, 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.48f), new Vector2(560f, 180f));
        var restartButton = CreateButton(endPanel.transform, "Restart", new Vector2(0.5f, 0.18f), new Vector2(320f, 110f));
        restartButton.onClick.AddListener(RestartGame);

        hudPanel.SetActive(false);
        upgradePanel.SetActive(false);
        endPanel.SetActive(false);
    }

    private void ShowMenu()
    {
        menuPanel.SetActive(true);
        hudPanel.SetActive(false);
        upgradePanel.SetActive(false);
        endPanel.SetActive(false);
    }

    private void StartGame()
    {
        ResetRuntimeState();
        gameStarted = true;
        menuPanel.SetActive(false);
        hudPanel.SetActive(true);
        endPanel.SetActive(false);
        BuildWorld();
        RefreshHud();
    }

    private void ResetRuntimeState()
    {
        gameEnded = false;
        isDragging = false;
        sprintActive = false;
        waveActive = false;
        waveSpawning = false;
        waveNumber = 0;
        enemiesLeftToSpawn = 0;
        castleUpgradeLevel = 0;
        holeUpgradeLevel = 0;
        builtTowerCount = 0;
        holeRadius = 1.2f;
        holeSpeed = 4.5f;
        currentSprintTime = 0f;
        sprintCooldownRemaining = 0f;
        castleMaxHp = 120f;
        castleHp = 120f;
        castleDamage = 2.2f;
        castleRange = 7f;
        castleCooldown = 0.85f;
        nextSpawnTimer = 0f;
        waveBreakTimer = 3f;
        resourceRespawnTimer = ResourceRespawnInterval;

        resources[ResourceType.Wood] = 0;
        resources[ResourceType.Stone] = 0;
        resources[ResourceType.Iron] = 0;

        ClearEntities();
    }

    private void BuildWorld()
    {
        if (worldRoot != null)
        {
            Destroy(worldRoot.gameObject);
        }

        resourceNodes.Clear();
        mountainBounds.Clear();
        roadWaypoints.Clear();

        worldRoot = new GameObject("Runtime World").transform;
        CreateGround();
        CreateRoad();
        CreateMountains();
        CreateCastle();
        CreateHole();
        SpawnResources();
    }

    private void CreateGround()
    {
        var groundObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groundObject.name = "Ground";
        groundObject.transform.SetParent(worldRoot);
        groundObject.transform.position = new Vector3(0f, -0.55f, 0f);
        groundObject.transform.localScale = new Vector3(MapWidth, 1f, MapHeight);
        groundObject.GetComponent<Renderer>().material.color = new Color(0.37f, 0.68f, 0.34f);
    }

    private void CreateRoad()
    {
        roadWaypoints.Add(new Vector3(-14f, 0f, -7.5f));
        roadWaypoints.Add(new Vector3(-11f, 0f, -3.5f));
        roadWaypoints.Add(new Vector3(-8f, 0f, 0.5f));
        roadWaypoints.Add(new Vector3(-5f, 0f, 3.5f));
        roadWaypoints.Add(new Vector3(-1.5f, 0f, 2f));
        roadWaypoints.Add(new Vector3(1.5f, 0f, 0f));

        for (int index = 0; index < roadWaypoints.Count - 1; index++)
        {
            var from = roadWaypoints[index];
            var to = roadWaypoints[index + 1];
            var midpoint = (from + to) * 0.5f + new Vector3(0f, -0.2f, 0f);
            var length = Vector3.Distance(from, to);

            var roadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roadObject.name = "Road Segment";
            roadObject.transform.SetParent(worldRoot);
            roadObject.transform.position = midpoint;
            roadObject.transform.localScale = new Vector3(2.4f, 0.15f, length);
            roadObject.transform.rotation = Quaternion.LookRotation(to - from);
            roadObject.GetComponent<Renderer>().material.color = new Color(0.56f, 0.46f, 0.34f);
            Destroy(roadObject.GetComponent<Collider>());
        }
    }

    private void CreateMountains()
    {
        var mountainSegments = new List<MountainData>();
        var minZ = -MapHeight * 0.5f + 1.5f;
        var maxZ = MapHeight * 0.5f - 1.5f;
        var segmentSpacing = 3f;

        for (var z = minZ; z <= maxZ; z += segmentSpacing)
        {
            mountainSegments.Add(new MountainData(new Vector3(5.2f, 0.7f, z), new Vector3(3f, 1.6f, 3f)));
            mountainSegments.Add(new MountainData(new Vector3(7.8f, 0.7f, z + segmentSpacing * 0.5f), new Vector3(3f, 1.6f, 3f)));
        }

        foreach (var mountain in mountainSegments)
        {
            var mountainObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mountainObject.name = "Mountain";
            mountainObject.transform.SetParent(worldRoot);
            mountainObject.transform.position = mountain.Position;
            mountainObject.transform.localScale = mountain.Size;
            mountainObject.GetComponent<Renderer>().material.color = new Color(0.4f, 0.4f, 0.45f);
            mountainBounds.Add(new Bounds(mountain.Position, mountain.Size));
        }
    }

    private void CreateCastle()
    {
        var castleObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        castleObject.name = "Castle";
        castleObject.transform.SetParent(worldRoot);
        castleObject.transform.position = CastlePosition + new Vector3(0f, 0.6f, 0f);
        castleObject.transform.localScale = new Vector3(2.2f, 1.2f, 2.2f);
        castleObject.GetComponent<Renderer>().material.color = new Color(0.76f, 0.72f, 0.64f);
        castleTransform = castleObject.transform;

        var zoneObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        zoneObject.name = "Castle Zone";
        zoneObject.transform.SetParent(worldRoot);
        zoneObject.transform.position = CastlePosition + new Vector3(0f, 0.01f, 0f);
        zoneObject.transform.localScale = new Vector3(CastleZoneRadius * 2f, 0.01f, CastleZoneRadius * 2f);
        zoneObject.GetComponent<Renderer>().material.color = new Color(0.3f, 0.52f, 0.34f, 0.35f);
        Destroy(zoneObject.GetComponent<Collider>());

        turrets.Add(new DefenseTurret
        {
            Transform = castleTransform,
            Range = castleRange,
            Damage = castleDamage,
            Cooldown = castleCooldown,
            VisualColor = new Color(0.92f, 0.78f, 0.34f)
        });
    }

    private void CreateHole()
    {
        var holeObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        holeObject.name = "Player Hole";
        holeObject.transform.SetParent(worldRoot);
        holeObject.transform.position = HoleStartPosition;
        holeObject.GetComponent<Renderer>().material.color = new Color(0.05f, 0.05f, 0.07f);
        Destroy(holeObject.GetComponent<Collider>());
        holeTransform = holeObject.transform;
        holeTarget = holeTransform.position;
        UpdateHoleVisual();
        UpdateCameraFollow();
    }

    private void SpawnResources()
    {
        SpawnResourceStrip(ResourceType.Wood, 10);
        SpawnResourceStrip(ResourceType.Stone, 8);
        SpawnResourceStrip(ResourceType.Iron, 6);
    }

    private void SpawnResourceStrip(ResourceType type, int count)
    {
        for (int index = 0; index < count; index++)
        {
            SpawnSingleResource(type);
        }
    }

    private void HandlePointerInput()
    {
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began && !IsPointerOverUi(touch.fingerId))
            {
                if (!TryTapEnemy(touch.position))
                {
                    isDragging = TryGetGroundPoint(touch.position, out holeTarget);
                }
            }
            else if ((touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) && isDragging)
            {
                TryGetGroundPoint(touch.position, out holeTarget);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                holeTarget = holeTransform.position;
                isDragging = false;
            }

            return;
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUi(-1))
        {
            if (!TryTapEnemy(Input.mousePosition))
            {
                isDragging = TryGetGroundPoint(Input.mousePosition, out holeTarget);
            }
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            TryGetGroundPoint(Input.mousePosition, out holeTarget);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            holeTarget = holeTransform.position;
            isDragging = false;
        }
    }

    private void UpdateHoleMovement()
    {
        if (holeTransform == null)
        {
            return;
        }

        var currentPosition = holeTransform.position;
        var targetPosition = new Vector3(
            Mathf.Clamp(holeTarget.x, -MapWidth * 0.5f + 1f, MapWidth * 0.5f - 1f),
            currentPosition.y,
            Mathf.Clamp(holeTarget.z, -MapHeight * 0.5f + 1f, MapHeight * 0.5f - 1f));

        var speedMultiplier = sprintActive ? 1.75f : 1f;
        var nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, holeSpeed * speedMultiplier * Time.deltaTime);
        if (!IntersectsMountain(nextPosition))
        {
            holeTransform.position = nextPosition;
        }
    }

    private void UpdateSprintState()
    {
        if (sprintActive)
        {
            currentSprintTime -= Time.deltaTime;
            if (currentSprintTime <= 0f)
            {
                sprintActive = false;
                sprintCooldownRemaining = SprintCooldown;
            }
        }
        else if (sprintCooldownRemaining > 0f)
        {
            sprintCooldownRemaining -= Time.deltaTime;
        }

        sprintButton.interactable = !sprintActive && sprintCooldownRemaining <= 0f;
        sprintButtonText.text = sprintActive
            ? "Sprint!"
            : sprintCooldownRemaining > 0f
                ? "Sprint " + Mathf.CeilToInt(sprintCooldownRemaining)
                : "Sprint";
    }

    private void UpdateAbsorption()
    {
        for (int index = resourceNodes.Count - 1; index >= 0; index--)
        {
            var node = resourceNodes[index];
            if (node.Transform == null)
            {
                resourceNodes.RemoveAt(index);
                continue;
            }

            var nodePosition = node.Transform.position;
            var direction = holeTransform.position - nodePosition;
            var distance = direction.magnitude;

            if (distance <= holeRadius + 0.35f)
            {
                resources[node.ResourceType] += node.Amount;
                CreatePulseVisual(node.Transform.position + Vector3.up * 0.15f, new Color(0.9f, 0.94f, 0.55f));
                Destroy(node.Transform.gameObject);
                resourceNodes.RemoveAt(index);
                continue;
            }

        }
    }

    private void UpdateResourceRespawn()
    {
        resourceRespawnTimer -= Time.deltaTime;
        if (resourceRespawnTimer > 0f)
        {
            return;
        }

        resourceRespawnTimer = ResourceRespawnInterval;
        TryRespawnResource(ResourceType.Wood);
        TryRespawnResource(ResourceType.Stone);
        TryRespawnResource(ResourceType.Iron);
    }

    private void TryRespawnResource(ResourceType type)
    {
        if (CountResources(type) >= GetTargetResourceCount(type))
        {
            return;
        }

        SpawnSingleResource(type);
    }

    private int CountResources(ResourceType type)
    {
        var count = 0;
        for (int index = 0; index < resourceNodes.Count; index++)
        {
            if (resourceNodes[index].Transform != null && resourceNodes[index].ResourceType == type)
            {
                count++;
            }
        }

        return count;
    }

    private int GetTargetResourceCount(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return 10;
            case ResourceType.Stone:
                return 8;
            default:
                return 6;
        }
    }

    private void SpawnSingleResource(ResourceType type)
    {
        PrimitiveType primitiveType;
        Color color;
        float minX;
        float maxX;

        switch (type)
        {
            case ResourceType.Wood:
                primitiveType = PrimitiveType.Capsule;
                color = new Color(0.24f, 0.52f, 0.18f);
                minX = 7.5f;
                maxX = 13f;
                break;
            case ResourceType.Stone:
                primitiveType = PrimitiveType.Cube;
                color = new Color(0.56f, 0.58f, 0.62f);
                minX = 8.5f;
                maxX = 14f;
                break;
            default:
                primitiveType = PrimitiveType.Sphere;
                color = new Color(0.5f, 0.42f, 0.26f);
                minX = 10f;
                maxX = 14f;
                break;
        }

        var resourceObject = GameObject.CreatePrimitive(primitiveType);
        resourceObject.name = type + " Node";
        resourceObject.transform.SetParent(worldRoot);
        var resourceScale = Vector3.one * Random.Range(0.7f, 1.15f);
        resourceObject.transform.localScale = resourceScale;
        var resourceRenderer = resourceObject.GetComponent<Renderer>();
        resourceObject.transform.position = FindResourceSpawnPosition(resourceScale.x, minX, maxX, GroundSurfaceY + resourceRenderer.bounds.extents.y);
        resourceRenderer.material.color = color;

        var rigidbody = resourceObject.AddComponent<Rigidbody>();
        rigidbody.mass = 0.35f;
        rigidbody.drag = 1.5f;
        rigidbody.angularDrag = 3f;
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;
        rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

        resourceNodes.Add(new ResourceNode
        {
            ResourceType = type,
            Amount = type == ResourceType.Iron ? 3 : 5,
            Transform = resourceObject.transform
        });
    }

    private Vector3 FindResourceSpawnPosition(float resourceSize, float minX, float maxX, float spawnY)
    {
        var fallbackPosition = new Vector3(Random.Range(minX, maxX), spawnY, Random.Range(-9f, 9f));

        for (int attempt = 0; attempt < ResourceSpawnAttempts; attempt++)
        {
            var candidatePosition = new Vector3(Random.Range(minX, maxX), spawnY, Random.Range(-9f, 9f));
            var overlapsExistingResource = false;

            for (int index = 0; index < resourceNodes.Count; index++)
            {
                var existingTransform = resourceNodes[index].Transform;
                if (existingTransform == null)
                {
                    continue;
                }

                var requiredDistance = (resourceSize + existingTransform.localScale.x) * 0.8f;
                var candidateDistance = Vector2.Distance(
                    new Vector2(candidatePosition.x, candidatePosition.z),
                    new Vector2(existingTransform.position.x, existingTransform.position.z));

                if (candidateDistance < requiredDistance)
                {
                    overlapsExistingResource = true;
                    break;
                }
            }

            if (!overlapsExistingResource)
            {
                return candidatePosition;
            }
        }

        return fallbackPosition;
    }

    private void UpdateUpgradePanel()
    {
        if (holeTransform == null)
        {
            upgradePanel.SetActive(false);
            return;
        }

        var nearCastle = Vector3.Distance(holeTransform.position, CastlePosition + new Vector3(0f, 0.15f, 0f)) <= CastleZoneRadius;
        upgradePanel.SetActive(nearCastle);
        if (!nearCastle)
        {
            return;
        }

        var castleCost = GetCastleUpgradeCost();
        var holeCost = GetHoleUpgradeCost();
        var towerCost = GetTowerBuildCost();
        castleUpgradeButtonText.text = "Castle +" + (castleUpgradeLevel + 1) + "\n" + FormatCost(castleCost);
        holeUpgradeButtonText.text = "Hole +" + (holeUpgradeLevel + 1) + "\n" + FormatCost(holeCost);
        towerBuildButtonText.text = builtTowerCount >= 3 ? "Towers Full" : "Build Tower\n" + FormatCost(towerCost);
        castleUpgradeButton.interactable = HasResources(castleCost);
        holeUpgradeButton.interactable = HasResources(holeCost);
        towerBuildButton.interactable = builtTowerCount < 3 && HasResources(towerCost);
    }

    private void RefreshHud()
    {
        resourceText.text =
            "Wood: " + resources[ResourceType.Wood] + "\n" +
            "Stone: " + resources[ResourceType.Stone] + "\n" +
            "Iron: " + resources[ResourceType.Iron];
        waveText.text = "Wave: " + waveNumber + "/" + TotalWaves;
        castleHpText.text = "Castle HP: " + Mathf.CeilToInt(castleHp) + "/" + Mathf.CeilToInt(castleMaxHp);

        if (!waveActive)
        {
            var nextWave = Mathf.Min(waveNumber + 1, TotalWaves);
            statusText.text = nextWave > TotalWaves
                ? "All waves cleared."
                : "Upgrade window: " + Mathf.CeilToInt(Mathf.Max(0f, waveBreakTimer)) + "s until wave " + nextWave + ".";
        }
        else if (waveSpawning)
        {
            statusText.text = "Wave " + waveNumber + " pouring in. Enemies left: " + (enemies.Count + enemiesLeftToSpawn);
        }
        else
        {
            statusText.text = "Wave " + waveNumber + " in progress. Clean up the stragglers.";
        }
    }

    private void TriggerSprint()
    {
        if (sprintActive || sprintCooldownRemaining > 0f)
        {
            return;
        }

        sprintActive = true;
        currentSprintTime = SprintDuration;
    }

    private void ToggleSound()
    {
        soundEnabled = !soundEnabled;
        soundToggle.Label.text = soundEnabled ? "Sound: On" : "Sound: Off";
    }

    private void UpdateHoleVisual()
    {
        holeTransform.localScale = new Vector3(holeRadius * 2f, 0.18f, holeRadius * 2f);
    }

    private void UpdateCameraFollow()
    {
        if (mainCamera == null || holeTransform == null)
        {
            return;
        }

        mainCamera.transform.position = new Vector3(
            holeTransform.position.x,
            holeTransform.position.y + CameraFollowHeight,
            holeTransform.position.z + CameraFollowDepthOffset);
    }

    private void UpdateWaveLoop()
    {
        if (waveSpawning)
        {
            nextSpawnTimer -= Time.deltaTime;
            if (nextSpawnTimer <= 0f && enemiesLeftToSpawn > 0)
            {
                SpawnEnemy();
                enemiesLeftToSpawn--;
                nextSpawnTimer = Mathf.Max(0.35f, 0.95f - waveNumber * 0.03f);
            }

            if (enemiesLeftToSpawn <= 0)
            {
                waveSpawning = false;
            }
        }

        if (waveActive && !waveSpawning && enemies.Count == 0)
        {
            waveActive = false;
            if (waveNumber >= TotalWaves)
            {
                FinishGame(true);
            }
            else
            {
                waveBreakTimer = WaveBreakDuration;
            }
        }

        if (!waveActive && waveNumber < TotalWaves)
        {
            waveBreakTimer -= Time.deltaTime;
            if (waveBreakTimer <= 0f)
            {
                StartNextWave();
            }
        }
    }

    private void StartNextWave()
    {
        waveNumber++;
        waveActive = true;
        waveSpawning = true;
        enemiesLeftToSpawn = 3 + waveNumber * 2;
        nextSpawnTimer = 0f;
        statusText.text = "Wave " + waveNumber + " is approaching. Hold the bastion.";
    }

    private void SpawnEnemy()
    {
        var enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemyObject.name = "Enemy";
        enemyObject.transform.SetParent(worldRoot);
        enemyObject.transform.position = roadWaypoints[0] + new Vector3(0f, 0.6f, 0f);
        enemyObject.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        enemyObject.GetComponent<Renderer>().material.color = new Color(0.78f, 0.2f, 0.2f);

        enemies.Add(new EnemyUnit
        {
            Transform = enemyObject.transform,
            HitPoints = 4f + waveNumber * 1.15f,
            MaxHitPoints = 4f + waveNumber * 1.15f,
            Speed = 1.45f + waveNumber * 0.06f,
            Damage = 6f + waveNumber,
            WaypointIndex = 1
        });
    }

    private void UpdateEnemies()
    {
        for (int index = enemies.Count - 1; index >= 0; index--)
        {
            var enemy = enemies[index];
            if (enemy.Transform == null)
            {
                enemies.RemoveAt(index);
                continue;
            }

            if (enemy.WaypointIndex >= roadWaypoints.Count)
            {
                castleHp -= enemy.Damage;
                CreatePulseVisual(CastlePosition + Vector3.up, new Color(1f, 0.4f, 0.35f));
                Destroy(enemy.Transform.gameObject);
                enemies.RemoveAt(index);
                if (castleHp <= 0f)
                {
                    castleHp = 0f;
                    FinishGame(false);
                    return;
                }

                continue;
            }

            var target = roadWaypoints[enemy.WaypointIndex] + new Vector3(0f, 0.6f, 0f);
            enemy.Transform.position = Vector3.MoveTowards(enemy.Transform.position, target, enemy.Speed * Time.deltaTime);
            enemy.Transform.LookAt(target);

            if (Vector3.Distance(enemy.Transform.position, target) <= 0.08f)
            {
                enemy.WaypointIndex++;
            }
        }
    }

    private void UpdateDefenses()
    {
        for (int index = 0; index < turrets.Count; index++)
        {
            var turret = turrets[index];
            if (turret.Transform == null)
            {
                continue;
            }

            if (turret.CooldownRemaining > 0f)
            {
                turret.CooldownRemaining -= Time.deltaTime;
                continue;
            }

            var target = FindClosestEnemy(turret.Transform.position, turret.Range);
            if (target == null)
            {
                continue;
            }

            CreateShotVisual(turret.Transform.position + Vector3.up * 0.8f, target.Transform.position + Vector3.up * 0.5f, turret.VisualColor);
            DamageEnemy(target, turret.Damage);
            turret.CooldownRemaining = turret.Cooldown;
        }
    }

    private void UpdateTemporaryVisuals()
    {
        for (int index = temporaryVisuals.Count - 1; index >= 0; index--)
        {
            var visual = temporaryVisuals[index];
            if (visual.GameObject == null)
            {
                temporaryVisuals.RemoveAt(index);
                continue;
            }

            visual.Lifetime -= Time.deltaTime;
            if (visual.Lifetime <= 0f)
            {
                Destroy(visual.GameObject);
                temporaryVisuals.RemoveAt(index);
            }
        }
    }

    private void UpgradeCastle()
    {
        var cost = GetCastleUpgradeCost();
        if (!HasResources(cost))
        {
            return;
        }

        SpendResources(cost);
        castleUpgradeLevel++;
        castleMaxHp += 25f;
        castleHp = Mathf.Min(castleMaxHp, castleHp + 25f);
        castleDamage += 0.6f;
        castleRange += 0.35f;

        var castleTurret = turrets[0];
        castleTurret.Damage = castleDamage;
        castleTurret.Range = castleRange;
        ApplyCastleVisuals();
    }

    private void UpgradeHole()
    {
        var cost = GetHoleUpgradeCost();
        if (!HasResources(cost))
        {
            return;
        }

        SpendResources(cost);
        holeUpgradeLevel++;
        holeRadius += 0.28f;
        holeSpeed += 0.75f;
        UpdateHoleVisual();
    }

    private void BuildTower()
    {
        if (builtTowerCount >= 3)
        {
            return;
        }

        var cost = GetTowerBuildCost();
        if (!HasResources(cost))
        {
            return;
        }

        SpendResources(cost);
        var towerPositions = new[]
        {
            CastlePosition + new Vector3(-2.6f, 0.6f, -2.2f),
            CastlePosition + new Vector3(-2.9f, 0.6f, 2.5f),
            CastlePosition + new Vector3(0.6f, 0.6f, -3.2f)
        };

        var towerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        towerObject.name = "Tower";
        towerObject.transform.SetParent(worldRoot);
        towerObject.transform.position = towerPositions[builtTowerCount];
        towerObject.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
        towerObject.GetComponent<Renderer>().material.color = new Color(0.58f, 0.68f, 0.82f);

        turrets.Add(new DefenseTurret
        {
            Transform = towerObject.transform,
            Range = 6.4f,
            Damage = 1.6f + builtTowerCount * 0.4f,
            Cooldown = 0.7f,
            VisualColor = new Color(0.55f, 0.9f, 1f)
        });

        builtTowerCount++;
    }

    private void ApplyCastleVisuals()
    {
        castleTransform.localScale = new Vector3(2.2f + castleUpgradeLevel * 0.14f, 1.2f + castleUpgradeLevel * 0.08f, 2.2f + castleUpgradeLevel * 0.14f);
        var renderer = castleTransform.GetComponent<Renderer>();
        if (castleUpgradeLevel >= 4)
        {
            renderer.material.color = new Color(0.82f, 0.76f, 0.62f);
        }
        else if (castleUpgradeLevel >= 2)
        {
            renderer.material.color = new Color(0.72f, 0.74f, 0.78f);
        }
        else
        {
            renderer.material.color = new Color(0.76f, 0.72f, 0.64f);
        }
    }

    private void FinishGame(bool victory)
    {
        gameEnded = true;
        upgradePanel.SetActive(false);
        endPanel.SetActive(true);
        endTitleText.text = victory ? "Victory" : "Game Over";
        endBodyText.text = victory
            ? "The Void Bastion endured all 15 waves."
            : "The castle fell during wave " + Mathf.Max(1, waveNumber) + ".";
    }

    private void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ClearEntities()
    {
        for (int index = resourceNodes.Count - 1; index >= 0; index--)
        {
            if (resourceNodes[index].Transform != null)
            {
                Destroy(resourceNodes[index].Transform.gameObject);
            }
        }

        for (int index = enemies.Count - 1; index >= 0; index--)
        {
            if (enemies[index].Transform != null)
            {
                Destroy(enemies[index].Transform.gameObject);
            }
        }

        for (int index = temporaryVisuals.Count - 1; index >= 0; index--)
        {
            if (temporaryVisuals[index].GameObject != null)
            {
                Destroy(temporaryVisuals[index].GameObject);
            }
        }

        enemies.Clear();
        resourceNodes.Clear();
        turrets.Clear();
        temporaryVisuals.Clear();
    }

    private bool TryTapEnemy(Vector2 screenPosition)
    {
        var ray = mainCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out var hit, 100f))
        {
            for (int index = enemies.Count - 1; index >= 0; index--)
            {
                if (enemies[index].Transform == hit.transform)
                {
                    DamageEnemy(enemies[index], 1f);
                    CreatePulseVisual(hit.point, new Color(1f, 0.94f, 0.45f));
                    return true;
                }
            }
        }

        return false;
    }

    private void DamageEnemy(EnemyUnit enemy, float amount)
    {
        enemy.HitPoints -= amount;
        var healthFactor = Mathf.Clamp01(enemy.HitPoints / enemy.MaxHitPoints);
        enemy.Transform.GetComponent<Renderer>().material.color = Color.Lerp(new Color(0.22f, 0.1f, 0.1f), new Color(0.78f, 0.2f, 0.2f), healthFactor);

        if (enemy.HitPoints > 0f)
        {
            return;
        }

        CreatePulseVisual(enemy.Transform.position + Vector3.up * 0.35f, new Color(1f, 0.7f, 0.25f));
        Destroy(enemy.Transform.gameObject);
        enemies.Remove(enemy);
    }

    private EnemyUnit FindClosestEnemy(Vector3 from, float range)
    {
        EnemyUnit bestEnemy = null;
        var bestDistance = range;

        for (int index = 0; index < enemies.Count; index++)
        {
            if (enemies[index].Transform == null)
            {
                continue;
            }

            var distance = Vector3.Distance(from, enemies[index].Transform.position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestEnemy = enemies[index];
            }
        }

        return bestEnemy;
    }

    private void CreateShotVisual(Vector3 from, Vector3 to, Color color)
    {
        var beamObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beamObject.name = "Shot";
        beamObject.transform.SetParent(worldRoot);
        beamObject.transform.position = (from + to) * 0.5f;
        beamObject.transform.rotation = Quaternion.LookRotation(to - from);
        beamObject.transform.localScale = new Vector3(0.12f, 0.12f, Vector3.Distance(from, to));
        beamObject.GetComponent<Renderer>().material.color = color;
        Destroy(beamObject.GetComponent<Collider>());
        temporaryVisuals.Add(new TemporaryVisual { GameObject = beamObject, Lifetime = 0.08f });
    }

    private void CreatePulseVisual(Vector3 position, Color color)
    {
        var pulseObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pulseObject.name = "Pulse";
        pulseObject.transform.SetParent(worldRoot);
        pulseObject.transform.position = position;
        pulseObject.transform.localScale = Vector3.one * 0.4f;
        pulseObject.GetComponent<Renderer>().material.color = color;
        Destroy(pulseObject.GetComponent<Collider>());
        temporaryVisuals.Add(new TemporaryVisual { GameObject = pulseObject, Lifetime = 0.16f });
    }

    private ResourceCost GetCastleUpgradeCost()
    {
        return new ResourceCost(25 + castleUpgradeLevel * 10, 18 + castleUpgradeLevel * 8, 6 + castleUpgradeLevel * 3);
    }

    private ResourceCost GetHoleUpgradeCost()
    {
        return new ResourceCost(18 + holeUpgradeLevel * 8, 10 + holeUpgradeLevel * 6, 4 + holeUpgradeLevel * 2);
    }

    private ResourceCost GetTowerBuildCost()
    {
        return new ResourceCost(24 + builtTowerCount * 6, 20 + builtTowerCount * 5, 8 + builtTowerCount * 3);
    }

    private bool HasResources(ResourceCost cost)
    {
        return resources[ResourceType.Wood] >= cost.Wood &&
               resources[ResourceType.Stone] >= cost.Stone &&
               resources[ResourceType.Iron] >= cost.Iron;
    }

    private void SpendResources(ResourceCost cost)
    {
        resources[ResourceType.Wood] -= cost.Wood;
        resources[ResourceType.Stone] -= cost.Stone;
        resources[ResourceType.Iron] -= cost.Iron;
    }

    private string FormatCost(ResourceCost cost)
    {
        return cost.Wood + "W " + cost.Stone + "S " + cost.Iron + "I";
    }

    private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 worldPoint)
    {
        var ray = mainCamera.ScreenPointToRay(screenPosition);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out var enter))
        {
            worldPoint = ray.GetPoint(enter);
            worldPoint.y = holeTransform != null ? holeTransform.position.y : HoleCenterY;
            return true;
        }

        worldPoint = Vector3.zero;
        return false;
    }

    private bool IsPointerOverUi(int pointerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
    }

    private bool IntersectsMountain(Vector3 position)
    {
        var point = new Vector3(position.x, 0.7f, position.z);
        for (int index = 0; index < mountainBounds.Count; index++)
        {
            if (mountainBounds[index].Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    private GameObject CreatePanel(string panelName, Vector2 anchor, Vector2 size, Color color)
    {
        var panelObject = new GameObject(panelName);
        panelObject.transform.SetParent(uiCanvas.transform, false);
        var image = panelObject.AddComponent<Image>();
        image.color = color;

        var rectTransform = panelObject.GetComponent<RectTransform>();
        if (size == Vector2.zero)
        {
            image.raycastTarget = false;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
        else
        {
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = Vector2.zero;
        }
        return panelObject;
    }

    private Text CreateText(Transform parent, string content, int fontSize, TextAnchor alignment, Vector2 anchor, Vector2 size)
    {
        var textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;

        var rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;
        return text;
    }

    private Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 size)
    {
        var buttonObject = new GameObject(label + " Button");
        buttonObject.transform.SetParent(parent, false);

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.85f, 0.72f, 0.36f, 0.95f);

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        var rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;

        var labelText = CreateText(buttonObject.transform, label, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), size - new Vector2(24f, 16f));
        labelText.color = new Color(0.14f, 0.12f, 0.08f);

        return button;
    }

    private sealed class ResourceNode
    {
        public ResourceType ResourceType;
        public int Amount;
        public Transform Transform;
    }

    private sealed class EnemyUnit
    {
        public Transform Transform;
        public float HitPoints;
        public float MaxHitPoints;
        public float Speed;
        public float Damage;
        public int WaypointIndex;
    }

    private sealed class DefenseTurret
    {
        public Transform Transform;
        public float Range;
        public float Damage;
        public float Cooldown;
        public float CooldownRemaining;
        public Color VisualColor;
    }

    private sealed class TemporaryVisual
    {
        public GameObject GameObject;
        public float Lifetime;
    }

    private readonly struct ResourceCost
    {
        public ResourceCost(int wood, int stone, int iron)
        {
            Wood = wood;
            Stone = stone;
            Iron = iron;
        }

        public int Wood { get; }
        public int Stone { get; }
        public int Iron { get; }
    }

    private readonly struct MountainData
    {
        public MountainData(Vector3 position, Vector3 size)
        {
            Position = position;
            Size = size;
        }

        public Vector3 Position { get; }
        public Vector3 Size { get; }
    }

    private sealed class ToggleableButton
    {
        public Button Button;
        public Text Label;
    }

    private enum ResourceType
    {
        Wood,
        Stone,
        Iron
    }
}
