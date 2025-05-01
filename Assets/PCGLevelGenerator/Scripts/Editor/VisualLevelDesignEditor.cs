using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

// ██████╗  ██████╗  ██████╗    ██╗     ███████╗██╗   ██╗███████╗██╗         ██████╗ ███████╗███╗   ██╗
// ██╔══██╗██╔════╝ ██╔════╝    ██║     ██╔════╝██║   ██║██╔════╝██║        ██╔════╝ ██╔════╝████╗  ██║
// ██████╔╝██║      ██║  ███╗   ██║     █████╗  ██║   ██║█████╗  ██║        ██║  ███╗█████╗  ██╔██╗ ██║
// ██╔═══╝ ██║      ██║   ██║   ██║     ██╔══╝  ╚██╗ ██╔╝██╔══╝  ██║        ██║   ██║██╔══╝  ██║╚██╗██║
// ██║     ╚██████╗ ╚██████╔╝   ███████╗███████╗ ╚████╔╝ ███████╗███████╗   ╚██████╔╝███████╗██║ ╚████║
// ╚═╝      ╚═════╝  ╚═════╝    ╚══════╝╚══════╝  ╚═══╝  ╚══════╝╚══════╝    ╚═════╝ ╚══════╝╚═╝  ╚═══╝
//
// PCG Level Generator for Unity
// Copyright (c) 2025 Dineshkumar & Kamalanathan
// Version: 1.0.0

// Note: Assumes NodeType enum is defined in a separate file (e.g., LevelGenerationData.cs)

public class VisualLevelDesignEditor : EditorWindow
{
    #region Data Structures
    private class DesignerNode
    {
        public string id;
        public string displayName;
        public NodeType nodeType;
        public Vector2 position;
        public Vector2Int size;
        public string templateName;
        public GameObject templatePrefab;
        public List<string> connections = new List<string>();
        public Rect rect;
        public bool isSelected;
        public bool isDragging;
        public Color typeColor;
        public Vector2 visualSize;



        public DesignerNode(string id, NodeType type, Vector2 position, Vector2Int logicalSize, Vector2 initialVisualSize, string name = null)
        {
            this.id = id;
            this.nodeType = type;
            this.position = position;
            this.size = logicalSize;
            this.isSelected = false;
            this.isDragging = false;
            this.displayName = string.IsNullOrEmpty(name) ? $"{type}_{id}" : name;
            this.visualSize = initialVisualSize;

            switch (type)
            {
                case NodeType.Rect:
                    this.typeColor = UnityEngine.ColorUtility.TryParseHtmlString("#3B82F6", out Color cRect) ? cRect : Color.blue;
                    break;
                case NodeType.LShape:
                    this.typeColor = UnityEngine.ColorUtility.TryParseHtmlString("#10B981", out Color cL) ? cL : Color.green;
                    break;
                case NodeType.Template:
                    this.typeColor = UnityEngine.ColorUtility.TryParseHtmlString("#8B5CF6", out Color cT) ? cT : Color.magenta;
                    break;
                default:
                    this.typeColor = Color.grey;
                    break;
            }

            UpdateRect();
        }

        public void UpdateRect()
        {
            this.rect = new Rect(position.x - visualSize.x / 2f, position.y - visualSize.y / 2f, visualSize.x, visualSize.y);
        }

        public void SetPosition(Vector2 newPosition)
        {
            this.position = newPosition;
            UpdateRect();
        }

        public void SetVisualSize(Vector2 newSize)
        {
            this.visualSize.x = Mathf.Max(20f, newSize.x);
            this.visualSize.y = Mathf.Max(20f, newSize.y);
            UpdateRect();
        }
    }

    private class Connection
    {
        public DesignerNode fromNode;
        public DesignerNode toNode;
        public string fromId => fromNode?.id;
        public string toId => toNode?.id;

        public Connection(DesignerNode from, DesignerNode to)
        {
            this.fromNode = from;
            this.toNode = to;
        }

        public override bool Equals(object obj)
        {
            if (obj is Connection other)
            {
                return (fromNode == other.fromNode && toNode == other.toNode) ||
                       (fromNode == other.toNode && toNode == other.fromNode);
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash1 = fromNode?.GetHashCode() ?? 0;
            int hash2 = toNode?.GetHashCode() ?? 0;
            return hash1 ^ hash2;
        }
    }

    // JSON Data structures for Save/Load
    [System.Serializable]
    public class DesignerData
    {
        public List<NodeInfo> nodes = new List<NodeInfo>();
        public List<ConnectionInfo> connections = new List<ConnectionInfo>();

        [System.Serializable]
        public class NodeInfo
        {
            public string id;
            public string displayName;
            public int nodeType;
            public float x, y;
            public int logicalWidth, logicalHeight;
            public string templateName;
            public float visualWidth;
            public float visualHeight;
        }

        [System.Serializable]
        public class ConnectionInfo
        {
            public string fromId;
            public string toId;
        }
    }
    #endregion

    #region Editor State Variables
    private List<DesignerNode> nodes = new List<DesignerNode>();
    private List<Connection> connections = new List<Connection>();
    private HybridLevelGenerator targetGenerator;
    private Vector2 panOffset = Vector2.zero;
    private Vector2 dragStartOffset;
    private float zoom = 1.0f;
    private Rect graphViewRect;
    private DesignerNode selectedNode;
    private Connection selectedConnection;
    private DesignerNode connectingFromNode;
    private bool isCreatingConnection = false;
    private bool isPanning = false;
    private bool isDraggingNode = false;
    private bool showInstructions = true;
    private Vector2 sidePanelScrollPos = Vector2.zero;
    private NodeType newNodeType = NodeType.Rect;
    private Vector2Int newNodeSize = new Vector2Int(10, 10);
    private GameObject newTemplateReference;
    private string newNodeName = "New Room";
    private int nextNodeNumber = 1;
    private GUIStyle nodeStyle;
    private GUIStyle selectedNodeStyle;
    private GUIStyle instructionStyle;
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle connectionButtonStyle;
    private GUIStyle coordinateLabelStyle;
    private GUIStyle sectionHeaderStyle;
    private bool stylesInitialized = false;
    private float nodeSpacing = 150f;
    private int nodesPerRow = 5;
    private bool snapToGrid = true;
    private float gridSnapSize = 10f;
    private GUIContent helpContent;
    private Vector2 globalNodeVisualSize = new Vector2(15f, 15f);
    private Color globalNodeColor = Color.gray;
    private bool useNodeTypeColor = true;
    private bool hasUnsavedChanges = false;
    private string currentFilePath = "";
    private List<DesignerNode> nodesOutsideBounds = new List<DesignerNode>();
    private bool showBoundsWarning = true;
    private GUIStyle warningLabelStyle;
    #endregion

    #region Unity Editor Methods
    [MenuItem("Window/Visual Level Designer")]
    public static void ShowWindow()
    {
        GetWindow<VisualLevelDesignEditor>("Visual Level Designer").Show();
    }

    private void OnEnable()
    {
        targetGenerator = FindObjectOfType<HybridLevelGenerator>();
        nextNodeNumber = 1;

        if (nodes.Count == 0)
        {
            NewDesign(false);
        }

        // Initialize node size to 20% of level dimensions if generator exists
        if (targetGenerator != null)
        {
            SetDefaultNodeSize();
        }

        EditorApplication.delayCall += () => {
            // Adjust view to match the level dimensions
            AdjustViewToLevelDimensions();
            Repaint();
        };
    }

    private void SetDefaultNodeSize()
    {
        if (targetGenerator != null)
        {
            // Set node size to 20% of level dimensions
            int nodeWidth = Mathf.Max(10, Mathf.RoundToInt(targetGenerator.levelWidth * 0.2f));
            int nodeHeight = Mathf.Max(10, Mathf.RoundToInt(targetGenerator.levelHeight * 0.2f));
            newNodeSize = new Vector2Int(nodeWidth, nodeHeight);
        }
    }

    private void AdjustViewToLevelDimensions()
    {
        if (targetGenerator == null) return;

        // Get the level dimensions from the generator
        float levelWidth = targetGenerator.levelWidth;
        float levelHeight = targetGenerator.levelHeight;

        // Add padding to ensure the entire level is visible
        float paddingFactor = 1.2f; // 20% padding
        float paddedWidth = levelWidth * paddingFactor;
        float paddedHeight = levelHeight * paddingFactor;

        // Calculate zoom to fit level in view
        float zoomX = graphViewRect.width / paddedWidth;
        float zoomY = graphViewRect.height / paddedHeight;
        zoom = Mathf.Min(zoomX, zoomY);

        // Ensure zoom is within reasonable bounds
        zoom = Mathf.Clamp(zoom, 0.1f, 5.0f);

        // Center the view on origin
        Vector2 screenCenter = graphViewRect.size / 2f;
        panOffset = screenCenter;

        GUI.changed = true;
    }

