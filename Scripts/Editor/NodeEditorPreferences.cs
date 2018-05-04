﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor
{
    public static class NodeEditorPreferences
    {
        public enum NoodleType { Curve, Line, Angled }

        /// <summary> The last editor we checked. This should be the one we modify </summary>
        private static XNodeEditor.NodeGraphEditor _lastEditor;
        /// <summary> The last key we checked. This should be the one we modify </summary>
        private static string _lastKey = "xNode.Settings";

        private static Dictionary<string, Color> _typeColors = new Dictionary<string, Color>();
        private static Dictionary<string, Settings> _settings = new Dictionary<string, Settings>();

        [System.Serializable]
        public class Settings : ISerializationCallbackReceiver
        {
            [SerializeField] private Color32 _gridLineColor = new Color(0.45f, 0.45f, 0.45f);
            public Color32 GridLineColor { get { return _gridLineColor; } set { _gridLineColor = value; _gridTexture = null; _crossTexture = null; } }

            [SerializeField] private Color32 _gridBgColor = new Color(0.18f, 0.18f, 0.18f);
            public Color32 GridBgColor { get { return _gridBgColor; } set { _gridBgColor = value; _gridTexture = null; } }

            public Color32 HighlightColor = new Color32(255, 255, 255, 255);
            public bool GridSnap = true;
            public bool AutoSave = true;
            [SerializeField] private string _typeColorsData = "";
            [NonSerialized] public Dictionary<string, Color> TypeColors = new Dictionary<string, Color>();
            public NoodleType NoodleType = NoodleType.Curve;

            private Texture2D _gridTexture;
            public Texture2D GridTexture
            {
                get
                {
                    if (_gridTexture == null) _gridTexture = NodeEditorResources.GenerateGridTexture(GridLineColor, GridBgColor);
                    return _gridTexture;
                }
            }
            private Texture2D _crossTexture;
            public Texture2D CrossTexture
            {
                get
                {
                    if (_crossTexture == null) _crossTexture = NodeEditorResources.GenerateCrossTexture(GridLineColor);
                    return _crossTexture;
                }
            }

            public void OnAfterDeserialize()
            {
                // Deserialize typeColorsData
                TypeColors = new Dictionary<string, Color>();
                string[] data = _typeColorsData.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < data.Length; i += 2)
                {
                    Color col;
                    if (ColorUtility.TryParseHtmlString("#" + data[i + 1], out col))
                    {
                        TypeColors.Add(data[i], col);
                    }
                }
            }

            public void OnBeforeSerialize()
            {
                // Serialize typeColors
                _typeColorsData = "";
                foreach (var item in TypeColors)
                {
                    _typeColorsData += item.Key + "," + ColorUtility.ToHtmlStringRGB(item.Value) + ",";
                }
            }
        }

        /// <summary> Get settings of current active editor </summary>
        public static Settings GetSettings()
        {
            if (_lastEditor != XNodeEditor.NodeEditorWindow.Current.GraphEditor)
            {
                object[] attribs = NodeEditorWindow.Current.GraphEditor.GetType().GetCustomAttributes(typeof(NodeGraphEditor.CustomNodeGraphEditorAttribute), true);
                if (attribs.Length == 1)
                {
                    XNodeEditor.NodeGraphEditor.CustomNodeGraphEditorAttribute attrib = attribs[0] as XNodeEditor.NodeGraphEditor.CustomNodeGraphEditorAttribute;
                    _lastEditor = XNodeEditor.NodeEditorWindow.Current.GraphEditor;
                    _lastKey = attrib.EditorPrefsKey;
                }
                else return null;
            }
            if (!_settings.ContainsKey(_lastKey)) VerifyLoaded();
            return _settings[_lastKey];
        }

        [PreferenceItem("Node Editor")]
        private static void PreferencesGUI()
        {
            VerifyLoaded();
            Settings settings = NodeEditorPreferences._settings[_lastKey];

            NodeSettingsGUI(_lastKey, settings);
            GridSettingsGUI(_lastKey, settings);
            SystemSettingsGUI(_lastKey, settings);
            TypeColorsGUI(_lastKey, settings);
            if (GUILayout.Button(new GUIContent("Set Default", "Reset all values to default"), GUILayout.Width(120)))
            {
                ResetPrefs();
            }
        }

        private static void GridSettingsGUI(string key, Settings settings)
        {
            //Label
            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
            settings.GridSnap = EditorGUILayout.Toggle(new GUIContent("Snap", "Hold CTRL in editor to invert"), settings.GridSnap);

            settings.GridLineColor = EditorGUILayout.ColorField("Color", settings.GridLineColor);
            settings.GridBgColor = EditorGUILayout.ColorField(" ", settings.GridBgColor);
            if (GUI.changed)
            {
                SavePrefs(key, settings);

                NodeEditorWindow.RepaintAll();
            }
            EditorGUILayout.Space();
        }

        private static void SystemSettingsGUI(string key, Settings settings)
        {
            //Label
            EditorGUILayout.LabelField("System", EditorStyles.boldLabel);
            settings.AutoSave = EditorGUILayout.Toggle(new GUIContent("Autosave", "Disable for better editor performance"), settings.AutoSave);
            if (GUI.changed) SavePrefs(key, settings);
            EditorGUILayout.Space();
        }

        private static void NodeSettingsGUI(string key, Settings settings)
        {
            //Label
            EditorGUILayout.LabelField("Node", EditorStyles.boldLabel);
            settings.HighlightColor = EditorGUILayout.ColorField("Selection", settings.HighlightColor);
            settings.NoodleType = (NoodleType)EditorGUILayout.EnumPopup("Noodle type", (Enum)settings.NoodleType);
            if (GUI.changed)
            {
                SavePrefs(key, settings);
                NodeEditorWindow.RepaintAll();
            }
            EditorGUILayout.Space();
        }

        private static void TypeColorsGUI(string key, Settings settings)
        {
            //Label
            EditorGUILayout.LabelField("Types", EditorStyles.boldLabel);

            //Display type colors. Save them if they are edited by the user
            List<string> typeColorKeys = new List<string>(_typeColors.Keys);
            foreach (string typeColorKey in typeColorKeys)
            {
                Color col = _typeColors[typeColorKey];
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                col = EditorGUILayout.ColorField(typeColorKey, col);
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    _typeColors[typeColorKey] = col;
                    if (settings.TypeColors.ContainsKey(typeColorKey)) settings.TypeColors[typeColorKey] = col;
                    else settings.TypeColors.Add(typeColorKey, col);
                    SavePrefs(typeColorKey, settings);
                    NodeEditorWindow.RepaintAll();
                }
            }
        }

        /// <summary> Load prefs if they exist. Create if they don't </summary>
        private static Settings LoadPrefs()
        {
            // Create settings if it doesn't exist
            if (!EditorPrefs.HasKey(_lastKey))
            {
                if (_lastEditor != null) EditorPrefs.SetString(_lastKey, JsonUtility.ToJson(_lastEditor.GetDefaultPreferences()));
                else EditorPrefs.SetString(_lastKey, JsonUtility.ToJson(new Settings()));
            }
            return JsonUtility.FromJson<Settings>(EditorPrefs.GetString(_lastKey));
        }

        /// <summary> Delete all prefs </summary>
        public static void ResetPrefs()
        {
            if (EditorPrefs.HasKey(_lastKey)) EditorPrefs.DeleteKey(_lastKey);
            if (_settings.ContainsKey(_lastKey)) _settings.Remove(_lastKey);
            _typeColors = new Dictionary<string, Color>();
            VerifyLoaded();
            NodeEditorWindow.RepaintAll();
        }

        /// <summary> Save preferences in EditorPrefs </summary>
        private static void SavePrefs(string key, Settings settings)
        {
            EditorPrefs.SetString(key, JsonUtility.ToJson(settings));
        }

        /// <summary> Check if we have loaded settings for given key. If not, load them </summary>
        private static void VerifyLoaded()
        {
            if (!_settings.ContainsKey(_lastKey)) _settings.Add(_lastKey, LoadPrefs());
        }

        /// <summary> Return color based on type </summary>
        public static Color GetTypeColor(System.Type type)
        {
            VerifyLoaded();
            if (type == null) return Color.gray;
            string typeName = type.PrettyName();
            if (!_typeColors.ContainsKey(typeName))
            {
                if (_settings[_lastKey].TypeColors.ContainsKey(typeName)) _typeColors.Add(typeName, _settings[_lastKey].TypeColors[typeName]);
                else
                {
#if UNITY_5_4_OR_NEWER
                    UnityEngine.Random.InitState(typeName.GetHashCode());
#else
                    UnityEngine.Random.seed = typeName.GetHashCode();
#endif
                    _typeColors.Add(typeName, new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value));
                }
            }
            return _typeColors[typeName];
        }
    }
}