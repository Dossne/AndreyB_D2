using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class VoidBastionBootstrap : MonoBehaviour
{
    private const float MapWidth = 30f;
    private const float MapHeight = 22f;
    private const float CastleZoneRadius = 3.25f;
    private const float SprintDuration = 1.5f;
    private const float SprintCooldown = 5f;

    private static readonly Vector3 CastlePosition = new Vector3(1.5f, 0f, 0f);
    private static readonly Vector3 HoleStartPosition = new Vector3(4.5f, 0.15f, -1.5f);

    private readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();
    private readonly List<ResourceNode> resourceNodes = new List<ResourceNode>();

    private Camera mainCamera;
    private Transform holeTransform;
    private Transform castleTransform;
    private Transform worldRoot;
    private Canvas uiCanvas;
    private GameObject menuPanel;
    private GameObject hudPanel;
    private GameObject upgradePanel;
    private Text resourceText;
    private Text statusText;
    private Text waveText;
    private Text castleHpText;
    private Button sprintButton;
    private Text sprintButtonText;

    private bool gameStarted;
    private bool isDragging;
    private bool sprintActive;
    private float holeRadius = 1.2f;
    private float holeSpeed = 9f;
    private float currentSprintTime;
    private float sprintCooldownRemaining;
    private Vector3 holeTarget;

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
        if (!gameStarted)
        {
            return;
        }

        HandlePointerInput();
        UpdateHoleMovement();
        UpdateSprintState();
        UpdateAbsorption();
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

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 11f;
        mainCamera.transform.position = new Vector3(1.5f, 18f, -0.5f);
        mainCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
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
        CreateText(menuPanel.transform, "Drag the living void, gather resources and keep the castle alive.", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.62f), new Vector2(560f, 140f));

        var playButton = CreateButton(menuPanel.transform, "Play", new Vector2(0.5f, 0.38f), new Vector2(320f, 110f));
        playButton.onClick.AddListener(StartGame);

        var settingsButton = CreateButton(menuPanel.transform, "Sound: On", new Vector2(0.5f, 0.2f), new Vector2(320f, 100f));
        settingsButton.onClick.AddListener(() =>
        {
            settingsButton.GetComponentInChildren<Text>().text = settingsButton.GetComponentInChildren<Text>().text == "Sound: On"
                ? "Sound: Off"
                : "Sound: On";
        });

        hudPanel = CreatePanel("HUD Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0f, 0f, 0f, 0f));
        resourceText = CreateText(hudPanel.transform, string.Empty, 30, TextAnchor.UpperLeft, new Vector2(0.06f, 0.96f), new Vector2(420f, 200f));
        waveText = CreateText(hudPanel.transform, "Wave: 0", 30, TextAnchor.UpperCenter, new Vector2(0.5f, 0.96f), new Vector2(260f, 70f));
        castleHpText = CreateText(hudPanel.transform, "Castle HP: 100", 30, TextAnchor.UpperRight, new Vector2(0.94f, 0.96f), new Vector2(320f, 70f));
        statusText = CreateText(hudPanel.transform, "Gather resources to empower the bastion.", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.88f), new Vector2(820f, 80f));

        sprintButton = CreateButton(hudPanel.transform, "Sprint", new Vector2(0.83f, 0.12f), new Vector2(220f, 110f));
        sprintButton.onClick.AddListener(TriggerSprint);
        sprintButtonText = sprintButton.GetComponentInChildren<Text>();

        upgradePanel = CreatePanel("Upgrade Panel", new Vector2(0.5f, 0.12f), new Vector2(900f, 180f), new Color(0.1f, 0.18f, 0.12f, 0.88f));
        CreateButton(upgradePanel.transform, "Castle Upgrade", new Vector2(0.2f, 0.5f), new Vector2(240f, 90f));
        CreateButton(upgradePanel.transform, "Hole Upgrade", new Vector2(0.5f, 0.5f), new Vector2(240f, 90f));
        CreateButton(upgradePanel.transform, "Build Tower", new Vector2(0.8f, 0.5f), new Vector2(240f, 90f));

        hudPanel.SetActive(false);
        upgradePanel.SetActive(false);
    }

    private void ShowMenu()
    {
        menuPanel.SetActive(true);
        hudPanel.SetActive(false);
        upgradePanel.SetActive(false);
    }

    private void StartGame()
    {
        gameStarted = true;
        menuPanel.SetActive(false);
        hudPanel.SetActive(true);
        BuildWorld();
        RefreshHud();
    }

    private void BuildWorld()
    {
        if (worldRoot != null)
        {
            Destroy(worldRoot.gameObject);
        }

        resourceNodes.Clear();

        worldRoot = new GameObject("Runtime World").transform;
        CreateGround();
        CreateRoad();
        CreateMountains();

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
        zoneObject.GetComponent<Renderer>().material.color = new Color(0.3f, 0.52f, 0.34f, 0.5f);
        Destroy(zoneObject.GetComponent<Collider>());

        var holeObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        holeObject.name = "Player Hole";
        holeObject.transform.SetParent(worldRoot);
        holeObject.transform.position = HoleStartPosition;
        holeObject.GetComponent<Renderer>().material.color = new Color(0.05f, 0.05f, 0.07f);
        holeTransform = holeObject.transform;
        holeTarget = holeTransform.position;
        UpdateHoleVisual();

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
        var waypoints = GetRoadWaypoints();
        for (int index = 0; index < waypoints.Count - 1; index++)
        {
            var from = waypoints[index];
            var to = waypoints[index + 1];
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
        var mountainPositions = new[]
        {
            new Vector3(5.2f, 0.7f, -9f),
            new Vector3(5.2f, 0.7f, -5.5f),
            new Vector3(5.2f, 0.7f, 5.5f),
            new Vector3(5.2f, 0.7f, 9f),
            new Vector3(7.8f, 0.7f, -7.3f),
            new Vector3(7.8f, 0.7f, 7.3f)
        };

        foreach (var position in mountainPositions)
        {
            var mountainObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mountainObject.name = "Mountain";
            mountainObject.transform.SetParent(worldRoot);
            mountainObject.transform.position = position;
            mountainObject.transform.localScale = new Vector3(3f, 1.6f, 3f);
            mountainObject.GetComponent<Renderer>().material.color = new Color(0.4f, 0.4f, 0.45f);
        }
    }

    private void SpawnResources()
    {
        SpawnResourceStrip(ResourceType.Wood, PrimitiveType.Capsule, new Color(0.24f, 0.52f, 0.18f), 9, 7.5f, 13f);
        SpawnResourceStrip(ResourceType.Stone, PrimitiveType.Cube, new Color(0.56f, 0.58f, 0.62f), 7, 8.5f, 14f);
        SpawnResourceStrip(ResourceType.Iron, PrimitiveType.Sphere, new Color(0.5f, 0.42f, 0.26f), 5, 10f, 14f);
    }

    private void SpawnResourceStrip(ResourceType type, PrimitiveType primitiveType, Color color, int count, float minX, float maxX)
    {
        for (int index = 0; index < count; index++)
        {
            var resourceObject = GameObject.CreatePrimitive(primitiveType);
            resourceObject.name = type + " Node";
            resourceObject.transform.SetParent(worldRoot);
            resourceObject.transform.position = new Vector3(Random.Range(minX, maxX), 0.4f, Random.Range(-9f, 9f));
            resourceObject.transform.localScale = Vector3.one * Random.Range(0.7f, 1.15f);
            resourceObject.GetComponent<Renderer>().material.color = color;

            resourceNodes.Add(new ResourceNode
            {
                ResourceType = type,
                Amount = type == ResourceType.Iron ? 3 : 5,
                Transform = resourceObject.transform
            });
        }
    }

    private void HandlePointerInput()
    {
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began && !IsPointerOverUi(touch.fingerId))
            {
                isDragging = TryGetGroundPoint(touch.position, out holeTarget);
            }
            else if ((touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) && isDragging)
            {
                TryGetGroundPoint(touch.position, out holeTarget);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isDragging = false;
            }

            return;
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUi(-1))
        {
            isDragging = TryGetGroundPoint(Input.mousePosition, out holeTarget);
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            TryGetGroundPoint(Input.mousePosition, out holeTarget);
        }
        else if (Input.GetMouseButtonUp(0))
        {
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
        holeTransform.position = Vector3.MoveTowards(currentPosition, targetPosition, holeSpeed * speedMultiplier * Time.deltaTime);
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
                Destroy(node.Transform.gameObject);
                resourceNodes.RemoveAt(index);
                continue;
            }

            if (distance <= holeRadius + 1.6f)
            {
                node.Transform.position += direction.normalized * Time.deltaTime * 3.5f;
            }
        }
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
    }

    private void RefreshHud()
    {
        resourceText.text =
            "Wood: " + resources[ResourceType.Wood] + "\n" +
            "Stone: " + resources[ResourceType.Stone] + "\n" +
            "Iron: " + resources[ResourceType.Iron];
        waveText.text = "Wave: 0";
        castleHpText.text = "Castle HP: 100";
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

    private void UpdateHoleVisual()
    {
        holeTransform.localScale = new Vector3(holeRadius * 2f, 0.18f, holeRadius * 2f);
    }

    private List<Vector3> GetRoadWaypoints()
    {
        return new List<Vector3>
        {
            new Vector3(-14f, -0.05f, -7.5f),
            new Vector3(-11f, -0.05f, -3.5f),
            new Vector3(-8f, -0.05f, 0.5f),
            new Vector3(-5f, -0.05f, 3.5f),
            new Vector3(-1.5f, -0.05f, 2f),
            new Vector3(1.5f, -0.05f, 0f)
        };
    }

    private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 worldPoint)
    {
        var ray = mainCamera.ScreenPointToRay(screenPosition);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out var enter))
        {
            worldPoint = ray.GetPoint(enter);
            worldPoint.y = holeTransform != null ? holeTransform.position.y : 0.15f;
            return true;
        }

        worldPoint = Vector3.zero;
        return false;
    }

    private bool IsPointerOverUi(int pointerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
    }

    private GameObject CreatePanel(string panelName, Vector2 anchor, Vector2 size, Color color)
    {
        var panelObject = new GameObject(panelName);
        panelObject.transform.SetParent(uiCanvas.transform, false);
        var image = panelObject.AddComponent<Image>();
        image.color = color;

        var rectTransform = panelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;
        return panelObject;
    }

    private Text CreateText(Transform parent, string content, int fontSize, TextAnchor alignment, Vector2 anchor, Vector2 size)
    {
        var textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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

    private enum ResourceType
    {
        Wood,
        Stone,
        Iron
    }
}
