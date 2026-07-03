#if UNITY_EDITOR
#region Usings
using System.Collections.Generic;
using Pedro.TurnBasedDeckbuilder.TextAnimation;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
#endregion

namespace Pedro.TurnBasedDeckbuilder.Editor.TextAnimation
{
    public sealed class BreezeblocksTextEffectCodesWindow : OdinEditorWindow
    {
        private const string DefaultDatabasePath = "Assets/Breezeblocks/0. Game Design/Text Effects Database.asset";
        private const string ExampleContent = "Example Text";
        private const string ColorPreviewText = "Color";
        private const float PreviewMotionScale = 8f;
        private const int PreviewBaseFontSize = 14;

        private static readonly GUIContent[] ModifierHeaders =
        {
            new GUIContent("Modifier"),
            new GUIContent("ID"),
            new GUIContent("How It Works"),
            new GUIContent("Min"),
            new GUIContent("Max")
        };

        private struct ModifierIds
        {
            public string amplitude;
            public string speed;
            public string frequency;
        }

        [SerializeField] private TextAnimatorEffectDatabase _database;
        [SerializeField] private Color _globalColorTag = Color.red;
        [SerializeField] private bool _colorTabExpanded;
        private Vector2 _scroll;
        private readonly HashSet<int> _expandedEffects = new HashSet<int>();

        [MenuItem("Breezeblocks/Text Effect Codes", priority = 31)]
        /// <summary>
        /// Opens the text effect reference window from the Breezeblocks menu.
        /// </summary>
        private static void OpenWindow()
        {
            BreezeblocksTextEffectCodesWindow window = GetWindow<BreezeblocksTextEffectCodesWindow>();
            window.titleContent = new GUIContent("Text Effect Codes");
            window.minSize = new Vector2(980f, 620f);
            window.Show();
            window.Focus();
        }

        /// <summary>
        /// Auto-loads the effect database when the editor window opens.
        /// </summary>
        private void OnEnable()
        {
            TryAssignDatabase(force: false);
        }

        [Sirenix.OdinInspector.OnInspectorGUI, Sirenix.OdinInspector.PropertyOrder(-5000)]
        /// <summary>
        /// Draws the full Odin editor window UI.
        /// </summary>
        private void DrawWindow()
        {
            DrawHeaderBanner();
            EditorGUILayout.Space(10f);
            DrawToolbar();
            EditorGUILayout.Space(6f);

            if (_database == null)
                DrawMissingDatabaseState();
            else
                DrawGlobalModifierInfo();

            EditorGUILayout.Space(6f);
            DrawEffectsList();

            // Keep the preview animation alive in edit mode.
            Repaint();
        }