    private void ValidateNodePositions()
    {
        if (targetGenerator == null)
            return;

        nodesOutsideBounds.Clear();

        float halfW = targetGenerator.levelWidth / 2f;
        float halfH = targetGenerator.levelHeight / 2f;

        Rect levelBounds = new Rect(-halfW, -halfH, targetGenerator.levelWidth, targetGenerator.levelHeight);

        foreach (var node in nodes)
        {
            // Check if node is fully or partially outside the level bounds
            float nodeHalfWidth = node.size.x / 2f;
            float nodeHalfHeight = node.size.y / 2f;

            Rect nodeRect = new Rect(
                node.position.x - nodeHalfWidth,
                node.position.y - nodeHalfHeight,
                node.size.x,
                node.size.y
            );

            if (!levelBounds.Contains(new Vector2(nodeRect.xMin, nodeRect.yMin)) ||
                !levelBounds.Contains(new Vector2(nodeRect.xMax, nodeRect.yMin)) ||
                !levelBounds.Contains(new Vector2(nodeRect.xMin, nodeRect.yMax)) ||
                !levelBounds.Contains(new Vector2(nodeRect.xMax, nodeRect.yMax)))
            {
                nodesOutsideBounds.Add(node);
            }
        }
    }

    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        // Base node style with semi-transparent background
        nodeStyle = new GUIStyle(GUI.skin.box);
        nodeStyle.normal.background = MakeColorTexture(new Color(0.2f, 0.2f, 0.25f, 0.85f));
        nodeStyle.border = new RectOffset(8, 8, 8, 8);
        nodeStyle.padding = new RectOffset(6, 6, 6, 6);
        nodeStyle.alignment = TextAnchor.UpperCenter;
        nodeStyle.normal.textColor = Color.white;
        nodeStyle.fontSize = 10;

        // Selected node style with more vibrant semi-transparent background
        selectedNodeStyle = new GUIStyle(nodeStyle);
        selectedNodeStyle.normal.background = MakeColorTexture(new Color(0.4f, 0.6f, 0.9f, 0.9f));

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 10,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };

        instructionStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 11,
            wordWrap = true,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
            padding = new RectOffset(10, 10, 10, 10)
        };

        connectionButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            padding = new RectOffset(5, 5, 2, 2),
            margin = new RectOffset(0, 0, 2, 0),
            fontSize = 9
        };

        coordinateLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 0.8f) }
        };

        sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        warningLabelStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 11,
            wordWrap = true,
            normal = { textColor = new Color(0.9f, 0.6f, 0.1f) },
            padding = new RectOffset(10, 10, 10, 10),
            richText = true
        };

        helpContent = new GUIContent("Help:\n• Right-click empty space: Create Room\n• Left-click drag node: Move\n• Click 'Connect' button: Start line\n• Click target node: Finish line\n• Right-click node/line: Options\n• Middle-click drag: Pan\n• Mouse wheel: Zoom\n• F key: Center View (Frames Nodes)\n• O key: Focus Origin (0,0)\n• Toggle 'Snap Nodes' in toolbar");

        stylesInitialized = true;
    }

    private Texture2D MakeColorTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private void OnGUI()
    {
        if (!stylesInitialized)
            InitializeStyles();

        HandleKeyboardShortcuts();

        EditorGUILayout.BeginVertical();
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        Rect potentialGraphRect = GUILayoutUtility.GetRect(100, 10000, 100, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (Event.current.type == EventType.Repaint)
        {
            graphViewRect = potentialGraphRect;
        }

        DrawGraphArea(graphViewRect);
        DrawSidePanels();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        ProcessEvents(Event.current);
        if (targetGenerator != null)
        {
            ValidateNodePositions();
        }
        if (GUI.changed)
            Repaint();
    }
    #endregion

    #region Drawing Methods
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            NewDesign();
        }

        if (GUILayout.Button("Open", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            OpenDesign();
        }

        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            SaveDesign();
        }

        if (GUILayout.Button("Save As", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            SaveDesignAs();
        }

        GUILayout.Space(20);

        showInstructions = GUILayout.Toggle(showInstructions, "Show Help", EditorStyles.toolbarButton);
        snapToGrid = GUILayout.Toggle(snapToGrid, "Snap Nodes", EditorStyles.toolbarButton);

        GUILayout.Space(10);

        if (GUILayout.Button("Auto-Arrange", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            AutoArrangeNodes();
        }

        if (GUILayout.Button("Center View (F)", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            CenterView();
        }

        if (GUILayout.Button("Focus Origin (O)", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            FocusOnOrigin();
        }

        if (GUILayout.Button("Match Level Bounds", EditorStyles.toolbarButton, GUILayout.Width(120)))
        {
            AdjustViewToLevelDimensions();
        }

        GUILayout.FlexibleSpace();

        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginHorizontal(GUILayout.Width(310));
        EditorGUILayout.PrefixLabel("Generator:");
        EditorGUI.BeginChangeCheck();
        HybridLevelGenerator newGenerator = EditorGUILayout.ObjectField("", targetGenerator, typeof(HybridLevelGenerator), true, GUILayout.Width(170)) as HybridLevelGenerator;

        if (GUILayout.Button(new GUIContent("?", "Edit the HybridLevelGenerator component to change level dimensions"), EditorStyles.miniButton, GUILayout.Width(20)))
        {
            if (targetGenerator != null)
                Selection.activeObject = targetGenerator;
            else
                EditorUtility.DisplayDialog("Level Dimensions", "Assign a HybridLevelGenerator component to control level dimensions. You can change width/height in the HybridLevelGenerator to expand level bounds.", "OK");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndHorizontal();

        GUIStyle creditStyle = new GUIStyle(EditorStyles.miniLabel);
        creditStyle.alignment = TextAnchor.MiddleCenter;
        creditStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 0.4f, 0.4f);
        EditorGUILayout.LabelField("Developed by Dineshkumar & Kamalanathan", creditStyle);
    }

    private void DrawGraphArea(Rect availableRect)
    {
        GUI.Box(availableRect, "", EditorStyles.helpBox);
        GUI.BeginClip(availableRect);

        Vector2 graphSize = availableRect.size;

        DrawGrid(graphSize, gridSnapSize, 0.2f, Color.gray);
        DrawGrid(graphSize, gridSnapSize * 5f, 0.4f, Color.gray);
        DrawOriginMarker(graphSize);
        DrawCoordinateLabels(graphSize);
        DrawGeneratorBoundsRect(graphSize);
        DrawConnections(graphSize);
        DrawNodes(graphSize);

        if (isCreatingConnection && connectingFromNode != null)
        {
            DrawConnectionInProgress(graphSize);
        }

        if (showBoundsWarning && nodesOutsideBounds.Count > 0 && targetGenerator != null)
        {
            string warningText = $"<b>Warning:</b> {nodesOutsideBounds.Count} node(s) are outside level boundaries and will be truncated!";
            string tipText = "\nTip: You can increase level dimensions in the HybridLevelGenerator component.";
            float warningWidth = 350;
            float textHeight = warningLabelStyle.CalcHeight(new GUIContent(warningText + tipText), warningWidth);

            // Calculate total height including button and padding
            float totalHeight = textHeight + 55; // Text + button (25) + padding (10)

            // Position the entire warning box at the bottom of the screen with some padding
            Rect warningRect = new Rect(
                availableRect.x + 10,
                availableRect.y + availableRect.height - totalHeight - 10,
                warningWidth,
                totalHeight
            );

            // Just draw the text part
            GUI.Box(warningRect, "", EditorStyles.helpBox);
            Rect textRect = new Rect(
                warningRect.x + 10,
                warningRect.y + 5,
                warningRect.width - 20,
                textHeight
            );
            GUI.Label(textRect, warningText + tipText, warningLabelStyle);

            // Create a properly positioned button directly under the text
            Rect buttonRect = new Rect(
                warningRect.x + 10,
                warningRect.y + textHeight + 5,
                warningRect.width - 20,
                25
            );

            if (GUI.Button(buttonRect, "Edit HybridLevelGenerator"))
            {
                Selection.activeObject = targetGenerator;
            }

            // Add a dismiss button to temporarily hide the warning
            Rect dismissRect = new Rect(
                warningRect.x + warningRect.width - 25,
                warningRect.y + 5,
                20,
                20
            );
            if (GUI.Button(dismissRect, "×", EditorStyles.miniButton))
            {
                showBoundsWarning = false;
                GUI.changed = true;
            }
        }

        GUI.EndClip();

        if (showInstructions && instructionStyle != null && helpContent != null)
        {
            float helpWidth = Mathf.Min(350, availableRect.width - 20);
            float helpHeight = stylesInitialized ? instructionStyle.CalcHeight(helpContent, helpWidth) : 110f;
            Rect helpRect = new Rect(availableRect.x + 10, availableRect.y + 10, helpWidth, helpHeight);
            GUI.Box(helpRect, helpContent, instructionStyle);
        }
    }

    private void DrawSidePanels()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(Mathf.Max(250, position.width * 0.25f)), GUILayout.ExpandHeight(true));

        sidePanelScrollPos = EditorGUILayout.BeginScrollView(sidePanelScrollPos);

        DrawGlobalSettingsPanel();
        EditorGUILayout.Space(10);
        DrawRoomCreationPanel();
        EditorGUILayout.Space(10);
        DrawNodePropertiesPanel();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        Color defaultBg = GUI.backgroundColor;
        GUI.backgroundColor = Color.green * 1.2f;

        if (GUILayout.Button("Create Scene Objects & Generate", GUILayout.Height(35)))
        {
            CreateSceneObjectsAndGenerate();
        }

        GUI.backgroundColor = defaultBg;

        EditorGUILayout.Space(5);

        EditorGUILayout.EndVertical();
    }

    private void DrawGrid(Vector2 viewSize, float gridSpacing, float gridOpacity, Color gridColor)
    {
        Handles.BeginGUI();

        // Base grid color with adjusted opacity for better aesthetics
        Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity * 0.8f);

        float zoomedSpacing = gridSpacing * zoom;
        if (zoomedSpacing < 4f)
        {
            Handles.EndGUI();
            return;
        }

        Vector2 offset = new Vector2(panOffset.x % zoomedSpacing, panOffset.y % zoomedSpacing);
        int widthDivs = Mathf.CeilToInt(viewSize.x / zoomedSpacing);
        int heightDivs = Mathf.CeilToInt(viewSize.y / zoomedSpacing);

        Vector2 originScreenPos = WorldToScreenPosition(Vector2.zero);

        // Draw vertical grid lines
        for (int i = -1; i <= widthDivs; i++)
        {
            float x = zoomedSpacing * i + offset.x;
            bool isOriginLine = Mathf.Abs(x - (originScreenPos.x - graphViewRect.x)) < 1f;

            // Use a more distinct color for the origin axes
            if (isOriginLine)
            {
                Handles.color = new Color(0.8f, 0.2f, 0.2f, 0.4f); // Red for X axis
            }
            else
            {
                Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity * 0.8f);
            }

            Handles.DrawLine(new Vector3(x, 0, 0), new Vector3(x, viewSize.y, 0));
        }

        // Draw horizontal grid lines
        for (int j = -1; j <= heightDivs; j++)
        {
            float y = zoomedSpacing * j + offset.y;
            bool isOriginLine = Mathf.Abs(y - (originScreenPos.y - graphViewRect.y)) < 1f;

            // Use a more distinct color for the origin axes
            if (isOriginLine)
            {
                Handles.color = new Color(0.2f, 0.7f, 0.2f, 0.4f); // Green for Y axis
            }
            else
            {
                Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity * 0.8f);
            }

            Handles.DrawLine(new Vector3(0, y, 0), new Vector3(viewSize.x, y, 0));
        }

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawOriginMarker(Vector2 graphSize)
    {
        Vector2 originScreenPos = WorldToScreenPosition(Vector2.zero);
        float markerSize = 5f * zoom;

        if (originScreenPos.x >= graphViewRect.x && originScreenPos.x <= graphViewRect.x + graphSize.x &&
            originScreenPos.y >= graphViewRect.y && originScreenPos.y <= graphViewRect.y + graphSize.y)
        {
            Handles.BeginGUI();
            Handles.color = Color.red;

            Vector2 screenOriginRelative = originScreenPos - graphViewRect.position;
            Handles.DrawLine(screenOriginRelative + Vector2.left * markerSize, screenOriginRelative + Vector2.right * markerSize);
            Handles.DrawLine(screenOriginRelative + Vector2.up * markerSize, screenOriginRelative + Vector2.down * markerSize);

            Handles.color = Color.white;
            Handles.EndGUI();
        }
    }

    private void DrawCoordinateLabels(Vector2 viewSize)
    {
        if (!stylesInitialized || coordinateLabelStyle == null)
            return;

        Handles.BeginGUI();

        float majorGridSpacing = gridSnapSize * 5f;
        float labelSpacing = majorGridSpacing * zoom;

        if (labelSpacing < 40f)
            labelSpacing *= 2f;

        if (labelSpacing < 20f)
        {
            Handles.EndGUI();
            return;
        }

        Vector2 offset = panOffset;

        Vector2 topLeftWorld = ScreenToWorldPosition(Vector2.zero);
        Vector2 bottomRightWorld = ScreenToWorldPosition(viewSize);

        float labelStep = majorGridSpacing;
        if (labelSpacing < 40f)
            labelStep *= 2f;

        float firstWorldX = Mathf.Ceil(topLeftWorld.x / labelStep) * labelStep;
        float firstWorldY = Mathf.Ceil(topLeftWorld.y / labelStep) * labelStep;

        for (float worldX = firstWorldX; worldX < bottomRightWorld.x; worldX += labelStep)
        {
            Vector2 screenPos = WorldToScreenPosition(new Vector2(worldX, topLeftWorld.y));

            if (screenPos.x < graphViewRect.x || screenPos.x > graphViewRect.x + viewSize.x)
                continue;

            Rect labelRect = new Rect(screenPos.x + 2, graphViewRect.y + 2, 50, 15);
            GUI.Label(labelRect, worldX.ToString("F0"), coordinateLabelStyle);
        }

        for (float worldY = firstWorldY; worldY < bottomRightWorld.y; worldY += labelStep)
        {
            Vector2 screenPos = WorldToScreenPosition(new Vector2(topLeftWorld.x, worldY));

            if (screenPos.y < graphViewRect.y || screenPos.y > graphViewRect.y + viewSize.y)
                continue;

            Rect labelRect = new Rect(graphViewRect.x + 2, screenPos.y + 2, 50, 15);
            GUI.Label(labelRect, worldY.ToString("F0"), coordinateLabelStyle);
        }

        Handles.EndGUI();
    }

    private void DrawGeneratorBoundsRect(Vector2 graphSize)
    {
        if (targetGenerator == null)
            return;

        float halfW = targetGenerator.levelWidth / 2f;
        float halfH = targetGenerator.levelHeight / 2f;

        Vector2 worldBottomLeft = new Vector2(-halfW, -halfH);
        Vector2 worldTopLeft = new Vector2(-halfW, halfH);
        Vector2 worldTopRight = new Vector2(halfW, halfH);
        Vector2 worldBottomRight = new Vector2(halfW, -halfH);

        Vector2 screenBL = WorldToScreenPosition(worldBottomLeft);
        Vector2 screenTL = WorldToScreenPosition(worldTopLeft);
        Vector2 screenTR = WorldToScreenPosition(worldTopRight);
        Vector2 screenBR = WorldToScreenPosition(worldBottomRight);

        Handles.BeginGUI();
        Handles.color = new Color(0f, 0.8f, 0.8f, 0.4f);
        Handles.DrawAAPolyLine(3f, screenBL, screenTL, screenTR, screenBR, screenBL);
        // Draw informational text about changing level dimensions
        if (targetGenerator != null)
        {
            Handles.BeginGUI();
            GUIStyle infoStyle = new GUIStyle(EditorStyles.miniLabel);
            infoStyle.normal.textColor = new Color(0f, 0.8f, 0.8f, 0.9f);

            string infoText = "Level dimensions are controlled by HybridLevelGenerator";
            float textWidth = 300;
            Vector2 textPos = new Vector2(
                screenBL.x,
                screenBL.y + 5
            );

            GUI.Label(new Rect(textPos.x, textPos.y, textWidth, 20),
                      infoText, infoStyle);

            Handles.EndGUI();
        }
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawNodes(Vector2 graphSize)
    {
        if (nodes.Count == 0)
        {
            GUI.Label(new Rect(graphSize.x / 2 - 100, graphSize.y / 2 - 10, 200, 20),
                      "Right-click to create a room.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            node.UpdateRect();

            Rect displayRect = WorldToScreenRect(node.rect);
            if (displayRect.xMax < 0 || displayRect.x > graphSize.x ||
                displayRect.yMax < 0 || displayRect.y > graphSize.y)
                continue;

            // Store original background color
            Color originalBgColor = GUI.backgroundColor;

            // Determine node color with transparency for better visuals
            Color nodeBgColor = useNodeTypeColor ? node.typeColor : globalNodeColor;
            nodeBgColor.a = 0.85f;  // Apply semi-transparency

            // Create a slightly darker header color
            Color headerColor = Color.Lerp(nodeBgColor, Color.black, 0.2f);
            headerColor.a = nodeBgColor.a;

            // Draw node shadow for depth
            Rect shadowRect = new Rect(displayRect);
            shadowRect.x += 3f;
            shadowRect.y += 3f;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.3f);
            GUI.Box(shadowRect, "", nodeStyle);

            // Draw main node background
            GUI.backgroundColor = nodeBgColor;

            GUIStyle style = node.isSelected ? selectedNodeStyle : nodeStyle;

            // Create a temporary style for this specific node
            GUIStyle tempStyle = new GUIStyle(style);
            tempStyle.normal.background = MakeColorTexture(GUI.backgroundColor);

            if (node.isSelected)
            {
                // Highlight selected nodes with brighter color
                Color selectedBg = Color.Lerp(nodeBgColor, Color.white, 0.3f);
                selectedBg.a = 0.9f;
                tempStyle.normal.background = MakeColorTexture(selectedBg);
            }

            // Draw the node box
            GUI.Box(displayRect, "", tempStyle);

            if (nodesOutsideBounds.Contains(node))
            {
                // Draw a warning border for nodes outside bounds
                Color originalColor = GUI.color;
                GUI.color = new Color(1f, 0.6f, 0.1f, 0.8f);
                Rect warningRect = new Rect(displayRect);
                warningRect.x -= 2;
                warningRect.y -= 2;
                warningRect.width += 4;
                warningRect.height += 4;
                GUI.Box(warningRect, "", EditorStyles.helpBox);
                GUI.color = originalColor;
            }

            // Draw a header section with gradient
            Rect headerRect = new Rect(displayRect.x, displayRect.y, displayRect.width, 18);
            EditorGUI.DrawRect(headerRect, headerColor);

            // Start content area
            GUILayout.BeginArea(displayRect);

            // Node title with icon
            string typeIcon = GetTypeIcon(node.nodeType);
            string title = $"{typeIcon} {node.displayName}";

            // Add some spacing at the top
            GUILayout.Space(3);
            GUILayout.Label(title, subHeaderStyle);

            // Display ID with smaller font
            GUILayout.Label($"ID: {node.id}", EditorStyles.miniLabel);

            // Display template info if applicable
            if (node.nodeType == NodeType.Template && !string.IsNullOrEmpty(node.templateName))
            {
                GUILayout.Label($"Tmpl: {node.templateName}", EditorStyles.miniLabel);
            }

            // Button with slight styling
            GUI.backgroundColor = Color.Lerp(nodeBgColor, Color.white, 0.2f);
            if (GUILayout.Button("Connect", connectionButtonStyle))
            {
                connectingFromNode = node;
                isCreatingConnection = true;
                GUI.changed = true;
            }

            // Restore original colors
            GUI.backgroundColor = originalBgColor;

            GUILayout.EndArea();
        }
    }

    private void DrawConnections(Vector2 graphSize)
    {
        Handles.BeginGUI();
        List<Connection> connectionsToDelete = null;

        foreach (var connection in connections)
        {
            if (connection.fromNode == null || connection.toNode == null)
                continue;

            Vector2 startPos = WorldToScreenPosition(connection.fromNode.position);
            Vector2 endPos = WorldToScreenPosition(connection.toNode.position);

            if ((startPos.x < 0 && endPos.x < 0) ||
                (startPos.x > graphSize.x && endPos.x > graphSize.x) ||
                (startPos.y < 0 && endPos.y < 0) ||
                (startPos.y > graphSize.y && endPos.y > graphSize.y))
                continue;

            bool isSelected = (selectedConnection == connection);

            // Calculate line thickness based on zoom level - thicker when zoomed out
            float baseThickness = 2.0f;
            float zoomFactor = Mathf.Max(1f, 1.5f / zoom);
            float outerThickness = baseThickness * zoomFactor;
            float innerThickness = outerThickness * 0.5f;

            // Draw straight line connections with improved visibility
            if (isSelected)
            {
                // Draw a glow for selected connections
                Handles.color = new Color(1f, 0.9f, 0.2f, 0.5f); // More opaque
                Handles.DrawLine(startPos, endPos, outerThickness);
                Handles.color = Color.yellow;
                Handles.DrawLine(startPos, endPos, innerThickness);
            }
            else
            {
                // Draw normal connections with improved visibility
                Handles.color = new Color(0.5f, 0.8f, 1f, 0.6f); // More opaque
                Handles.DrawLine(startPos, endPos, outerThickness);
                Handles.color = new Color(0.5f, 0.8f, 1f, 0.9f); // More opaque inner line
                Handles.DrawLine(startPos, endPos, innerThickness);
            }

            // Calculate midpoint for the handle
            Vector2 handlePos = (startPos + endPos) * 0.5f;
            float handleSize = (isSelected ? 9f : 7f) * zoomFactor; // Scale with zoom

            // Create a rect for hit testing
            Rect midPointRect = new Rect(handlePos.x - handleSize / 2, handlePos.y - handleSize / 2,
                                        handleSize, handleSize);

            // Draw connection handle point
            if (isSelected)
            {
                Handles.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            }
            else
            {
                Handles.color = new Color(0.5f, 0.8f, 1f, 0.8f);
            }

            Handles.DrawSolidDisc(handlePos, Vector3.forward, handleSize * 0.5f);

            // Context menu and selection handling for connections
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 1 &&
                midPointRect.Contains(Event.current.mousePosition))
            {
                if (connectionsToDelete == null)
                    connectionsToDelete = new List<Connection>();

                connectionsToDelete.Add(connection);
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDown &&
                     Event.current.button == 0 &&
                     midPointRect.Contains(Event.current.mousePosition))
            {
                selectedConnection = connection;
                selectedNode = null;
                GUI.changed = true;
                Event.current.Use();
            }
        }

        Handles.color = Color.white;
        Handles.EndGUI();

        // Handle deletion dialog
        if (connectionsToDelete != null)
        {
            foreach (var conn in connectionsToDelete)
            {
                if (EditorUtility.DisplayDialog("Delete Connection",
                    $"Delete connection between {conn.fromNode.displayName} and {conn.toNode.displayName}?",
                    "Yes", "No"))
                {
                    RemoveConnection(conn.fromNode, conn.toNode);
                }
            }
        }
    }

    private void DrawConnectionInProgress(Vector2 graphSize)
    {
        if (connectingFromNode == null) return;

        Handles.BeginGUI();

        Vector2 startPos = WorldToScreenPosition(connectingFromNode.position);
        Vector2 mousePos = Event.current.mousePosition;

        // Draw straight line connection preview with improved visibility
        // Outer glow with higher opacity
        Handles.color = new Color(1f, 0.9f, 0.2f, 0.5f);
        float lineThickness = 5f * Mathf.Max(1f, 1.5f / zoom); // Scale with zoom
        Handles.DrawLine(startPos, mousePos, lineThickness);

        // Inner line with higher opacity
        Handles.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Handles.DrawLine(startPos, mousePos, lineThickness * 0.5f);

        Handles.color = Color.white;
        Handles.EndGUI();

        // Make sure to repaint constantly while connection is in progress
        Repaint();
        GUI.changed = true;
    }

    private void DrawRoomCreationPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(new GUIContent(" Create New Room", EditorGUIUtility.IconContent("CreateAddNew").image), sectionHeaderStyle);

        EditorGUILayout.Space(5);

        newNodeName = EditorGUILayout.TextField("Name:", newNodeName);
        newNodeType = (NodeType)EditorGUILayout.EnumPopup("Type:", newNodeType);

        // Only show default 20% button if generator exists
        if (targetGenerator != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Size Options:");

            if (GUILayout.Button("Set to 20% of Level", EditorStyles.miniButton))
            {
                SetDefaultNodeSize();
            }
            EditorGUILayout.EndHorizontal();
        }

        newNodeSize = EditorGUILayout.Vector2IntField("Logical Size:", newNodeSize);

        if (newNodeType == NodeType.Template)
        {
            newTemplateReference = EditorGUILayout.ObjectField("Template Prefab:", newTemplateReference, typeof(GameObject), false) as GameObject;

            if (targetGenerator != null && targetGenerator.roomTemplatePrefabs != null && targetGenerator.roomTemplatePrefabs.Count > 0)
            {
                List<string> names = targetGenerator.roomTemplatePrefabs.Where(p => p != null).Select(p => p.name).ToList();
                names.Insert(0, "Select from Generator...");

                int currentIndex = newTemplateReference != null ? names.IndexOf(newTemplateReference.name) : 0;
                if (currentIndex < 0) currentIndex = 0;

                int newIndex = EditorGUILayout.Popup(" ", currentIndex, names.ToArray());
                if (newIndex > 0 && newIndex != currentIndex)
                {
                    newTemplateReference = targetGenerator.roomTemplatePrefabs.FirstOrDefault(p => p.name == names[newIndex]);
                }
            }
        }

        // Visually improved create button
        GUILayout.Space(5);
        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.9f);

        if (GUILayout.Button("Create Room", GUILayout.Height(30)))
        {
            Vector2 centerPos = graphViewRect.size / 2f;
            Vector2 worldPos = ScreenToWorldPosition(centerPos);
            CreateNewNode(worldPos);
        }

        GUI.backgroundColor = defaultColor;

        EditorGUILayout.EndVertical();
    }

    private void DrawGlobalSettingsPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(new GUIContent(" Global Node Visuals", EditorGUIUtility.IconContent("Settings").image), sectionHeaderStyle);

        EditorGUI.BeginChangeCheck();

        Vector2 newVisualSize = EditorGUILayout.Vector2Field("Node Visual Size:", globalNodeVisualSize);
        bool newUseNodeTypeColor = EditorGUILayout.Toggle("Use Color By Node Type", useNodeTypeColor);

        Color newGlobalColor = globalNodeColor;
        EditorGUI.BeginDisabledGroup(newUseNodeTypeColor);
        newGlobalColor = EditorGUILayout.ColorField("Global Node Color:", globalNodeColor);
        EditorGUI.EndDisabledGroup();

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(this, "Change Global Node Visuals");
            bool updateNodes = false;

            newVisualSize.x = Mathf.Max(20f, newVisualSize.x);
            newVisualSize.y = Mathf.Max(20f, newVisualSize.y);

            if (globalNodeVisualSize != newVisualSize)
            {
                globalNodeVisualSize = newVisualSize;
                updateNodes = true;
            }

            if (useNodeTypeColor != newUseNodeTypeColor)
            {
                useNodeTypeColor = newUseNodeTypeColor;
                updateNodes = true;
            }

            if (globalNodeColor != newGlobalColor)
            {
                globalNodeColor = newGlobalColor;
                if (!useNodeTypeColor) updateNodes = true;
            }

            if (updateNodes)
            {
                foreach (var node in nodes)
                {
                    node.SetVisualSize(globalNodeVisualSize);
                }
                GUI.changed = true;
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawNodePropertiesPanel()
    {
        if (selectedNode == null) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(new GUIContent($" Properties: {selectedNode.displayName}", EditorGUIUtility.IconContent("d_Prefab Icon").image), sectionHeaderStyle);

        EditorGUILayout.Space(5);

        EditorGUI.BeginChangeCheck();

        GUI.enabled = false;
        EditorGUILayout.TextField("Room ID:", selectedNode.id);
        EditorGUILayout.EnumPopup("Type:", selectedNode.nodeType);
        EditorGUILayout.Vector2Field("World Position (Center):", selectedNode.position);
        GUI.enabled = true;

        selectedNode.displayName = EditorGUILayout.TextField("Name:", selectedNode.displayName);
        selectedNode.size = EditorGUILayout.Vector2IntField("Logical Size:", selectedNode.size);
        if (nodesOutsideBounds.Contains(selectedNode))
        {

            EditorGUILayout.Space(5);
            string warningMessage = "This room extends beyond level boundaries and will be truncated during generation!";

            // Add the hint about expanding level dimensions
            if (targetGenerator != null)
            {
                warningMessage += "\n\nTip: You can increase the level dimensions in the HybridLevelGenerator component to accommodate larger layouts.";
            }

            EditorGUILayout.HelpBox(warningMessage, MessageType.Warning);

            if (targetGenerator != null && GUILayout.Button("Select HybridLevelGenerator"))
            {
                Selection.activeObject = targetGenerator;
            }

        }

        if (selectedNode.nodeType == NodeType.Template)
        {
            GameObject newTemplate = EditorGUILayout.ObjectField("Template Prefab:", selectedNode.templatePrefab, typeof(GameObject), false) as GameObject;
            if (newTemplate != selectedNode.templatePrefab)
            {
                selectedNode.templatePrefab = newTemplate;
                selectedNode.templateName = newTemplate?.name ?? "";
            }
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Connections:", EditorStyles.boldLabel);

        if (selectedNode.connections.Count == 0)
        {
            EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
        }
        else
        {
            List<string> connectionsToRemove = null;

            foreach (string connectedId in selectedNode.connections)
            {
                DesignerNode connectedNode = nodes.FirstOrDefault(n => n.id == connectedId);
                if (connectedNode == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"→ {connectedNode.displayName} ({connectedId})", EditorStyles.miniLabel);

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    if (connectionsToRemove == null) connectionsToRemove = new List<string>();
                    connectionsToRemove.Add(connectedId);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (connectionsToRemove != null)
            {
                foreach (string idToRemove in connectionsToRemove)
                {
                    RemoveConnection(selectedNode, nodes.FirstOrDefault(n => n.id == idToRemove));
                }
            }
        }

        EditorGUILayout.Space(10);

        // Improved delete button with warning color
        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.2f);
        if (GUILayout.Button("Delete Room", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Delete Room", $"Delete room \"{selectedNode.displayName}\"?", "Yes", "No"))
            {
                DeleteNode(selectedNode);
                GUIUtility.ExitGUI();
            }
        }
        GUI.backgroundColor = Color.white;

        if (EditorGUI.EndChangeCheck())
        {
            hasUnsavedChanges = true;
        }

        EditorGUILayout.EndVertical();
    }
    #endregion

    #region Event Handling
    private void HandleKeyboardShortcuts()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                if (selectedNode != null)
                {
                    DeleteNode(selectedNode);
                    e.Use();
                }
                else if (selectedConnection != null)
                {
                    RemoveConnection(selectedConnection.fromNode, selectedConnection.toNode);
                    e.Use();
                }
            }
            else if (e.control && e.keyCode == KeyCode.S)
            {
                SaveDesign();
                e.Use();
            }
            else if (e.control && e.shift && e.keyCode == KeyCode.S)
            {
                SaveDesignAs();
                e.Use();
            }
            else if (e.control && e.keyCode == KeyCode.O)
            {
                OpenDesign();
                e.Use();
            }
            else if (e.control && e.keyCode == KeyCode.N)
            {
                NewDesign();
                e.Use();
            }
            else if (e.keyCode == KeyCode.F)
            {
                CenterView();
                e.Use();
            }
            else if (e.keyCode == KeyCode.O)
            {
                FocusOnOrigin();
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                if (isCreatingConnection)
                {
                    isCreatingConnection = false;
                    connectingFromNode = null;
                    e.Use();
                }
                else if (selectedNode != null || selectedConnection != null)
                {
                    selectedNode = null;
                    selectedConnection = null;
                    e.Use();
                }
            }
        }
    }

    private void ProcessEvents(Event e)
    {
        switch (e.type)
        {
            case EventType.MouseDown:
                OnMouseDown(e);
                break;
            case EventType.MouseUp:
                OnMouseUp(e);
                break;
            case EventType.MouseDrag:
                OnMouseDrag(e);
                break;
            case EventType.ScrollWheel:
                OnScrollWheel(e);
                break;
            case EventType.ContextClick:
                HandleRightMouseDown(e.mousePosition, e);
                break;
        }
    }

    private void OnMouseDown(Event e)
    {
        Vector2 graphSpaceMousePos = ScreenToWorldPosition(e.mousePosition);

        if (e.button == 0)
        {
            HandleLeftMouseDown(graphSpaceMousePos, e);
        }
        else if (e.button == 2)
        {
            isPanning = true;
            isDraggingNode = false;
            isCreatingConnection = false;
            GUI.FocusControl(null);
            e.Use();
        }
    }

    private void OnMouseUp(Event e)
    {
        if (e.button == 0)
        {
            if (isCreatingConnection && connectingFromNode != null)
            {
                CompleteConnection(e.mousePosition);
            }

            if (isDraggingNode && selectedNode != null)
            {
                if (snapToGrid && gridSnapSize > 0)
                {
                    Vector2 currentPos = selectedNode.position;
                    float snappedX = Mathf.Round(currentPos.x / gridSnapSize) * gridSnapSize;
                    float snappedY = Mathf.Round(currentPos.y / gridSnapSize) * gridSnapSize;

                    if (snappedX != currentPos.x || snappedY != currentPos.y)
                    {
                        selectedNode.SetPosition(new Vector2(snappedX, snappedY));
                        hasUnsavedChanges = true;
                        GUI.changed = true;
                    }
                }

                isDraggingNode = false;
            }
        }
        else if (e.button == 2)
        {
            isPanning = false;
        }

        foreach (var node in nodes) node.isDragging = false;
    }

    private void OnMouseDrag(Event e)
    {
        if (e.button == 0 && isDraggingNode && selectedNode != null)
        {
            Vector2 worldDelta = e.delta / zoom;
            selectedNode.SetPosition(selectedNode.position + worldDelta);
            hasUnsavedChanges = true;
            GUI.changed = true;
            e.Use();
        }
        else if (e.button == 2 && isPanning)
        {
            panOffset += e.delta;
            GUI.changed = true;
            e.Use();
        }
    }

    private void OnScrollWheel(Event e)
    {
        // Improved zoom control with better sensitivity and limits
        float zoomDelta = -e.delta.y * 0.05f; // Reduced sensitivity for smoother zooming
        float newZoom = zoom * (1f + zoomDelta);
        newZoom = Mathf.Clamp(newZoom, 0.1f, 5.0f); // Wider zoom range

        Vector2 mousePos = e.mousePosition;
        Vector2 worldPos = ScreenToWorldPosition(mousePos);

        zoom = newZoom;

        Vector2 newScreenPos = WorldToScreenPosition(worldPos);
        panOffset += (mousePos - newScreenPos);

        e.Use();
        GUI.changed = true;
    }

    private void HandleLeftMouseDown(Vector2 worldMousePos, Event e)
    {
        bool clickedOnNode = false;
        selectedConnection = null;

        // Special handling for nodes outside boundaries - check them first
        foreach (var node in nodesOutsideBounds)
        {
            if (node.rect.Contains(worldMousePos))
            {
                selectedNode = node;
                isDraggingNode = true;
                dragStartOffset = worldMousePos - node.position;
                clickedOnNode = true;
                GUI.FocusControl(null);
                e.Use();
                break;
            }
        }

        // If no outside-boundary node is clicked, check the regular nodes
        if (!clickedOnNode)
        {
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var node = nodes[i];
                // Skip nodes we already checked in nodesOutsideBounds
                if (nodesOutsideBounds.Contains(node)) continue;

                if (node.rect.Contains(worldMousePos))
                {
                    selectedNode = node;
                    isDraggingNode = true;
                    dragStartOffset = worldMousePos - node.position;
                    clickedOnNode = true;
                    GUI.FocusControl(null);
                    e.Use();
                    break;
                }
            }
        }

        if (!clickedOnNode)
        {
            selectedNode = null;
            GUI.FocusControl(null);
        }

        GUI.changed = true;
    }

    private void HandleRightMouseDown(Vector2 screenMousePos, Event e)
    {
        Vector2 worldMousePos = ScreenToWorldPosition(screenMousePos);

        bool clickedOnNode = false;
        DesignerNode targetNode = null;

        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            if (nodes[i].rect.Contains(worldMousePos))
            {
                clickedOnNode = true;
                targetNode = nodes[i];
                break;
            }
        }

        GenericMenu menu = new GenericMenu();

        if (clickedOnNode)
        {
            menu.AddItem(new GUIContent("Delete Room"), false, () => {
                if (EditorUtility.DisplayDialog("Delete Room", $"Delete room \"{targetNode.displayName}\"?", "Yes", "No"))
                {
                    DeleteNode(targetNode);
                }
            });

            menu.AddItem(new GUIContent("Start Connection"), false, () => {
                connectingFromNode = targetNode;
                isCreatingConnection = true;
            });
        }
        else
        {
            menu.AddItem(new GUIContent("Create Rect Room"), false, () => CreateNewNode(worldMousePos, NodeType.Rect));
            menu.AddItem(new GUIContent("Create L-Shape Room"), false, () => CreateNewNode(worldMousePos, NodeType.LShape));
            menu.AddItem(new GUIContent("Create Template Room"), false, () => CreateNewNode(worldMousePos, NodeType.Template));

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Center View (F)"), false, CenterView);
            menu.AddItem(new GUIContent("Focus Origin (O)"), false, FocusOnOrigin);
            menu.AddItem(new GUIContent("Match Level Bounds"), false, AdjustViewToLevelDimensions);
        }

        menu.ShowAsContext();
        e.Use();
    }

    private void CompleteConnection(Vector2 screenMousePos)
    {
        Vector2 worldMousePos = ScreenToWorldPosition(screenMousePos);

        DesignerNode targetNode = null;
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            if (nodes[i] != connectingFromNode && nodes[i].rect.Contains(worldMousePos))
            {
                targetNode = nodes[i];
                break;
            }
        }

        if (targetNode != null)
        {
            if (!ConnectionExists(connectingFromNode, targetNode))
            {
                AddConnection(connectingFromNode, targetNode);
            }
            else
            {
                Debug.LogWarning("Connection already exists.");
            }
        }

        isCreatingConnection = false;
        connectingFromNode = null;
        GUI.changed = true;
    }
    #endregion

    #region Node & Connection Management
    private void CreateNewNode(Vector2 worldPos, NodeType type = NodeType.Rect)
    {
        string baseName = string.IsNullOrEmpty(newNodeName) ? type.ToString() : newNodeName;
        string id = $"{baseName.Replace(" ", "")}_{nextNodeNumber++}";

        while (nodes.Any(n => n.id == id))
        {
            id = $"{baseName.Replace(" ", "")}_{nextNodeNumber++}";
        }

        // Use 20% of level dimensions if a generator exists
        Vector2Int nodeSize = newNodeSize;
        if (targetGenerator != null)
        {
            // Calculate size as 20% of level dimensions
            nodeSize = new Vector2Int(
                Mathf.Max(10, Mathf.RoundToInt(targetGenerator.levelWidth * 0.2f)),
                Mathf.Max(10, Mathf.RoundToInt(targetGenerator.levelHeight * 0.2f))
            );
        }

        DesignerNode newNode = new DesignerNode(id, type, worldPos, nodeSize, globalNodeVisualSize, baseName);

        if (type == NodeType.Template)
        {
            newNode.templatePrefab = newTemplateReference;
            newNode.templateName = newTemplateReference?.name ?? "";
        }

        nodes.Add(newNode);
        selectedNode = newNode;
        selectedConnection = null;
        hasUnsavedChanges = true;
        GUI.changed = true;
    }

    private void DeleteNode(DesignerNode nodeToDelete)
    {
        if (nodeToDelete == null) return;

        List<Connection> connectionsToRemove = connections.Where(c =>
            c.fromNode == nodeToDelete || c.toNode == nodeToDelete).ToList();

        foreach (var conn in connectionsToRemove)
        {
            connections.Remove(conn);

            if (conn.fromNode != nodeToDelete) conn.fromNode?.connections.Remove(nodeToDelete.id);
            if (conn.toNode != nodeToDelete) conn.toNode?.connections.Remove(nodeToDelete.id);
        }

        nodes.Remove(nodeToDelete);

        if (selectedNode == nodeToDelete) selectedNode = null;
        selectedConnection = null;

        hasUnsavedChanges = true;
        GUI.changed = true;
    }

    private bool ConnectionExists(DesignerNode from, DesignerNode to)
    {
        return connections.Any(c =>
            (c.fromNode == from && c.toNode == to) ||
            (c.fromNode == to && c.toNode == from));
    }

    private void AddConnection(DesignerNode from, DesignerNode to)
    {
        if (from == null || to == null || from == to || ConnectionExists(from, to))
            return;

        Connection newConnection = new Connection(from, to);
        connections.Add(newConnection);

        from.connections.Add(to.id);
        to.connections.Add(from.id);

        hasUnsavedChanges = true;
        GUI.changed = true;
    }

    private void RemoveConnection(DesignerNode from, DesignerNode to)
    {
        if (from == null || to == null) return;

        Connection connectionToRemove = connections.FirstOrDefault(c =>
            (c.fromNode == from && c.toNode == to) ||
            (c.fromNode == to && c.toNode == from));

        if (connectionToRemove != null)
        {
            connections.Remove(connectionToRemove);
            from.connections.Remove(to.id);
            to.connections.Remove(from.id);

            if (selectedConnection == connectionToRemove)
                selectedConnection = null;

            hasUnsavedChanges = true;
            GUI.changed = true;
        }
    }
    #endregion

    #region View Controls
    private void CenterView()
    {
        if (nodes.Count == 0)
        {
            FocusOnOrigin();
            return;
        }

        Rect bounds = nodes[0].rect;
        for (int i = 1; i < nodes.Count; i++)
        {
            bounds = Rect.MinMaxRect(
                Mathf.Min(bounds.xMin, nodes[i].rect.xMin),
                Mathf.Min(bounds.yMin, nodes[i].rect.yMin),
                Mathf.Max(bounds.xMax, nodes[i].rect.xMax),
                Mathf.Max(bounds.yMax, nodes[i].rect.yMax)
            );
        }

        float padding = 50f;
        float requiredWidth = bounds.width + 2 * padding;
        float requiredHeight = bounds.height + 2 * padding;

        if (requiredWidth <= 0 || requiredHeight <= 0)
        {
            FocusOnOrigin();
            return;
        }

        float zoomX = graphViewRect.width / requiredWidth;
        float zoomY = graphViewRect.height / requiredHeight;

        zoom = Mathf.Clamp(Mathf.Min(zoomX, zoomY), 0.1f, 5.0f);

        Vector2 worldCenter = bounds.center;
        Vector2 screenCenter = graphViewRect.size / 2f;

        panOffset = screenCenter - (worldCenter * zoom);

        GUI.changed = true;
    }

    private void FocusOnOrigin()
    {
        // Use level bounds if available, otherwise use default view
        if (targetGenerator != null)
        {
            AdjustViewToLevelDimensions();
        }
        else
        {
            zoom = 1.0f;
            Vector2 screenCenter = graphViewRect.size / 2f;
            panOffset = screenCenter;
            GUI.changed = true;
        }
    }

    private void AutoArrangeNodes()
    {
        if (nodes.Count == 0) return;

        Vector2 startPos = ScreenToWorldPosition(new Vector2(100, 100));
        float currentX = startPos.x;
        float currentY = startPos.y;
        float maxYInRow = currentY;

        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].SetPosition(new Vector2(currentX, currentY));

            maxYInRow = Mathf.Max(maxYInRow, currentY + nodes[i].rect.height);

            if ((i + 1) % nodesPerRow == 0)
            {
                currentX = startPos.x;
                currentY = maxYInRow + nodeSpacing * 0.5f;
                maxYInRow = currentY;
            }
            else
            {
                currentX += nodeSpacing;
            }
        }

        hasUnsavedChanges = true;
        GUI.changed = true;
    }
    #endregion

    #region Coordinate Conversion
    private Vector2 ScreenToWorldPosition(Vector2 screenPos)
    {
        Vector2 adjustedScreenPos = screenPos - graphViewRect.position;
        return (adjustedScreenPos - panOffset) / zoom;
    }

    private Vector2 WorldToScreenPosition(Vector2 worldPos)
    {
        Vector2 adjustedScreenPos = (worldPos * zoom) + panOffset;
        return adjustedScreenPos + graphViewRect.position;
    }

    private Rect WorldToScreenRect(Rect worldRect)
    {
        Vector2 screenPos = WorldToScreenPosition(worldRect.position);
        return new Rect(screenPos.x, screenPos.y, worldRect.width * zoom, worldRect.height * zoom);
    }
    #endregion

    #region Save/Load/Export
    private bool AskSaveChanges()
    {
        if (hasUnsavedChanges)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Unsaved Changes",
                "You have unsaved changes in the Visual Level Designer.",
                "Save", "Don't Save", "Cancel");

            if (choice == 0) return SaveDesign();
            else if (choice == 1) return true;
            else return false;
        }
        return true;
    }

    private void NewDesign(bool prompt = true)
    {
        if (prompt && !AskSaveChanges()) return;

        nodes.Clear();
        connections.Clear();
        panOffset = Vector2.zero;
        zoom = 1.0f;
        selectedNode = null;
        selectedConnection = null;
        connectingFromNode = null;
        isCreatingConnection = false;
        nextNodeNumber = 1;
        currentFilePath = "";
        hasUnsavedChanges = false;

        this.titleContent = new GUIContent("Visual Level Designer"); // Reset title

        GUI.changed = true;
        FocusOnOrigin();
    }

    private bool SaveDesign()
    {
        if (string.IsNullOrEmpty(currentFilePath))
        {
            return SaveDesignAs();
        }
        else
        {
            return SaveToFile(currentFilePath);
        }
    }

    private bool SaveDesignAs()
    {
        string path = EditorUtility.SaveFilePanel(
            "Save Level Design As...",
            Application.dataPath,
            "NewLevelDesign",
            "json");

        if (!string.IsNullOrEmpty(path))
        {
            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }

            currentFilePath = path;
            this.titleContent = new GUIContent("Visual Level Designer"); // Remove asterisk

            return SaveToFile(currentFilePath);
        }

        return false;
    }

    private bool SaveToFile(string path)
    {
        try
        {
            DesignerData data = new DesignerData();

            foreach (var node in nodes)
            {
                if (node == null || node.id == null) continue;

                data.nodes.Add(new DesignerData.NodeInfo
                {
                    id = node.id,
                    displayName = node.displayName,
                    nodeType = (int)node.nodeType,
                    x = node.position.x,
                    y = node.position.y,
                    logicalWidth = node.size.x,
                    logicalHeight = node.size.y,
                    templateName = node.templateName,
                    visualWidth = node.visualSize.x,
                    visualHeight = node.visualSize.y
                });
            }

            foreach (var conn in connections)
            {
                if (conn == null || conn.fromNode == null || conn.toNode == null ||
                    conn.fromId == null || conn.toId == null) continue;

                data.connections.Add(new DesignerData.ConnectionInfo
                {
                    fromId = conn.fromId,
                    toId = conn.toId
                });
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);

            hasUnsavedChanges = false;
            this.titleContent = new GUIContent("Visual Level Designer"); // Remove asterisk

            Debug.Log($"Level design saved to: {path}");
            AssetDatabase.Refresh();

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving level design to {path}: {e.Message}");
            return false;
        }
    }

    private void OpenDesign()
    {
        if (!AskSaveChanges()) return;

        string path = EditorUtility.OpenFilePanel("Open Level Design", Application.dataPath, "json");

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                DesignerData data = JsonUtility.FromJson<DesignerData>(json);

                if (data == null || data.nodes == null || data.connections == null)
                {
                    throw new Exception("Invalid JSON data format.");
                }

                nodes.Clear();
                connections.Clear();
                nextNodeNumber = 1;

                Dictionary<string, DesignerNode> nodeMap = new Dictionary<string, DesignerNode>();

                foreach (var nodeInfo in data.nodes)
                {
                    if (string.IsNullOrEmpty(nodeInfo.id)) continue;

                    if (nodeInfo.id.Contains("_"))
                    {
                        int num;
                        if (int.TryParse(nodeInfo.id.Split('_').LastOrDefault(), out num))
                        {
                            nextNodeNumber = Mathf.Max(nextNodeNumber, num + 1);
                        }
                    }

                    // Use loaded visual size if available, otherwise global default
                    Vector2 loadedVisualSize = (nodeInfo.visualWidth > 0 && nodeInfo.visualHeight > 0)
                                            ? new Vector2(nodeInfo.visualWidth, nodeInfo.visualHeight)
                                            : globalNodeVisualSize;

                    DesignerNode newNode = new DesignerNode(
                        nodeInfo.id,
                        (NodeType)nodeInfo.nodeType,
                        new Vector2(nodeInfo.x, nodeInfo.y),
                        new Vector2Int(nodeInfo.logicalWidth, nodeInfo.logicalHeight),
                        loadedVisualSize,
                        nodeInfo.displayName
                    );

                    newNode.templateName = nodeInfo.templateName;

                    if (newNode.nodeType == NodeType.Template && targetGenerator != null &&
                        !string.IsNullOrEmpty(newNode.templateName))
                    {
                        newNode.templatePrefab = targetGenerator.roomTemplatePrefabs?.FirstOrDefault(
                            p => p != null && p.name == newNode.templateName);
                    }

                    nodes.Add(newNode);
                    nodeMap[newNode.id] = newNode;
                }

                foreach (var connInfo in data.connections)
                {
                    if (!string.IsNullOrEmpty(connInfo.fromId) && !string.IsNullOrEmpty(connInfo.toId) &&
                        nodeMap.TryGetValue(connInfo.fromId, out DesignerNode from) &&
                        nodeMap.TryGetValue(connInfo.toId, out DesignerNode to))
                    {
                        AddConnection(from, to);
                    }
                    else
                    {
                        Debug.LogWarning($"Skipping invalid connection on load: {connInfo.fromId} -> {connInfo.toId}");
                    }
                }

                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }

                currentFilePath = path;
                hasUnsavedChanges = false;
                selectedNode = null;
                selectedConnection = null;

                CenterView();
                GUI.changed = true;
                this.titleContent = new GUIContent("Visual Level Designer");

                Debug.Log($"Level design loaded from: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error opening level design from {path}: {e.Message}");
                EditorUtility.DisplayDialog("Load Error", $"Failed to load design file.\nError: {e.Message}", "OK");
            }
        }
    }

    private void CreateSceneObjectsAndGenerate()
    {
        Debug.Log($"[VLE] Starting CreateSceneObjectsAndGenerate...");

        if (targetGenerator == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a Target Generator in the toolbar first.", "OK");
            Debug.LogError("[VLE] Target Generator not assigned.");
            return;
        }

        // Check if there are any nodes in the internal list BEFORE trying to create objects
        if (nodes == null || nodes.Count == 0)
        {
            Debug.LogWarning($"[VLE] The internal 'nodes' list is empty or null. No nodes were designed in the editor window?");
            // Display a specific error to the user in this case
            EditorUtility.DisplayDialog("Error", "Cannot create scene objects. No rooms have been added to the visual design window yet.", "OK");
            return; // Exit early if no nodes exist in the design
        }

        Debug.Log($"[VLE] Found {nodes.Count} nodes in the internal design list. Proceeding to create scene objects.");

        if (!EditorUtility.DisplayDialog("Create Scene Objects & Generate",
            $"This will create/overwrite {nodes.Count} RoomNode GameObject(s) in the scene under 'LevelDesignRoot', " +
            $"set the generator mode to UserDefinedLayout, and run generation. The created scene objects " +
            $"will persist until 'Clear Level' is used.\n\nContinue?",
            "Yes", "No"))
        {
            Debug.Log("[VLE] User cancelled operation.");
            return;
        }

        // Find or Create Root Object
        GameObject designRoot = GameObject.Find("LevelDesignRoot");
        if (designRoot == null)
        {
            designRoot = new GameObject("LevelDesignRoot");
            Debug.Log($"[VLE] Created new LevelDesignRoot object.");
            Undo.RegisterCreatedObjectUndo(designRoot, "Create Level Design Root");
        }
        else
        {
            Debug.Log($"[VLE] Found existing LevelDesignRoot. Clearing its children...");
            int childCount = designRoot.transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(designRoot.transform.GetChild(i).gameObject);
            }
            Debug.Log($"[VLE] Cleared {childCount} children.");
        }

        // Setup Layer
        int designLayer = LayerMask.NameToLayer("LevelDesignNodes");
        if (designLayer == -1)
        {
            Debug.LogWarning("[VLE] Layer 'LevelDesignNodes' not found.");
            designLayer = 0;
        }
        designRoot.layer = designLayer;

        // Create Node GameObjects and Components
        Dictionary<string, RoomNode> createdNodeComponents = new Dictionary<string, RoomNode>();
        int createdCount = 0; // Counter for verification

        foreach (var node in nodes)
        {
            if (node == null)
            {
                Debug.LogWarning("[VLE] Encountered a null node in the design list, skipping.");
                continue; // Skip null entries if any exist
            }

            Debug.Log($"[VLE] Processing design node: ID='{node.id}', Name='{node.displayName}'");

            GameObject nodeGO = null; // Declare outside try-catch
            try
            {
                nodeGO = new GameObject(node.displayName);
                Undo.RegisterCreatedObjectUndo(nodeGO, "Create Room Node Object");
                nodeGO.transform.SetParent(designRoot.transform);
                nodeGO.transform.position = new Vector3(node.position.x, node.position.y, 0);
                nodeGO.layer = designLayer;
                Debug.Log($"[VLE] -- Created GameObject '{nodeGO.name}'");

                RoomNode rnComponent = Undo.AddComponent<RoomNode>(nodeGO);
                if (rnComponent == null)
                {
                    Debug.LogError($"[VLE] -- FAILED to add RoomNode component to '{nodeGO.name}'! Check for errors with RoomNode.cs.", nodeGO);
                    continue; // Skip this node if component fails
                }

                Debug.Log($"[VLE] -- Added RoomNode component.");

                rnComponent.roomId = node.id;
                rnComponent.roomType = node.nodeType;
                rnComponent.roomTemplatePrefab = node.templatePrefab;
                rnComponent.roomSize = node.size;
                rnComponent.connectedRooms = new List<RoomNode>();
                createdNodeComponents[node.id] = rnComponent;
                createdCount++; // Increment success counter
                Debug.Log($"[VLE] -- Populated RoomNode properties.");
            }
            catch (Exception ex)
            {
                // Log any exception during node creation/setup
                Debug.LogError($"[VLE] Exception occurred while processing node '{node?.displayName ?? "NULL"}': {ex.Message}\n{ex.StackTrace}", nodeGO);
                // Optionally destroy the partially created node if an error occurred
                if (nodeGO != null)
                    Undo.DestroyObjectImmediate(nodeGO);
            }
        }

        Debug.Log($"[VLE] Finished node creation loop. Successfully created {createdCount} node GameObjects.");

        // Link Connections (only if nodes were created)
        if (createdCount > 0)
        {
            Debug.Log("[VLE] Linking connections...");

            foreach (var connection in connections)
            {
                if (createdNodeComponents.TryGetValue(connection.fromId, out RoomNode fromComp) &&
                   createdNodeComponents.TryGetValue(connection.toId, out RoomNode toComp))
                {
                    // Ensure lists are initialized
                    if (fromComp.connectedRooms == null)
                        fromComp.connectedRooms = new List<RoomNode>();
                    if (toComp.connectedRooms == null)
                        toComp.connectedRooms = new List<RoomNode>();

                    if (!fromComp.connectedRooms.Contains(toComp))
                        fromComp.connectedRooms.Add(toComp);
                    if (!toComp.connectedRooms.Contains(fromComp))
                        toComp.connectedRooms.Add(fromComp);
                }
            }

            Debug.Log("[VLE] Finished linking connections.");
        }
        else
        {
            Debug.LogWarning("[VLE] Skipping connection linking as no nodes were successfully created.");
        }

        // Mark components as dirty so connections are saved
        foreach (var comp in createdNodeComponents.Values)
            EditorUtility.SetDirty(comp);
        EditorUtility.SetDirty(designRoot);

        // Configure and Run Generator (only if nodes were created)
        if (createdCount > 0)
        {
            Undo.RecordObject(targetGenerator, "Set Generation Mode and Generate");
            targetGenerator.generationMode = GenerationMode.UserDefinedLayout;
            EditorUtility.SetDirty(targetGenerator);
            Debug.Log($"[VLE] Set {targetGenerator.name} mode to UserDefinedLayout and triggering generation...");

            targetGenerator.GenerateLevel(true); // Use true to skip ClearLevel in generator

            EditorGUIUtility.PingObject(designRoot);

            if (nodesOutsideBounds.Count > 0)
            {
                string warningMessage = $"{nodesOutsideBounds.Count} room(s) extend beyond level boundaries " +
                    $"and will be truncated to fit within the level dimensions ({targetGenerator.levelWidth}x{targetGenerator.levelHeight}).\n\n" +
                    $"Affected rooms: {string.Join(", ", nodesOutsideBounds.Select(n => n.displayName))}";

                EditorUtility.DisplayDialog("Boundary Warning", warningMessage, "OK");
            }


            EditorUtility.DisplayDialog("Process Complete",
                $"Scene objects created/updated under '{designRoot.name}'.\n" +
                $"Level generation triggered using UserDefinedLayout mode.\n" +
                $"Check the scene view and console.", "OK");
        }
        else
        {
            Debug.LogError("[VLE] Generation not triggered because no RoomNode objects were successfully created.");
            EditorUtility.DisplayDialog("Error", "Failed to create any RoomNode objects from the design. Check the console for details.", "OK");
        }
    }
    #endregion

    #region Helpers
    private string GetTypeIcon(NodeType type)
    {
        switch (type)
        {
            case NodeType.Rect: return "■";
            case NodeType.LShape: return "L";
            case NodeType.Template: return "T";
            default: return "?";
        }
    }

    private bool IsPositionOnConnection(Vector2 screenPos, Connection connection)
    {
        if (connection?.fromNode == null || connection?.toNode == null)
            return false;

        Vector2 start = WorldToScreenPosition(connection.fromNode.position);
        Vector2 end = WorldToScreenPosition(connection.toNode.position);

        float threshold = 8f * zoom;
        float segmentLengthSqr = (end - start).sqrMagnitude;

        if (segmentLengthSqr == 0.0f)
            return Vector2.Distance(screenPos, start) < threshold;

        float t = Vector2.Dot(screenPos - start, end - start) / segmentLengthSqr;
        t = Mathf.Clamp01(t);

        Vector2 projection = start + t * (end - start);
        return Vector2.Distance(screenPos, projection) < threshold;
    }
    #endregion
}