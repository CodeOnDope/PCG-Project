using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

// Note: Assumes NodeType enum is defined in a separate file (e.g., LevelGenerationData.cs)

public class VisualLevelDesignEditor : EditorWindow
{
    #region Data Structures
    private class DesignerNode
    { /* ... Definition unchanged ... */
        public string id; public string displayName; public NodeType nodeType; public Vector2 position; public Vector2Int size; public string templateName; public GameObject templatePrefab; public List<string> connections = new List<string>(); public Rect rect; public bool isSelected; public bool isDragging; public Color typeColor; public Vector2 visualSize;
        public DesignerNode(string id, NodeType type, Vector2 position, Vector2Int logicalSize, Vector2 initialVisualSize, string name = null) { this.id = id; this.nodeType = type; this.position = position; this.size = logicalSize; this.isSelected = false; this.isDragging = false; this.displayName = string.IsNullOrEmpty(name) ? $"{type}_{id}" : name; this.visualSize = initialVisualSize; switch (type) { case NodeType.Rect: this.typeColor = UnityEngine.ColorUtility.TryParseHtmlString("#3B82F6", out Color cRect) ? cRect : Color.blue; break; case NodeType.LShape: this.typeColor = UnityEngine.ColorUtility.TryParseHtmlString("#10B981", out Color cL) ? cL : Color.green; break; case NodeType.Template: this.typeColor = UnityEngine.ColorUtility.TryParseHtmlString("#8B5CF6", out Color cT) ? cT : Color.magenta; break; default: this.typeColor = Color.grey; break; } UpdateRect(); }
        public void UpdateRect() { this.rect = new Rect(position.x - visualSize.x / 2f, position.y - visualSize.y / 2f, visualSize.x, visualSize.y); }
        public void SetPosition(Vector2 newPosition) { this.position = newPosition; UpdateRect(); }
        public void SetVisualSize(Vector2 newSize) { this.visualSize.x = Mathf.Max(20f, newSize.x); this.visualSize.y = Mathf.Max(20f, newSize.y); UpdateRect(); }
    }
    private class Connection
    { /* ... Definition unchanged ... */
        public DesignerNode fromNode; public DesignerNode toNode; public string fromId => fromNode?.id; public string toId => toNode?.id;
        public Connection(DesignerNode from, DesignerNode to) { this.fromNode = from; this.toNode = to; }
        public override bool Equals(object obj) { if (obj is Connection other) { return (fromNode == other.fromNode && toNode == other.toNode) || (fromNode == other.toNode && toNode == other.fromNode); } return false; }
        public override int GetHashCode() { int hash1 = fromNode?.GetHashCode() ?? 0; int hash2 = toNode?.GetHashCode() ?? 0; return hash1 ^ hash2; }
    }
    // JSON Data structures re-added for Save/Load
    [System.Serializable]
    public class DesignerData
    { /* ... Definition unchanged ... */
        public List<NodeInfo> nodes = new List<NodeInfo>(); public List<ConnectionInfo> connections = new List<ConnectionInfo>();
        [System.Serializable] public class NodeInfo { public string id; public string displayName; public int nodeType; public float x, y; public int logicalWidth, logicalHeight; public string templateName; public float visualWidth; public float visualHeight; }
        [System.Serializable] public class ConnectionInfo { public string fromId; public string toId; }
    }
    #endregion

    #region Editor State Variables
    // ... (Most variables unchanged) ...
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
    private Vector2 globalNodeVisualSize = new Vector2(80f, 40f);
    private Color globalNodeColor = Color.gray;
    private bool useNodeTypeColor = true;
    private bool hasUnsavedChanges = false;
    private string currentFilePath = "";
    #endregion