        /// <summary>
        /// Draws the title banner at the top of the window.
        /// </summary>
        private void DrawHeaderBanner()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 34f);
            EditorGUI.DrawRect(rect, new Color(0.11f, 0.23f, 0.31f, 1f));

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14
            };

            Color previous = GUI.color;
            GUI.color = new Color(0.92f, 0.97f, 1f, 1f);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 20f), "Text Effect Codes", titleStyle);
            GUI.color = previous;
        }

        /// <summary>
        /// Draws database selection and global expand/collapse controls.
        /// </summary>
        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Reference for tags, snippets and shared modifier IDs.", EditorStyles.miniLabel);
                EditorGUILayout.Space(2f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    TextAnimatorEffectDatabase next = (TextAnimatorEffectDatabase)EditorGUILayout.ObjectField(
                        "Text Effects Database",
                        _database,
                        typeof(TextAnimatorEffectDatabase),
                        false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _database = next;
                        _expandedEffects.Clear();
                    }

                    if (GUILayout.Button("Auto Load", GUILayout.Width(95f)))
                        TryAssignDatabase(force: true);

                    using (new EditorGUI.DisabledScope(_database == null))
                    {
                        if (GUILayout.Button("Ping", GUILayout.Width(70f)))
                            EditorGUIUtility.PingObject(_database);

                        if (GUILayout.Button("Expand All", GUILayout.Width(90f)))
                            SetExpandAll(true);

                        if (GUILayout.Button("Collapse All", GUILayout.Width(95f)))
                            SetExpandAll(false);
                    }
                }

                string path = _database != null ? AssetDatabase.GetAssetPath(_database) : DefaultDatabasePath;
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("Path", path);
            }
        }

        /// <summary>
        /// Draws recovery UI when no text animation database is assigned.
        /// </summary>
        private void DrawMissingDatabaseState()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "No TextAnimatorEffectDatabase found. Click Auto Load, or assign one manually.",
                    MessageType.Warning);

                if (GUILayout.Button("Auto Load Database", GUILayout.Height(30f)))
                    TryAssignDatabase(force: true);
            }
        }

        /// <summary>
        /// Draws shared modifier documentation for all text effects.
        /// </summary>
        private void DrawGlobalModifierInfo()
        {
            ModifierIds ids = ResolveModifierIds();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Rect titleRect = EditorGUILayout.GetControlRect(false, 24f);
                EditorGUI.DrawRect(titleRect, new Color(0.20f, 0.29f, 0.19f, 1f));

                GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft
                };

                Color previous = GUI.color;
                GUI.color = new Color(0.92f, 1f, 0.91f, 1f);
                GUI.Label(new Rect(titleRect.x + 8f, titleRect.y + 4f, titleRect.width - 16f, 18f), "Shared Modifier IDs", titleStyle);
                GUI.color = previous;

                EditorGUILayout.Space(3f);
                DrawModifiersHeader();
                DrawModifierRow("Amplitude", ids.amplitude, "Controls effect strength/intensity.", "0", "No Max");
                DrawModifierRow("Speed", ids.speed, "Controls animation speed over time.", "0", "No Max");
                DrawModifierRow("Frequency", ids.frequency, "Controls per-character phase spacing.", "0", "No Max");
                DrawModifierRow("Wave Size", "w", "Extra runtime frequency multiplier shared by effects.", "0", "No Max");
                DrawModifierRow("Delay", "d", "Delays effect start in seconds.", "0", "No Max");
                EditorGUILayout.HelpBox("All modifier values are parsed as floats. Negative values are clamped to 0 at runtime.", MessageType.None);
            }
        }

        /// <summary>
        /// Draws the scrollable list of color and text effect cards.
        /// </summary>
        private void DrawEffectsList()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
                EditorGUILayout.Space(3f);

                float scrollHeight = Mathf.Max(180f, position.height - 330f);
                using (EditorGUILayout.ScrollViewScope scrollScope = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Height(scrollHeight)))
                {
                    _scroll = scrollScope.scrollPosition;

                    DrawColorCard();
                    EditorGUILayout.Space(6f);

                    if (_database == null || _database.Effects == null || _database.Effects.Count <= 0)
                    {
                        EditorGUILayout.HelpBox("No database effects available.", MessageType.Info);
                        return;
                    }

                    for (int i = 0; i < _database.Effects.Count; i++)
                    {
                        DrawEffectCard(_database.Effects[i], i);
                        EditorGUILayout.Space(6f);
                    }
                }
            }
        }

        /// <summary>
        /// Draws the global TMP color tag helper card.
        /// </summary>
        private void DrawColorCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool nextExpanded = DrawHeaderToggle("Color (Global)", _colorTabExpanded, new Color(0.31f, 0.17f, 0.17f, 1f));
                if (nextExpanded != _colorTabExpanded)
                {
                    _colorTabExpanded = nextExpanded;
                    Repaint();
                }

                if (!_colorTabExpanded)
                    return;

                EditorGUILayout.Space(3f);
                EditorGUI.BeginChangeCheck();
                Color nextColor = EditorGUILayout.ColorField("Color Picker", _globalColorTag);
                if (EditorGUI.EndChangeCheck())
                {
                    _globalColorTag = nextColor;
                    Repaint();
                }

                string snippet = BuildColorSnippet(_globalColorTag);
                string example = BuildColorExample(_globalColorTag);

                EditorGUILayout.Space(4f);
                DrawColorPreview(_globalColorTag);
                EditorGUILayout.Space(4f);

                EditorGUILayout.LabelField("Code Snippet", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.TextField(snippet, GUILayout.ExpandWidth(true));

                    if (GUILayout.Button("Copy", GUILayout.Width(56f)))
                        EditorGUIUtility.systemCopyBuffer = snippet;
                }

                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Example Usage", EditorStyles.miniBoldLabel);
                DrawReadonlyMultiline(example, 44f);
            }
        }

        /// <summary>
        /// Draws one collapsible effect card with preview and snippets.
        /// </summary>
        private void DrawEffectCard(TextAnimatorEffectDefinition effect, int index)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (effect == null)
                {
                    EditorGUILayout.LabelField($"Effect #{index + 1}", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("This effect entry is null.", MessageType.Warning);
                    return;
                }

                string effectName = string.IsNullOrWhiteSpace(effect.DisplayName) ? $"Effect #{index + 1}" : effect.DisplayName;
                bool expanded = _expandedEffects.Contains(index);
                bool nextExpanded = DrawHeaderToggle($"{index + 1}. {effectName}", expanded, new Color(0.18f, 0.18f, 0.20f, 1f));
                if (nextExpanded != expanded)
                {
                    if (nextExpanded)
                        _expandedEffects.Add(index);
                    else
                        _expandedEffects.Remove(index);

                    Repaint();
                }

                if (!nextExpanded)
                    return;

                string tagId = effect.TagId;
                string snippet = BuildSnippet(tagId);
                string example = BuildExample(effect);

                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField($"Kind: {effect.Kind}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Tag ID: {tagId}", EditorStyles.miniLabel);
                EditorGUILayout.Space(4f);
                DrawLivePreview(effect, effectName);
                EditorGUILayout.Space(4f);

                EditorGUILayout.LabelField("Code Snippet", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.TextField(snippet, GUILayout.ExpandWidth(true));

                    if (GUILayout.Button("Copy", GUILayout.Width(56f)))
                        EditorGUIUtility.systemCopyBuffer = snippet;
                }

                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Example Usage", EditorStyles.miniBoldLabel);
                DrawReadonlyMultiline(example, 44f);
            }
        }

        /// <summary>
        /// Draws a lightweight IMGUI preview of one text effect.
        /// </summary>
        private static void DrawLivePreview(TextAnimatorEffectDefinition effect, string effectName)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.miniBoldLabel);

            Rect area = GUILayoutUtility.GetRect(16f, 56f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(0.10f, 0.11f, 0.13f, 1f));

            if (effect == null || !effect.Enabled)
            {
                GUIStyle disabledStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };

                GUI.Label(area, "Preview unavailable (effect disabled).", disabledStyle);
                return;
            }

            string previewText = string.IsNullOrWhiteSpace(effectName) ? "Effect Preview" : effectName;
            float elapsed = (float)EditorApplication.timeSinceStartup;
            TextAnimatorTagModifiers modifiers = TextAnimatorTagModifiers.Default;

            Color baseColor = EditorGUIUtility.isProSkin
                ? new Color(0.90f, 0.93f, 0.98f, 1f)
                : new Color(0.10f, 0.14f, 0.18f, 1f);

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = PreviewBaseFontSize,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,
                richText = false
            };

            // Precompute base width for centering.
            float fullWidth = 0f;
            for (int i = 0; i < previewText.Length; i++)
            {
                string ch = previewText[i].ToString();
                fullWidth += style.CalcSize(new GUIContent(ch)).x;
            }

            float x = area.x + Mathf.Max(8f, (area.width - fullWidth) * 0.5f);
            float baselineY = area.y + area.height * 0.5f - 10f;

            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;

            for (int i = 0; i < previewText.Length; i++)
            {
                char character = previewText[i];
                string ch = character.ToString();
                Vector2 charSize = style.CalcSize(new GUIContent(ch));

                TextAnimatorCharacterState state = TextAnimatorCharacterState.CreateDefault();
                effect.Apply(ref state, elapsed, i, i, previewText.Length, modifiers, (Color32)baseColor);

                float scale = Mathf.Clamp((state.ScaleMultiplier.x + state.ScaleMultiplier.y) * 0.5f, 0.5f, 2f);
                float w = charSize.x;
                float h = charSize.y;
                float drawX = x + state.PositionOffset.x * PreviewMotionScale;
                float drawY = baselineY - (h * 0.5f) + state.PositionOffset.y * PreviewMotionScale;

                Color c = baseColor;
                if (state.HasColorOverride)
                    c = state.ColorOverride;

                c.a *= Mathf.Clamp01(state.AlphaMultiplier);
                GUI.color = c;

                Rect charRect = new Rect(drawX, drawY, w, h);
                Vector2 pivot = new Vector2(charRect.x + charRect.width * 0.5f, charRect.y + charRect.height * 0.5f);

                GUI.matrix = previousMatrix;
                GUIUtility.RotateAroundPivot(state.RotationDegrees, pivot);
                GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), pivot);
                GUI.Label(charRect, ch, style);

                x += w;
            }

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        /// <summary>
        /// Draws a preview label using the selected color tag value.
        /// </summary>
        private static void DrawColorPreview(Color color)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.miniBoldLabel);
            Rect area = GUILayoutUtility.GetRect(16f, 56f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(0.10f, 0.11f, 0.13f, 1f));

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = PreviewBaseFontSize + 1,
                alignment = TextAnchor.MiddleCenter,
                richText = false
            };

            Color previous = GUI.color;
            Color c = color;
            c.a = Mathf.Clamp01(c.a);
            GUI.color = c;
            GUI.Label(area, ColorPreviewText, style);
            GUI.color = previous;
        }

        /// <summary>
        /// Draws a colored foldout header and returns the next expanded state.
        /// </summary>
        private static bool DrawHeaderToggle(string label, bool expanded, Color backgroundColor)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 24f);
            EditorGUI.DrawRect(headerRect, backgroundColor);

            string arrow = expanded ? "▼" : "▶";
            string buttonLabel = $"{arrow} {label}";

            GUIStyle buttonStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                richText = false
            };

            if (GUI.Button(headerRect, buttonLabel, buttonStyle))
                expanded = !expanded;

            return expanded;
        }

        /// <summary>
        /// Builds a minimal open and close tag snippet for an effect.
        /// </summary>
        private static string BuildSnippet(string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId))
                return "<invalid-tag></invalid-tag>";

            return $"<{tagId}></{tagId}>";
        }

        /// <summary>
        /// Builds a minimal TMP color tag snippet.
        /// </summary>
        private static string BuildColorSnippet(Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            return $"<color=#{hex}></color>";
        }

        /// <summary>
        /// Builds a complete TMP color tag example.
        /// </summary>
        private static string BuildColorExample(Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            return $"<color=#{hex}>{ColorPreviewText}</color>";
        }

        /// <summary>
        /// Builds a complete usage example with shared text effect modifiers.
        /// </summary>
        private static string BuildExample(TextAnimatorEffectDefinition effect)
        {
            if (effect == null || string.IsNullOrWhiteSpace(effect.TagId))
                return "<invalid-tag>Example Text</invalid-tag>";

            string a = string.IsNullOrWhiteSpace(effect.AmplitudeModifierId) ? "a" : effect.AmplitudeModifierId;
            string s = string.IsNullOrWhiteSpace(effect.SpeedModifierId) ? "s" : effect.SpeedModifierId;
            string f = string.IsNullOrWhiteSpace(effect.FrequencyModifierId) ? "f" : effect.FrequencyModifierId;

            return $"<{effect.TagId} {a}=1.2 {s}=1.0 {f}=0.35 w=1.0 d=0.0>{ExampleContent}</{effect.TagId}>";
        }

        /// <summary>
        /// Draws disabled multiline text for copy-friendly examples.
        /// </summary>
        private static void DrawReadonlyMultiline(string value, float minHeight)
        {
            GUIStyle style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false
            };

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextArea(value ?? string.Empty, style, GUILayout.MinHeight(minHeight));
            }
        }

        /// <summary>
        /// Draws the header row for the modifier documentation table.
        /// </summary>
        private static void DrawModifiersHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(ModifierHeaders[0], EditorStyles.miniBoldLabel, GUILayout.Width(120f));
                EditorGUILayout.LabelField(ModifierHeaders[1], EditorStyles.miniBoldLabel, GUILayout.Width(80f));
                EditorGUILayout.LabelField(ModifierHeaders[2], EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField(ModifierHeaders[3], EditorStyles.miniBoldLabel, GUILayout.Width(70f));
                EditorGUILayout.LabelField(ModifierHeaders[4], EditorStyles.miniBoldLabel, GUILayout.Width(80f));
            }
        }

        /// <summary>
        /// Draws one modifier documentation row.
        /// </summary>
        private static void DrawModifierRow(string modifierName, string modifierId, string behavior, string minValue, string maxValue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(modifierName, EditorStyles.miniLabel, GUILayout.Width(120f));
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrWhiteSpace(modifierId) ? "-" : modifierId,
                    EditorStyles.textField,
                    GUILayout.Width(80f),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.LabelField(behavior, EditorStyles.wordWrappedMiniLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField(minValue, EditorStyles.miniLabel, GUILayout.Width(70f));
                EditorGUILayout.LabelField(maxValue, EditorStyles.miniLabel, GUILayout.Width(80f));
            }
        }

        /// <summary>
        /// Resolves modifier ids from the first available database effect.
        /// </summary>
        private ModifierIds ResolveModifierIds()
        {
            ModifierIds ids = new ModifierIds
            {
                amplitude = "a",
                speed = "s",
                frequency = "f"
            };

            if (_database == null || _database.Effects == null)
                return ids;

            for (int i = 0; i < _database.Effects.Count; i++)
            {
                TextAnimatorEffectDefinition effect = _database.Effects[i];
                if (effect == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(effect.AmplitudeModifierId))
                    ids.amplitude = effect.AmplitudeModifierId;

                if (!string.IsNullOrWhiteSpace(effect.SpeedModifierId))
                    ids.speed = effect.SpeedModifierId;

                if (!string.IsNullOrWhiteSpace(effect.FrequencyModifierId))
                    ids.frequency = effect.FrequencyModifierId;

                return ids;
            }

            return ids;
        }

        /// <summary>
        /// Expands or collapses all effect cards.
        /// </summary>
        private void SetExpandAll(bool expand)
        {
            _expandedEffects.Clear();
            _colorTabExpanded = expand;
            if (!expand || _database == null || _database.Effects == null)
            {
                Repaint();
                return;
            }

            for (int i = 0; i < _database.Effects.Count; i++)
                _expandedEffects.Add(i);

            Repaint();
        }

        /// <summary>
        /// Finds and assigns the text animation database asset.
        /// </summary>
        private void TryAssignDatabase(bool force)
        {
            if (force)
                _database = null;

            if (_database != null)
                return;

            _database = AssetDatabase.LoadAssetAtPath<TextAnimatorEffectDatabase>(DefaultDatabasePath);
            if (_database != null)
                return;

            string[] localGuids = AssetDatabase.FindAssets(
                "t:TextAnimatorEffectDatabase",
                new[] { "Assets/Breezeblocks/0. Game Design" });

            if (localGuids != null && localGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(localGuids[0]);
                _database = AssetDatabase.LoadAssetAtPath<TextAnimatorEffectDatabase>(path);
                if (_database != null)
                    return;
            }

            string[] allGuids = AssetDatabase.FindAssets("t:TextAnimatorEffectDatabase");
            if (allGuids == null || allGuids.Length <= 0)
                return;

            string fallbackPath = AssetDatabase.GUIDToAssetPath(allGuids[0]);
            _database = AssetDatabase.LoadAssetAtPath<TextAnimatorEffectDatabase>(fallbackPath);
        }
    }
}
#endif