    #region Unity Editor Methods
    [MenuItem("Window/Visual Level Designer")] public static void ShowWindow() { GetWindow<VisualLevelDesignEditor>("Visual Level Designer").Show(); }
    private void OnEnable() { /* ... unchanged ... */ targetGenerator = FindObjectOfType<HybridLevelGenerator>(); nextNodeNumber = 1; if (nodes.Count == 0) { NewDesign(false); } EditorApplication.delayCall += () => { FocusOnOrigin(); Repaint(); }; }
    private void InitializeStyles() { /* ... unchanged ... */ if (stylesInitialized) return; nodeStyle = new GUIStyle(GUI.skin.box); nodeStyle.normal.background = MakeColorTexture(new Color(0.2f, 0.2f, 0.25f, 1f)); nodeStyle.border = new RectOffset(6, 6, 6, 6); nodeStyle.padding = new RectOffset(5, 5, 5, 5); nodeStyle.alignment = TextAnchor.UpperCenter; nodeStyle.normal.textColor = Color.white; nodeStyle.fontSize = 9; selectedNodeStyle = new GUIStyle(nodeStyle); selectedNodeStyle.normal.background = MakeColorTexture(new Color(0.4f, 0.6f, 0.9f, 1f)); headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } }; subHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } }; instructionStyle = new GUIStyle(EditorStyles.helpBox) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }, padding = new RectOffset(10, 10, 10, 10) }; connectionButtonStyle = new GUIStyle(EditorStyles.miniButton) { padding = new RectOffset(3, 3, 1, 1), margin = new RectOffset(0, 0, 2, 0), fontSize = 9 }; coordinateLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperLeft, normal = { textColor = Color.black * 0.6f } }; sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, alignment = TextAnchor.MiddleLeft }; helpContent = new GUIContent("Help:\n• Right-click empty space: Create Room\n• Left-click drag node: Move\n• Click 'Connect' button: Start line\n• Click target node: Finish line\n• Right-click node/line: Options\n• Middle-click drag: Pan\n• Mouse wheel: Zoom\n• F key: Center View (Frames Nodes)\n• O key: Focus Origin (0,0)\n• Toggle 'Snap Nodes' in toolbar"); stylesInitialized = true; }
    private Texture2D MakeColorTexture(Color color) { /* ... unchanged ... */ Texture2D tex = new Texture2D(1, 1); tex.SetPixel(0, 0, color); tex.Apply(); return tex; }
    private void OnGUI() { /* ... unchanged ... */ if (!stylesInitialized) InitializeStyles(); HandleKeyboardShortcuts(); EditorGUILayout.BeginVertical(); DrawToolbar(); EditorGUILayout.BeginHorizontal(); Rect potentialGraphRect = GUILayoutUtility.GetRect(100, 10000, 100, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)); if (Event.current.type == EventType.Repaint) { graphViewRect = potentialGraphRect; } DrawGraphArea(graphViewRect); DrawSidePanels(); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); ProcessEvents(Event.current); if (GUI.changed) Repaint(); }
    #endregion

    #region Drawing Methods
    private void DrawToolbar() { /* ... Re-added Save/Load ... */ EditorGUILayout.BeginHorizontal(EditorStyles.toolbar); if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50))) { NewDesign(); } if (GUILayout.Button("Open", EditorStyles.toolbarButton, GUILayout.Width(50))) { OpenDesign(); } if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50))) { SaveDesign(); } if (GUILayout.Button("Save As", EditorStyles.toolbarButton, GUILayout.Width(60))) { SaveDesignAs(); } GUILayout.Space(20); showInstructions = GUILayout.Toggle(showInstructions, "Show Help", EditorStyles.toolbarButton); snapToGrid = GUILayout.Toggle(snapToGrid, "Snap Nodes", EditorStyles.toolbarButton); GUILayout.Space(10); if (GUILayout.Button("Auto-Arrange", EditorStyles.toolbarButton, GUILayout.Width(80))) { AutoArrangeNodes(); } if (GUILayout.Button("Center View (F)", EditorStyles.toolbarButton, GUILayout.Width(100))) { CenterView(); } if (GUILayout.Button("Focus Origin (O)", EditorStyles.toolbarButton, GUILayout.Width(100))) { FocusOnOrigin(); } GUILayout.FlexibleSpace(); targetGenerator = EditorGUILayout.ObjectField("Target Generator:", targetGenerator, typeof(HybridLevelGenerator), true, GUILayout.Width(250)) as HybridLevelGenerator; EditorGUILayout.EndHorizontal(); }
    private void DrawGraphArea(Rect availableRect) { /* ... unchanged ... */ GUI.Box(availableRect, "", EditorStyles.helpBox); GUI.BeginClip(availableRect); Vector2 graphSize = availableRect.size; DrawGrid(graphSize, gridSnapSize, 0.2f, Color.gray); DrawGrid(graphSize, gridSnapSize * 5f, 0.4f, Color.gray); DrawOriginMarker(graphSize); DrawCoordinateLabels(graphSize); DrawGeneratorBoundsRect(graphSize); DrawConnections(graphSize); DrawNodes(graphSize); if (isCreatingConnection && connectingFromNode != null) { DrawConnectionInProgress(graphSize); } GUI.EndClip(); if (showInstructions && instructionStyle != null && helpContent != null) { float helpWidth = Mathf.Min(350, availableRect.width - 20); float helpHeight = stylesInitialized ? instructionStyle.CalcHeight(helpContent, helpWidth) : 110f; Rect helpRect = new Rect(availableRect.x + 10, availableRect.y + 10, helpWidth, helpHeight); GUI.Box(helpRect, helpContent, instructionStyle); } }
    private void DrawSidePanels() { /* ... Moved Generate Button ... */ EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(Mathf.Max(250, position.width * 0.25f)), GUILayout.ExpandHeight(true)); sidePanelScrollPos = EditorGUILayout.BeginScrollView(sidePanelScrollPos); DrawGlobalSettingsPanel(); EditorGUILayout.Space(10); DrawRoomCreationPanel(); EditorGUILayout.Space(10); DrawNodePropertiesPanel(); EditorGUILayout.EndScrollView(); EditorGUILayout.Space(10); Color defaultBg = GUI.backgroundColor; GUI.backgroundColor = Color.green * 1.2f; if (GUILayout.Button("Create Scene Objects & Generate", GUILayout.Height(35))) { CreateSceneObjectsAndGenerate(); } GUI.backgroundColor = defaultBg; EditorGUILayout.Space(5); EditorGUILayout.EndVertical(); }
    private void DrawGrid(Vector2 viewSize, float gridSpacing, float gridOpacity, Color gridColor) { /* ... unchanged ... */ Handles.BeginGUI(); Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity); float zoomedSpacing = gridSpacing * zoom; if (zoomedSpacing < 4f) { Handles.EndGUI(); return; } Vector2 offset = new Vector2(panOffset.x % zoomedSpacing, panOffset.y % zoomedSpacing); int widthDivs = Mathf.CeilToInt(viewSize.x / zoomedSpacing); int heightDivs = Mathf.CeilToInt(viewSize.y / zoomedSpacing); Vector2 originScreenPos = WorldToScreenPosition(Vector2.zero); for (int i = -1; i <= widthDivs; i++) { float x = zoomedSpacing * i + offset.x; bool isOriginLine = Mathf.Abs(x - (originScreenPos.x - graphViewRect.x)) < 1f; Handles.color = isOriginLine ? new Color(0.1f, 0.1f, 0.1f, 0.5f) : new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity); Handles.DrawLine(new Vector3(x, 0, 0), new Vector3(x, viewSize.y, 0)); } for (int j = -1; j <= heightDivs; j++) { float y = zoomedSpacing * j + offset.y; bool isOriginLine = Mathf.Abs(y - (originScreenPos.y - graphViewRect.y)) < 1f; Handles.color = isOriginLine ? new Color(0.1f, 0.1f, 0.1f, 0.5f) : new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity); Handles.DrawLine(new Vector3(0, y, 0), new Vector3(viewSize.x, y, 0)); } Handles.color = Color.white; Handles.EndGUI(); }
    private void DrawOriginMarker(Vector2 graphSize) { /* ... unchanged ... */ Vector2 originScreenPos = WorldToScreenPosition(Vector2.zero); float markerSize = 5f * zoom; if (originScreenPos.x >= graphViewRect.x && originScreenPos.x <= graphViewRect.x + graphSize.x && originScreenPos.y >= graphViewRect.y && originScreenPos.y <= graphViewRect.y + graphSize.y) { Handles.BeginGUI(); Handles.color = Color.red; Vector2 screenOriginRelative = originScreenPos - graphViewRect.position; Handles.DrawLine(screenOriginRelative + Vector2.left * markerSize, screenOriginRelative + Vector2.right * markerSize); Handles.DrawLine(screenOriginRelative + Vector2.up * markerSize, screenOriginRelative + Vector2.down * markerSize); Handles.color = Color.white; Handles.EndGUI(); } }
    private void DrawCoordinateLabels(Vector2 viewSize) { /* ... unchanged ... */ if (!stylesInitialized || coordinateLabelStyle == null) return; Handles.BeginGUI(); float majorGridSpacing = gridSnapSize * 5f; float labelSpacing = majorGridSpacing * zoom; if (labelSpacing < 40f) labelSpacing *= 2f; if (labelSpacing < 20f) { Handles.EndGUI(); return; } Vector2 offset = panOffset; Vector2 topLeftWorld = ScreenToWorldPosition(Vector2.zero); Vector2 bottomRightWorld = ScreenToWorldPosition(viewSize); float labelStep = majorGridSpacing; if (labelSpacing < 40f) labelStep *= 2f; float firstWorldX = Mathf.Ceil(topLeftWorld.x / labelStep) * labelStep; float firstWorldY = Mathf.Ceil(topLeftWorld.y / labelStep) * labelStep; for (float worldX = firstWorldX; worldX < bottomRightWorld.x; worldX += labelStep) { Vector2 screenPos = WorldToScreenPosition(new Vector2(worldX, topLeftWorld.y)); if (screenPos.x < graphViewRect.x || screenPos.x > graphViewRect.x + viewSize.x) continue; Rect labelRect = new Rect(screenPos.x + 2, graphViewRect.y + 2, 50, 15); GUI.Label(labelRect, worldX.ToString("F0"), coordinateLabelStyle); } for (float worldY = firstWorldY; worldY < bottomRightWorld.y; worldY += labelStep) { Vector2 screenPos = WorldToScreenPosition(new Vector2(topLeftWorld.x, worldY)); if (screenPos.y < graphViewRect.y || screenPos.y > graphViewRect.y + viewSize.y) continue; Rect labelRect = new Rect(graphViewRect.x + 2, screenPos.y + 2, 50, 15); GUI.Label(labelRect, worldY.ToString("F0"), coordinateLabelStyle); } Handles.EndGUI(); }
    private void DrawGeneratorBoundsRect(Vector2 graphSize) { /* ... unchanged ... */ if (targetGenerator == null) return; float halfW = targetGenerator.levelWidth / 2f; float halfH = targetGenerator.levelHeight / 2f; Vector2 worldBottomLeft = new Vector2(-halfW, -halfH); Vector2 worldTopLeft = new Vector2(-halfW, halfH); Vector2 worldTopRight = new Vector2(halfW, halfH); Vector2 worldBottomRight = new Vector2(halfW, -halfH); Vector2 screenBL = WorldToScreenPosition(worldBottomLeft); Vector2 screenTL = WorldToScreenPosition(worldTopLeft); Vector2 screenTR = WorldToScreenPosition(worldTopRight); Vector2 screenBR = WorldToScreenPosition(worldBottomRight); Handles.BeginGUI(); Handles.color = new Color(0f, 0.8f, 0.8f, 0.4f); Handles.DrawAAPolyLine(3f, screenBL, screenTL, screenTR, screenBR, screenBL); Handles.color = Color.white; Handles.EndGUI(); }
    private void DrawNodes(Vector2 graphSize) { /* ... unchanged ... */ if (nodes.Count == 0) { GUI.Label(new Rect(graphSize.x / 2 - 100, graphSize.y / 2 - 10, 200, 20), "Right-click to create a room.", EditorStyles.centeredGreyMiniLabel); return; } for (int i = nodes.Count - 1; i >= 0; i--) { var node = nodes[i]; node.UpdateRect(); Rect displayRect = WorldToScreenRect(node.rect); if (displayRect.xMax < 0 || displayRect.x > graphSize.x || displayRect.yMax < 0 || displayRect.y > graphSize.y) continue; Color nodeBgColor = useNodeTypeColor ? node.typeColor : globalNodeColor; GUI.backgroundColor = nodeBgColor; GUIStyle style = node.isSelected ? selectedNodeStyle : nodeStyle; GUIStyle tempStyle = new GUIStyle(style); tempStyle.normal.background = MakeColorTexture(GUI.backgroundColor); if (node.isSelected) { Color selectedBg = Color.Lerp(nodeBgColor, Color.white, 0.3f); tempStyle.normal.background = MakeColorTexture(selectedBg); } GUI.Box(displayRect, "", tempStyle); GUI.backgroundColor = Color.white; GUILayout.BeginArea(displayRect); string typeIcon = GetTypeIcon(node.nodeType); string title = $" {typeIcon} {node.displayName}"; GUILayout.Label(title, subHeaderStyle); GUILayout.Label($"ID: {node.id}", EditorStyles.miniLabel); if (node.nodeType == NodeType.Template && !string.IsNullOrEmpty(node.templateName)) { GUILayout.Label($"Tmpl: {node.templateName}", EditorStyles.miniLabel); } if (GUILayout.Button("Connect", connectionButtonStyle)) { connectingFromNode = node; isCreatingConnection = true; GUI.changed = true; } GUILayout.EndArea(); } }
    private void DrawConnections(Vector2 graphSize) { /* ... unchanged ... */ Handles.BeginGUI(); List<Connection> connectionsToDelete = null; foreach (var connection in connections) { if (connection.fromNode == null || connection.toNode == null) continue; Vector2 startPos = WorldToScreenPosition(connection.fromNode.position); Vector2 endPos = WorldToScreenPosition(connection.toNode.position); if ((startPos.x < 0 && endPos.x < 0) || (startPos.x > graphSize.x && endPos.x > graphSize.x) || (startPos.y < 0 && endPos.y < 0) || (startPos.y > graphSize.y && endPos.y > graphSize.y)) continue; bool isSelected = (selectedConnection == connection); Handles.color = isSelected ? Color.yellow : Color.cyan; Handles.DrawAAPolyLine(isSelected ? 4 : 3, startPos, endPos); Vector2 midPoint = (startPos + endPos) / 2f; float handleSize = 8f; Rect midPointRect = new Rect(midPoint.x - handleSize / 2, midPoint.y - handleSize / 2, handleSize, handleSize); Handles.color = isSelected ? Color.yellow : new Color(0.5f, 0.8f, 1f, 0.7f); Handles.DrawSolidDisc(midPoint, Vector3.forward, handleSize * 0.5f); if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && midPointRect.Contains(Event.current.mousePosition)) { if (connectionsToDelete == null) connectionsToDelete = new List<Connection>(); connectionsToDelete.Add(connection); Event.current.Use(); } else if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && midPointRect.Contains(Event.current.mousePosition)) { selectedConnection = connection; selectedNode = null; GUI.changed = true; Event.current.Use(); } } Handles.color = Color.white; Handles.EndGUI(); if (connectionsToDelete != null) { foreach (var conn in connectionsToDelete) { if (EditorUtility.DisplayDialog("Delete Connection", $"Delete connection between {conn.fromNode.displayName} and {conn.toNode.displayName}?", "Yes", "No")) { RemoveConnection(conn.fromNode, conn.toNode); } } } }
    private void DrawConnectionInProgress(Vector2 graphSize) { /* ... unchanged ... */ if (connectingFromNode == null) return; Handles.BeginGUI(); Vector2 startPos = WorldToScreenPosition(connectingFromNode.position); Vector2 mousePos = Event.current.mousePosition; Handles.color = Color.yellow; Handles.DrawAAPolyLine(2, startPos, mousePos); Handles.color = Color.white; Handles.EndGUI(); GUI.changed = true; }
    private void DrawRoomCreationPanel() { /* ... unchanged ... */ EditorGUILayout.BeginVertical(EditorStyles.helpBox); EditorGUILayout.LabelField(new GUIContent(" Create New Room", EditorGUIUtility.IconContent("CreateAddNew").image), sectionHeaderStyle); EditorGUILayout.Space(5); newNodeName = EditorGUILayout.TextField("Name:", newNodeName); newNodeType = (NodeType)EditorGUILayout.EnumPopup("Type:", newNodeType); newNodeSize = EditorGUILayout.Vector2IntField("Logical Size:", newNodeSize); if (newNodeType == NodeType.Template) { newTemplateReference = EditorGUILayout.ObjectField("Template Prefab:", newTemplateReference, typeof(GameObject), false) as GameObject; if (targetGenerator != null && targetGenerator.roomTemplatePrefabs != null && targetGenerator.roomTemplatePrefabs.Count > 0) { List<string> names = targetGenerator.roomTemplatePrefabs.Where(p => p != null).Select(p => p.name).ToList(); names.Insert(0, "Select from Generator..."); int currentIndex = newTemplateReference != null ? names.IndexOf(newTemplateReference.name) : 0; if (currentIndex < 0) currentIndex = 0; int newIndex = EditorGUILayout.Popup(" ", currentIndex, names.ToArray()); if (newIndex > 0 && newIndex != currentIndex) { newTemplateReference = targetGenerator.roomTemplatePrefabs.FirstOrDefault(p => p.name == names[newIndex]); } } } if (GUILayout.Button("Create Room", GUILayout.Height(30))) { Vector2 centerPos = graphViewRect.size / 2f; Vector2 worldPos = ScreenToWorldPosition(centerPos); CreateNewNode(worldPos); } EditorGUILayout.EndVertical(); }
    private void DrawGlobalSettingsPanel() { /* ... unchanged ... */ EditorGUILayout.BeginVertical(EditorStyles.helpBox); EditorGUILayout.LabelField(new GUIContent(" Global Node Visuals", EditorGUIUtility.IconContent("Settings").image), sectionHeaderStyle); EditorGUI.BeginChangeCheck(); Vector2 newVisualSize = EditorGUILayout.Vector2Field("Node Visual Size:", globalNodeVisualSize); bool newUseNodeTypeColor = EditorGUILayout.Toggle("Use Color By Node Type", useNodeTypeColor); Color newGlobalColor = globalNodeColor; EditorGUI.BeginDisabledGroup(newUseNodeTypeColor); newGlobalColor = EditorGUILayout.ColorField("Global Node Color:", globalNodeColor); EditorGUI.EndDisabledGroup(); if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(this, "Change Global Node Visuals"); bool updateNodes = false; newVisualSize.x = Mathf.Max(20f, newVisualSize.x); newVisualSize.y = Mathf.Max(20f, newVisualSize.y); if (globalNodeVisualSize != newVisualSize) { globalNodeVisualSize = newVisualSize; updateNodes = true; } if (useNodeTypeColor != newUseNodeTypeColor) { useNodeTypeColor = newUseNodeTypeColor; updateNodes = true; } if (globalNodeColor != newGlobalColor) { globalNodeColor = newGlobalColor; if (!useNodeTypeColor) updateNodes = true; } if (updateNodes) { foreach (var node in nodes) { node.SetVisualSize(globalNodeVisualSize); } GUI.changed = true; } } EditorGUILayout.EndVertical(); }
    private void DrawNodePropertiesPanel() { /* ... unchanged ... */ if (selectedNode == null) return; EditorGUILayout.BeginVertical(EditorStyles.helpBox); EditorGUILayout.LabelField(new GUIContent($" Properties: {selectedNode.displayName}", EditorGUIUtility.IconContent("d_Prefab Icon").image), sectionHeaderStyle); EditorGUILayout.Space(5); EditorGUI.BeginChangeCheck(); GUI.enabled = false; EditorGUILayout.TextField("Room ID:", selectedNode.id); EditorGUILayout.EnumPopup("Type:", selectedNode.nodeType); EditorGUILayout.Vector2Field("World Position (Center):", selectedNode.position); GUI.enabled = true; selectedNode.displayName = EditorGUILayout.TextField("Name:", selectedNode.displayName); selectedNode.size = EditorGUILayout.Vector2IntField("Logical Size:", selectedNode.size); if (selectedNode.nodeType == NodeType.Template) { GameObject newTemplate = EditorGUILayout.ObjectField("Template Prefab:", selectedNode.templatePrefab, typeof(GameObject), false) as GameObject; if (newTemplate != selectedNode.templatePrefab) { selectedNode.templatePrefab = newTemplate; selectedNode.templateName = newTemplate?.name ?? ""; } } EditorGUILayout.Space(5); EditorGUILayout.LabelField("Connections:", EditorStyles.boldLabel); if (selectedNode.connections.Count == 0) { EditorGUILayout.LabelField("None", EditorStyles.miniLabel); } else { List<string> connectionsToRemove = null; foreach (string connectedId in selectedNode.connections) { DesignerNode connectedNode = nodes.FirstOrDefault(n => n.id == connectedId); if (connectedNode == null) continue; EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField($"→ {connectedNode.displayName} ({connectedId})", EditorStyles.miniLabel); if (GUILayout.Button("×", GUILayout.Width(20))) { if (connectionsToRemove == null) connectionsToRemove = new List<string>(); connectionsToRemove.Add(connectedId); } EditorGUILayout.EndHorizontal(); } if (connectionsToRemove != null) { foreach (string idToRemove in connectionsToRemove) { RemoveConnection(selectedNode, nodes.FirstOrDefault(n => n.id == idToRemove)); } } } EditorGUILayout.Space(10); if (GUILayout.Button("Delete Room", GUILayout.Height(25))) { if (EditorUtility.DisplayDialog("Delete Room", $"Delete room \"{selectedNode.displayName}\"?", "Yes", "No")) { DeleteNode(selectedNode); GUIUtility.ExitGUI(); } } if (EditorGUI.EndChangeCheck()) { hasUnsavedChanges = true; } EditorGUILayout.EndVertical(); }
    // Removed DrawExportPanel
    #endregion

    #region Event Handling
    private void HandleKeyboardShortcuts() { /* ... Re-added Save/Load ... */ Event e = Event.current; if (e.type == EventType.KeyDown) { if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) { if (selectedNode != null) { DeleteNode(selectedNode); e.Use(); } else if (selectedConnection != null) { RemoveConnection(selectedConnection.fromNode, selectedConnection.toNode); e.Use(); } } else if (e.control && e.keyCode == KeyCode.S) { SaveDesign(); e.Use(); } else if (e.control && e.shift && e.keyCode == KeyCode.S) { SaveDesignAs(); e.Use(); } else if (e.control && e.keyCode == KeyCode.O) { OpenDesign(); e.Use(); } else if (e.control && e.keyCode == KeyCode.N) { NewDesign(); e.Use(); } else if (e.keyCode == KeyCode.F) { CenterView(); e.Use(); } else if (e.keyCode == KeyCode.O) { FocusOnOrigin(); e.Use(); } else if (e.keyCode == KeyCode.Escape) { if (isCreatingConnection) { isCreatingConnection = false; connectingFromNode = null; e.Use(); } else if (selectedNode != null || selectedConnection != null) { selectedNode = null; selectedConnection = null; e.Use(); } } } }
    private void ProcessEvents(Event e) { /* ... unchanged ... */ switch (e.type) { case EventType.MouseDown: OnMouseDown(e); break; case EventType.MouseUp: OnMouseUp(e); break; case EventType.MouseDrag: OnMouseDrag(e); break; case EventType.ScrollWheel: OnScrollWheel(e); break; case EventType.ContextClick: HandleRightMouseDown(e.mousePosition, e); break; } }
    private void OnMouseDown(Event e) { /* ... unchanged ... */ Vector2 graphSpaceMousePos = ScreenToWorldPosition(e.mousePosition); if (e.button == 0) { HandleLeftMouseDown(graphSpaceMousePos, e); } else if (e.button == 2) { isPanning = true; isDraggingNode = false; isCreatingConnection = false; GUI.FocusControl(null); e.Use(); } }
    private void OnMouseUp(Event e) { /* ... Added Snapping & hasUnsavedChanges ... */ if (e.button == 0) { if (isCreatingConnection && connectingFromNode != null) { CompleteConnection(e.mousePosition); } if (isDraggingNode && selectedNode != null) { if (snapToGrid && gridSnapSize > 0) { Vector2 currentPos = selectedNode.position; float snappedX = Mathf.Round(currentPos.x / gridSnapSize) * gridSnapSize; float snappedY = Mathf.Round(currentPos.y / gridSnapSize) * gridSnapSize; if (snappedX != currentPos.x || snappedY != currentPos.y) { selectedNode.SetPosition(new Vector2(snappedX, snappedY)); hasUnsavedChanges = true; GUI.changed = true; } } isDraggingNode = false; } } else if (e.button == 2) { isPanning = false; } foreach (var node in nodes) node.isDragging = false; }
    private void OnMouseDrag(Event e) { /* ... Added hasUnsavedChanges ... */ if (e.button == 0 && isDraggingNode && selectedNode != null) { Vector2 worldDelta = e.delta / zoom; selectedNode.SetPosition(selectedNode.position + worldDelta); hasUnsavedChanges = true; GUI.changed = true; e.Use(); } else if (e.button == 2 && isPanning) { panOffset += e.delta; GUI.changed = true; e.Use(); } }
    private void OnScrollWheel(Event e) { /* ... unchanged ... */ float zoomDelta = -e.delta.y / 150.0f; float newZoom = zoom + zoomDelta; newZoom = Mathf.Clamp(newZoom, 0.2f, 3.0f); Vector2 mousePos = e.mousePosition; Vector2 worldPos = ScreenToWorldPosition(mousePos); zoom = newZoom; Vector2 newScreenPos = WorldToScreenPosition(worldPos); panOffset += (mousePos - newScreenPos); e.Use(); GUI.changed = true; }
    private void HandleLeftMouseDown(Vector2 worldMousePos, Event e) { /* ... unchanged ... */ bool clickedOnNode = false; selectedConnection = null; for (int i = nodes.Count - 1; i >= 0; i--) { var node = nodes[i]; if (node.rect.Contains(worldMousePos)) { selectedNode = node; isDraggingNode = true; dragStartOffset = worldMousePos - node.position; clickedOnNode = true; GUI.FocusControl(null); e.Use(); break; } } if (!clickedOnNode) { selectedNode = null; GUI.FocusControl(null); } GUI.changed = true; }
    private void HandleRightMouseDown(Vector2 screenMousePos, Event e) { /* ... unchanged ... */ Vector2 worldMousePos = ScreenToWorldPosition(screenMousePos); bool clickedOnNode = false; DesignerNode targetNode = null; for (int i = nodes.Count - 1; i >= 0; i--) { if (nodes[i].rect.Contains(worldMousePos)) { clickedOnNode = true; targetNode = nodes[i]; break; } } GenericMenu menu = new GenericMenu(); if (clickedOnNode) { menu.AddItem(new GUIContent("Delete Room"), false, () => { if (EditorUtility.DisplayDialog("Delete Room", $"Delete room \"{targetNode.displayName}\"?", "Yes", "No")) { DeleteNode(targetNode); } }); menu.AddItem(new GUIContent("Start Connection"), false, () => { connectingFromNode = targetNode; isCreatingConnection = true; }); } else { menu.AddItem(new GUIContent("Create Rect Room"), false, () => CreateNewNode(worldMousePos, NodeType.Rect)); menu.AddItem(new GUIContent("Create L-Shape Room"), false, () => CreateNewNode(worldMousePos, NodeType.LShape)); menu.AddItem(new GUIContent("Create Template Room"), false, () => CreateNewNode(worldMousePos, NodeType.Template)); menu.AddSeparator(""); menu.AddItem(new GUIContent("Center View (F)"), false, CenterView); menu.AddItem(new GUIContent("Focus Origin (O)"), false, FocusOnOrigin); } menu.ShowAsContext(); e.Use(); }
    private void CompleteConnection(Vector2 screenMousePos) { /* ... unchanged ... */ Vector2 worldMousePos = ScreenToWorldPosition(screenMousePos); DesignerNode targetNode = null; for (int i = nodes.Count - 1; i >= 0; i--) { if (nodes[i] != connectingFromNode && nodes[i].rect.Contains(worldMousePos)) { targetNode = nodes[i]; break; } } if (targetNode != null) { if (!ConnectionExists(connectingFromNode, targetNode)) { AddConnection(connectingFromNode, targetNode); } else { Debug.LogWarning("Connection already exists."); } } isCreatingConnection = false; connectingFromNode = null; GUI.changed = true; }
    #endregion

    #region Node & Connection Management
    private void CreateNewNode(Vector2 worldPos, NodeType type = NodeType.Rect) { /* ... Added hasUnsavedChanges ... */ string baseName = string.IsNullOrEmpty(newNodeName) ? type.ToString() : newNodeName; string id = $"{baseName.Replace(" ", "")}_{nextNodeNumber++}"; while (nodes.Any(n => n.id == id)) { id = $"{baseName.Replace(" ", "")}_{nextNodeNumber++}"; } DesignerNode newNode = new DesignerNode(id, type, worldPos, newNodeSize, globalNodeVisualSize, baseName); if (type == NodeType.Template) { newNode.templatePrefab = newTemplateReference; newNode.templateName = newTemplateReference?.name ?? ""; } nodes.Add(newNode); selectedNode = newNode; selectedConnection = null; hasUnsavedChanges = true; GUI.changed = true; }
    private void DeleteNode(DesignerNode nodeToDelete) { /* ... Added hasUnsavedChanges ... */ if (nodeToDelete == null) return; List<Connection> connectionsToRemove = connections.Where(c => c.fromNode == nodeToDelete || c.toNode == nodeToDelete).ToList(); foreach (var conn in connectionsToRemove) { connections.Remove(conn); if (conn.fromNode != nodeToDelete) conn.fromNode?.connections.Remove(nodeToDelete.id); if (conn.toNode != nodeToDelete) conn.toNode?.connections.Remove(nodeToDelete.id); } nodes.Remove(nodeToDelete); if (selectedNode == nodeToDelete) selectedNode = null; selectedConnection = null; hasUnsavedChanges = true; GUI.changed = true; }
    private bool ConnectionExists(DesignerNode from, DesignerNode to) { /* ... unchanged ... */ return connections.Any(c => (c.fromNode == from && c.toNode == to) || (c.fromNode == to && c.toNode == from)); }
    private void AddConnection(DesignerNode from, DesignerNode to) { /* ... Added hasUnsavedChanges ... */ if (from == null || to == null || from == to || ConnectionExists(from, to)) return; Connection newConnection = new Connection(from, to); connections.Add(newConnection); from.connections.Add(to.id); to.connections.Add(from.id); hasUnsavedChanges = true; GUI.changed = true; }
    private void RemoveConnection(DesignerNode from, DesignerNode to) { /* ... Added hasUnsavedChanges ... */ if (from == null || to == null) return; Connection connectionToRemove = connections.FirstOrDefault(c => (c.fromNode == from && c.toNode == to) || (c.fromNode == to && c.toNode == from)); if (connectionToRemove != null) { connections.Remove(connectionToRemove); from.connections.Remove(to.id); to.connections.Remove(from.id); if (selectedConnection == connectionToRemove) selectedConnection = null; hasUnsavedChanges = true; GUI.changed = true; } }
    #endregion

    #region View Controls
    private void CenterView() { /* ... unchanged ... */ if (nodes.Count == 0) { FocusOnOrigin(); return; } Rect bounds = nodes[0].rect; for (int i = 1; i < nodes.Count; i++) { bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, nodes[i].rect.xMin), Mathf.Min(bounds.yMin, nodes[i].rect.yMin), Mathf.Max(bounds.xMax, nodes[i].rect.xMax), Mathf.Max(bounds.yMax, nodes[i].rect.yMax)); } float padding = 50f; float requiredWidth = bounds.width + 2 * padding; float requiredHeight = bounds.height + 2 * padding; if (requiredWidth <= 0 || requiredHeight <= 0) { FocusOnOrigin(); return; } float zoomX = graphViewRect.width / requiredWidth; float zoomY = graphViewRect.height / requiredHeight; zoom = Mathf.Clamp(Mathf.Min(zoomX, zoomY), 0.2f, 1.0f); Vector2 worldCenter = bounds.center; Vector2 screenCenter = graphViewRect.size / 2f; panOffset = screenCenter - (worldCenter * zoom); GUI.changed = true; }
    private void FocusOnOrigin() { /* ... unchanged ... */ zoom = 1.0f; Vector2 screenCenter = graphViewRect.size / 2f; panOffset = screenCenter; GUI.changed = true; }
    private void AutoArrangeNodes() { /* ... Added hasUnsavedChanges ... */ if (nodes.Count == 0) return; Vector2 startPos = ScreenToWorldPosition(new Vector2(100, 100)); float currentX = startPos.x; float currentY = startPos.y; float maxYInRow = currentY; for (int i = 0; i < nodes.Count; i++) { nodes[i].SetPosition(new Vector2(currentX, currentY)); maxYInRow = Mathf.Max(maxYInRow, currentY + nodes[i].rect.height); if ((i + 1) % nodesPerRow == 0) { currentX = startPos.x; currentY = maxYInRow + nodeSpacing * 0.5f; maxYInRow = currentY; } else { currentX += nodeSpacing; } } hasUnsavedChanges = true; GUI.changed = true; }
    #endregion

    #region Coordinate Conversion
    private Vector2 ScreenToWorldPosition(Vector2 screenPos) { /* ... unchanged ... */ Vector2 adjustedScreenPos = screenPos - graphViewRect.position; return (adjustedScreenPos - panOffset) / zoom; }
    private Vector2 WorldToScreenPosition(Vector2 worldPos) { /* ... unchanged ... */ Vector2 adjustedScreenPos = (worldPos * zoom) + panOffset; return adjustedScreenPos + graphViewRect.position; }
    private Rect WorldToScreenRect(Rect worldRect) { /* ... unchanged ... */ Vector2 screenPos = WorldToScreenPosition(worldRect.position); return new Rect(screenPos.x, screenPos.y, worldRect.width * zoom, worldRect.height * zoom); }
    #endregion

    #region Save/Load/Export (RE-ADDED JSON Save/Load)

    // *** RE-ADDED Save/Load Functionality ***
    private bool AskSaveChanges()
    {
        if (hasUnsavedChanges)
        {
            int choice = EditorUtility.DisplayDialogComplex("Unsaved Changes", "You have unsaved changes in the Visual Level Designer.", "Save", "Don't Save", "Cancel");
            if (choice == 0) return SaveDesign();
            else if (choice == 1) return true;
            else return false;
        }
        return true;
    }

    private void NewDesign(bool prompt = true)
    {
        if (prompt && !AskSaveChanges()) return;
        nodes.Clear(); connections.Clear(); panOffset = Vector2.zero; zoom = 1.0f;
        selectedNode = null; selectedConnection = null; connectingFromNode = null;
        isCreatingConnection = false; nextNodeNumber = 1; currentFilePath = "";
        hasUnsavedChanges = false;
        this.titleContent = new GUIContent("Visual Level Designer"); // Reset title
        GUI.changed = true; FocusOnOrigin();
    }

    private bool SaveDesign()
    {
        if (string.IsNullOrEmpty(currentFilePath)) { return SaveDesignAs(); }
        else { return SaveToFile(currentFilePath); }
    }

    private bool SaveDesignAs()
    {
        string path = EditorUtility.SaveFilePanel("Save Level Design As...", Application.dataPath, "NewLevelDesign", "json");
        if (!string.IsNullOrEmpty(path))
        {
            if (path.StartsWith(Application.dataPath)) { path = "Assets" + path.Substring(Application.dataPath.Length); }
            currentFilePath = path;
            this.titleContent = new GUIContent("Visual Level Designer*"); // Indicate unsaved changes
            return SaveToFile(currentFilePath);
        }
        return false;
    }

    private bool SaveToFile(string path)
    { // *** Saves Visual Size ***
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
                    logicalHeight = node.size.y, // Save logical size
                    templateName = node.templateName,
                    visualWidth = node.visualSize.x, // Save visual size
                    visualHeight = node.visualSize.y
                });
            }
            foreach (var conn in connections)
            {
                if (conn == null || conn.fromNode == null || conn.toNode == null || conn.fromId == null || conn.toId == null) continue;
                data.connections.Add(new DesignerData.ConnectionInfo { fromId = conn.fromId, toId = conn.toId });
            }
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            hasUnsavedChanges = false;
            this.titleContent = new GUIContent("Visual Level Designer"); // Remove asterisk
            Debug.Log($"Level design saved to: {path}");
            AssetDatabase.Refresh();
            return true;
        }
        catch (Exception e) { Debug.LogError($"Error saving level design to {path}: {e.Message}"); return false; }
    }

    private void OpenDesign()
    { // *** Loads Visual Size ***
        if (!AskSaveChanges()) return;
        string path = EditorUtility.OpenFilePanel("Open Level Design", Application.dataPath, "json");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                DesignerData data = JsonUtility.FromJson<DesignerData>(json);
                if (data == null || data.nodes == null || data.connections == null) { throw new Exception("Invalid JSON data format."); }

                nodes.Clear(); connections.Clear(); nextNodeNumber = 1;
                Dictionary<string, DesignerNode> nodeMap = new Dictionary<string, DesignerNode>();

                foreach (var nodeInfo in data.nodes)
                {
                    if (string.IsNullOrEmpty(nodeInfo.id)) continue;
                    if (nodeInfo.id.Contains("_")) { int num; if (int.TryParse(nodeInfo.id.Split('_').LastOrDefault(), out num)) { nextNodeNumber = Mathf.Max(nextNodeNumber, num + 1); } }

                    // *** Use loaded visual size if available, otherwise global default ***
                    Vector2 loadedVisualSize = (nodeInfo.visualWidth > 0 && nodeInfo.visualHeight > 0)
                                             ? new Vector2(nodeInfo.visualWidth, nodeInfo.visualHeight)
                                             : globalNodeVisualSize; // Fallback to current global setting

                    DesignerNode newNode = new DesignerNode(nodeInfo.id, (NodeType)nodeInfo.nodeType, new Vector2(nodeInfo.x, nodeInfo.y), new Vector2Int(nodeInfo.logicalWidth, nodeInfo.logicalHeight), loadedVisualSize, nodeInfo.displayName);
                    newNode.templateName = nodeInfo.templateName;
                    if (newNode.nodeType == NodeType.Template && targetGenerator != null && !string.IsNullOrEmpty(newNode.templateName)) { newNode.templatePrefab = targetGenerator.roomTemplatePrefabs?.FirstOrDefault(p => p != null && p.name == newNode.templateName); }
                    nodes.Add(newNode);
                    nodeMap[newNode.id] = newNode;
                }
                foreach (var connInfo in data.connections)
                {
                    if (!string.IsNullOrEmpty(connInfo.fromId) && !string.IsNullOrEmpty(connInfo.toId) &&
                        nodeMap.TryGetValue(connInfo.fromId, out DesignerNode from) && nodeMap.TryGetValue(connInfo.toId, out DesignerNode to))
                    {
                        AddConnection(from, to);
                    }
                    else { Debug.LogWarning($"Skipping invalid connection on load: {connInfo.fromId} -> {connInfo.toId}"); }
                }
                if (path.StartsWith(Application.dataPath)) { path = "Assets" + path.Substring(Application.dataPath.Length); }
                currentFilePath = path; hasUnsavedChanges = false; selectedNode = null; selectedConnection = null;
                CenterView(); GUI.changed = true; this.titleContent = new GUIContent("Visual Level Designer");
                Debug.Log($"Level design loaded from: {path}");
            }
            catch (Exception e) { Debug.LogError($"Error opening level design from {path}: {e.Message}"); EditorUtility.DisplayDialog("Load Error", $"Failed to load design file.\nError: {e.Message}", "OK"); }
        }
    }

    private void CreateSceneObjectsAndGenerate()
    { // *** Added Confirmation Dialog ***
        if (targetGenerator == null) { EditorUtility.DisplayDialog("Error", "Please assign a Target Generator in the toolbar first.", "OK"); return; }
        if (nodes.Count == 0) { EditorUtility.DisplayDialog("Error", "Cannot generate level. Design is empty.", "OK"); return; }

        if (!EditorUtility.DisplayDialog("Create Scene Objects & Generate", "This will create/overwrite RoomNode GameObjects in the scene based on the current design, set the generator mode to UserDefinedLayout, and run generation. Continue?", "Yes", "No")) { return; }

        GameObject designRoot = GameObject.Find("LevelDesignRoot");
        if (designRoot == null) { designRoot = new GameObject("LevelDesignRoot"); Undo.RegisterCreatedObjectUndo(designRoot, "Create Level Design Root"); }
        else { int childCount = designRoot.transform.childCount; for (int i = childCount - 1; i >= 0; i--) { Undo.DestroyObjectImmediate(designRoot.transform.GetChild(i).gameObject); } }

        int designLayer = LayerMask.NameToLayer("LevelDesignNodes");
        if (designLayer == -1) { Debug.LogWarning("Layer 'LevelDesignNodes' not found. Please create it in Project Settings -> Tags and Layers. Scene nodes will be visible."); designLayer = 0; }
        designRoot.layer = designLayer;
        designRoot.hideFlags = HideFlags.HideInHierarchy; // Hide root from hierarchy

        Dictionary<string, RoomNode> createdNodeComponents = new Dictionary<string, RoomNode>();
        foreach (var node in nodes)
        {
            GameObject nodeGO = new GameObject(node.displayName); Undo.RegisterCreatedObjectUndo(nodeGO, "Create Room Node Object"); nodeGO.transform.SetParent(designRoot.transform);
            nodeGO.transform.position = new Vector3(node.position.x, node.position.y, 0);
            nodeGO.layer = designLayer; // Assign layer
                                        // nodeGO.hideFlags = HideFlags.HideInHierarchy; // Optionally hide individual nodes too
            RoomNode rnComponent = Undo.AddComponent<RoomNode>(nodeGO); rnComponent.roomId = node.id; rnComponent.roomType = node.nodeType; rnComponent.roomTemplatePrefab = node.templatePrefab; rnComponent.roomSize = node.size; rnComponent.connectedRooms = new List<RoomNode>(); createdNodeComponents[node.id] = rnComponent;
        }
        foreach (var connection in connections)
        {
            if (createdNodeComponents.TryGetValue(connection.fromId, out RoomNode fromComp) && createdNodeComponents.TryGetValue(connection.toId, out RoomNode toComp))
            {
                if (fromComp.connectedRooms == null) fromComp.connectedRooms = new List<RoomNode>();
                if (!fromComp.connectedRooms.Contains(toComp)) { fromComp.connectedRooms.Add(toComp); }
                if (toComp.connectedRooms == null) toComp.connectedRooms = new List<RoomNode>();
                if (!toComp.connectedRooms.Contains(fromComp)) { toComp.connectedRooms.Add(fromComp); }
            }
        }
        foreach (var comp in createdNodeComponents.Values) EditorUtility.SetDirty(comp);

        Debug.Log($"Created {nodes.Count} RoomNode GameObjects under '{designRoot.name}'. Assigned to layer {LayerMask.LayerToName(designLayer)}.");

        Undo.RecordObject(targetGenerator, "Set Generation Mode and Generate");
        targetGenerator.generationMode = GenerationMode.UserDefinedLayout;
        EditorUtility.SetDirty(targetGenerator);
        Debug.Log($"Set {targetGenerator.name} mode to UserDefinedLayout and generating...");
        targetGenerator.GenerateLevel(true); // Pass true to skip ClearLevel

        if (designRoot != null) EditorGUIUtility.PingObject(designRoot);

        EditorUtility.DisplayDialog("Generation Triggered", "Scene objects created/updated and level generation started. Check the scene view and console for results.", "OK");
    }

    // Removed ExportAsJson
    #endregion

    #region Helpers
    private string GetTypeIcon(NodeType type) { /* ... unchanged ... */ switch (type) { case NodeType.Rect: return "■"; case NodeType.LShape: return "L"; case NodeType.Template: return "T"; default: return "?"; } }
    private bool IsPositionOnConnection(Vector2 screenPos, Connection connection) { /* ... unchanged ... */ if (connection?.fromNode == null || connection?.toNode == null) return false; Vector2 start = WorldToScreenPosition(connection.fromNode.position); Vector2 end = WorldToScreenPosition(connection.toNode.position); float threshold = 8f * zoom; float segmentLengthSqr = (end - start).sqrMagnitude; if (segmentLengthSqr == 0.0f) return Vector2.Distance(screenPos, start) < threshold; float t = Vector2.Dot(screenPos - start, end - start) / segmentLengthSqr; t = Mathf.Clamp01(t); Vector2 projection = start + t * (end - start); return Vector2.Distance(screenPos, projection) < threshold; }
    #endregion
}