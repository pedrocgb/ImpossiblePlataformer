#region Usings
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
#endregion

namespace Pedro.TurnBasedDeckbuilder.TextAnimation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPTextAnimator : MonoBehaviour
    {
        private struct ParsedEffectSpan
        {
            public TextAnimatorEffectDefinition effect;
            public TextAnimatorTagModifiers modifiers;
            public int sourceStartIndex;
            public int sourceEndIndex;
            public int visibleCharacterCount;
        }

        private struct OpenEffectTag
        {
            public string tagId;
            public TextAnimatorEffectDefinition effect;
            public TextAnimatorTagModifiers modifiers;
            public int sourceStartIndex;
        }

        [FoldoutGroup("References"), SerializeField] private TMP_Text _target;
        [FoldoutGroup("References"), SerializeField] private TextAnimatorEffectDatabase _database;

        [FoldoutGroup("Playback"), SerializeField] private bool _parseTextOnEnable = true;
        [FoldoutGroup("Playback"), SerializeField] private bool _autoParseWhenTextChanges = true;
        [FoldoutGroup("Playback"), SerializeField] private bool _useUnscaledTime = true;
        [FoldoutGroup("Playback"), SerializeField, MinValue(0f)] private float _timeScale = 1f;
        [FoldoutGroup("Playback"), SerializeField] private bool _restartTimeWhenParsing = true;

        [FoldoutGroup("Preview"), SerializeField, TextArea(2, 6)]
        private string _previewSourceText = "<wave a=1.1 s=1.0>Text Animator</wave>\n<rainb s=0.25>Rainbow</rainb> <shake a=0.6>shake</shake>";

        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")] private string _lastSourceText = string.Empty;
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")] private string _lastRenderedText = string.Empty;
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")] private int _activeEffectSpans;
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")] private float _elapsed;

        private readonly List<ParsedEffectSpan> _parsedSpans = new List<ParsedEffectSpan>(32);
        private readonly List<OpenEffectTag> _openTags = new List<OpenEffectTag>(16);
        private readonly List<int> _spanVisibleProgress = new List<int>(32);
        private readonly StringBuilder _builder = new StringBuilder(512);

        private static readonly char[] ModifierTokenSeparators = { ' ' };
        private static readonly NumberStyles ModifierNumberStyles = NumberStyles.Float;

        private TMP_MeshInfo[] _baseMeshInfo;
        private bool _hadAnimatedLastFrame;

        /// <summary>
        /// Assigns the same-object TMP text reference when the component is added or reset.
        /// </summary>
        private void Reset()
        {
            _target = GetComponent<TMP_Text>();
        }

        /// <summary>
        /// Resolves local component references before text parsing starts.
        /// </summary>
        private void Awake()
        {
            ResolveReferences();
        }

        /// <summary>
        /// Parses the current text or snapshots the base mesh when the animator becomes active.
        /// </summary>
        private void OnEnable()
        {
            ResolveReferences();

            if (_parseTextOnEnable && _target != null)
                ParseAndApplyText(_target.text);
            else
                RefreshBaseMeshSnapshot();
        }

        /// <summary>
        /// Restores base text geometry when the animator is disabled.
        /// </summary>
        private void OnDisable()
        {
            RestoreBaseGeometryIfNeeded();
            _hadAnimatedLastFrame = false;
        }

        /// <summary>
        /// Re-parses changed text and applies active text effects after TMP has updated geometry.
        /// </summary>
        private void LateUpdate()
        {
            if (_target == null)
                return;

            if (_autoParseWhenTextChanges && !string.Equals(_target.text, _lastRenderedText, StringComparison.Ordinal))
                ParseAndApplyText(_target.text);

            if (_target.havePropertiesChanged)
                RefreshBaseMeshSnapshot();

            if (_baseMeshInfo == null || _baseMeshInfo.Length == 0)
                return;

            if (_parsedSpans.Count <= 0)
            {
                RestoreBaseGeometryIfNeeded();
                _hadAnimatedLastFrame = false;
                return;
            }

            float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt < 0f)
                dt = 0f;

            _elapsed += dt * Mathf.Max(0f, _timeScale);
            AnimateText();
            _hadAnimatedLastFrame = true;
        }

        [FoldoutGroup("Actions"), Button(ButtonSizes.Medium)]
        /// <summary>
        /// Rebuilds effect spans from the current TMP text.
        /// </summary>
        public void ReparseCurrentTargetText()
        {
            if (_target == null)
                return;

            ParseAndApplyText(_target.text);
        }

        [FoldoutGroup("Actions"), Button(ButtonSizes.Medium)]
        /// <summary>
        /// Applies the configured inspector preview text to the target TMP component.
        /// </summary>
        public void ApplyPreviewText()
        {
            ParseAndApplyText(_previewSourceText ?? string.Empty);
        }

        [FoldoutGroup("Actions"), Button(ButtonSizes.Medium)]
        /// <summary>
        /// Removes active parsed spans and restores the unmodified TMP mesh.
        /// </summary>
        public void ClearEffectsAndRestoreMesh()
        {
            _parsedSpans.Clear();
            _activeEffectSpans = 0;
            RestoreBaseGeometryIfNeeded(forceUpload: true);
        }

        /// <summary>
        /// Assigns source text with effect tags and immediately parses it.
        /// </summary>
        public void SetTextWithEffects(string sourceText)
        {
            ParseAndApplyText(sourceText ?? string.Empty);
        }

        /// <summary>
        /// Replaces the effect database and optionally reparses the current text.
        /// </summary>
        public void SetDatabase(TextAnimatorEffectDatabase database, bool reparse = true)
        {
            _database = database;
            if (reparse && _target != null)
                ParseAndApplyText(string.IsNullOrEmpty(_lastSourceText) ? _target.text : _lastSourceText);
        }

        /// <summary>
        /// Caches the same-object TMP text reference when it is missing.
        /// </summary>
        private void ResolveReferences()
        {
            if (_target == null)
                _target = GetComponent<TMP_Text>();
        }

        /// <summary>
        /// Parses source effect tags, updates visible TMP text, and snapshots base geometry.
        /// </summary>
        private void ParseAndApplyText(string sourceText)
        {
            if (_target == null)
                return;

            ParseEffectTags(sourceText ?? string.Empty, out string renderedText);

            _lastSourceText = sourceText ?? string.Empty;
            _lastRenderedText = renderedText;
            _activeEffectSpans = _parsedSpans.Count;

            if (!string.Equals(_target.text, renderedText, StringComparison.Ordinal))
                _target.text = renderedText;

            RefreshBaseMeshSnapshot();

            if (_restartTimeWhenParsing)
                _elapsed = 0f;
        }

        /// <summary>
        /// Strips recognized effect tags while recording source ranges for animation.
        /// </summary>
        private void ParseEffectTags(string sourceText, out string renderedText)
        {
            _parsedSpans.Clear();
            _openTags.Clear();
            _builder.Length = 0;

            if (string.IsNullOrEmpty(sourceText))
            {
                renderedText = string.Empty;
                return;
            }

            for (int i = 0; i < sourceText.Length; i++)
            {
                char c = sourceText[i];
                if (c != '<')
                {
                    _builder.Append(c);
                    continue;
                }

                int closeIndex = sourceText.IndexOf('>', i + 1);
                if (closeIndex < 0)
                {
                    _builder.Append(sourceText, i, sourceText.Length - i);
                    break;
                }

                string token = sourceText.Substring(i + 1, closeIndex - i - 1);
                if (!TryParseTagToken(token, out bool isClosing, out bool isSelfClosing, out string tagId, out string arguments))
                {
                    _builder.Append(sourceText, i, closeIndex - i + 1);
                    i = closeIndex;
                    continue;
                }

                if (_database == null || !_database.TryGetEffectByTag(tagId, out TextAnimatorEffectDefinition effect) || effect == null)
                {
                    _builder.Append(sourceText, i, closeIndex - i + 1);
                    i = closeIndex;
                    continue;
                }

                if (isClosing)
                {
                    bool closed = CloseLastOpenTag(tagId, _builder.Length);
                    if (!closed)
                        _builder.Append(sourceText, i, closeIndex - i + 1);

                    i = closeIndex;
                    continue;
                }

                TextAnimatorTagModifiers modifiers = ParseModifiers(arguments, effect);
                if (!isSelfClosing)
                {
                    _openTags.Add(new OpenEffectTag
                    {
                        tagId = tagId,
                        effect = effect,
                        modifiers = modifiers,
                        sourceStartIndex = _builder.Length
                    });
                }

                i = closeIndex;
            }

            int endIndex = _builder.Length;
            for (int i = _openTags.Count - 1; i >= 0; i--)
            {
                OpenEffectTag open = _openTags[i];
                if (open.effect == null)
                    continue;

                _parsedSpans.Add(new ParsedEffectSpan
                {
                    effect = open.effect,
                    modifiers = open.modifiers,
                    sourceStartIndex = open.sourceStartIndex,
                    sourceEndIndex = endIndex
                });
            }

            _openTags.Clear();
            renderedText = _builder.ToString();
        }

        /// <summary>
        /// Closes the latest matching open tag and records its rendered text span.
        /// </summary>
        private bool CloseLastOpenTag(string tagId, int sourceEndIndex)
        {
            for (int i = _openTags.Count - 1; i >= 0; i--)
            {
                OpenEffectTag open = _openTags[i];
                if (!string.Equals(open.tagId, tagId, StringComparison.OrdinalIgnoreCase))
                    continue;

                _openTags.RemoveAt(i);
                if (open.effect == null)
                    return true;

                _parsedSpans.Add(new ParsedEffectSpan
                {
                    effect = open.effect,
                    modifiers = open.modifiers,
                    sourceStartIndex = open.sourceStartIndex,
                    sourceEndIndex = sourceEndIndex
                });
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses one tag token into tag id, arguments, and closing state.
        /// </summary>
        private static bool TryParseTagToken(
            string token,
            out bool isClosing,
            out bool isSelfClosing,
            out string tagId,
            out string arguments)
        {
            isClosing = false;
            isSelfClosing = false;
            tagId = string.Empty;
            arguments = string.Empty;

            if (string.IsNullOrWhiteSpace(token))
                return false;

            string trimmed = token.Trim();
            if (trimmed.Length <= 0)
                return false;

            if (trimmed[0] == '/')
            {
                isClosing = true;
                trimmed = trimmed.Substring(1).Trim();
            }
            else if (trimmed.EndsWith("/", StringComparison.Ordinal))
            {
                isSelfClosing = true;
                trimmed = trimmed.Substring(0, trimmed.Length - 1).Trim();
            }

            if (trimmed.Length <= 0)
                return false;

            int splitIndex = -1;
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (char.IsWhiteSpace(trimmed[i]))
                {
                    splitIndex = i;
                    break;
                }
            }

            if (splitIndex < 0)
            {
                if (trimmed.IndexOf('=') >= 0)
                    return false;

                tagId = trimmed.Trim().ToLowerInvariant();
                return !string.IsNullOrWhiteSpace(tagId);
            }

            tagId = trimmed.Substring(0, splitIndex).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(tagId))
                return false;

            arguments = trimmed.Substring(splitIndex + 1).Trim();
            return true;
        }

        /// <summary>
        /// Parses runtime numeric modifiers for one recognized text effect tag.
        /// </summary>
        private static TextAnimatorTagModifiers ParseModifiers(string arguments, TextAnimatorEffectDefinition effect)
        {
            TextAnimatorTagModifiers modifiers = TextAnimatorTagModifiers.Default;
            if (effect == null || string.IsNullOrWhiteSpace(arguments))
                return modifiers;

            string amplitudeKey = effect.AmplitudeModifierId;
            string speedKey = effect.SpeedModifierId;
            string frequencyKey = effect.FrequencyModifierId;

            string[] tokens = arguments.Split(ModifierTokenSeparators, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                int equalIndex = token.IndexOf('=');
                if (equalIndex <= 0 || equalIndex >= token.Length - 1)
                    continue;

                string key = token.Substring(0, equalIndex).Trim().ToLowerInvariant();
                string valueText = token.Substring(equalIndex + 1).Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(valueText))
                    continue;

                if (!float.TryParse(valueText, ModifierNumberStyles, CultureInfo.InvariantCulture, out float parsedValue))
                    continue;

                if (key == amplitudeKey)
                    modifiers.amplitude = Mathf.Max(0f, parsedValue);
                else if (key == speedKey)
                    modifiers.speed = Mathf.Max(0f, parsedValue);
                else if (key == frequencyKey)
                    modifiers.frequency = Mathf.Max(0f, parsedValue);
                else if (key == "w")
                    modifiers.waveSize = Mathf.Max(0f, parsedValue);
                else if (key == "d")
                    modifiers.delay = Mathf.Max(0f, parsedValue);
            }

            return modifiers;
        }

        /// <summary>
        /// Forces TMP mesh generation and stores a clean copy for later restoration.
        /// </summary>
        private void RefreshBaseMeshSnapshot()
        {
            if (_target == null)
                return;

            _target.ForceMeshUpdate();
            _baseMeshInfo = _target.textInfo.CopyMeshInfoVertexData();
        }

        /// <summary>
        /// Restores the stored base mesh when animation has changed vertices or colors.
        /// </summary>
        private void RestoreBaseGeometryIfNeeded(bool forceUpload = false)
        {
            if (_target == null || _baseMeshInfo == null || _baseMeshInfo.Length == 0)
                return;

            if (!forceUpload && !_hadAnimatedLastFrame)
                return;

            TMP_TextInfo textInfo = _target.textInfo;
            if (textInfo == null || textInfo.meshInfo == null)
                return;

            int meshCount = Mathf.Min(textInfo.meshInfo.Length, _baseMeshInfo.Length);
            for (int i = 0; i < meshCount; i++)
            {
                TMP_MeshInfo dst = textInfo.meshInfo[i];
                TMP_MeshInfo src = _baseMeshInfo[i];

                if (dst.vertices == null || src.vertices == null)
                    continue;

                int vertexCount = Mathf.Min(dst.vertices.Length, src.vertices.Length);
                if (vertexCount > 0)
                    Array.Copy(src.vertices, dst.vertices, vertexCount);

                if (dst.colors32 != null && src.colors32 != null)
                {
                    int colorCount = Mathf.Min(dst.colors32.Length, src.colors32.Length);
                    if (colorCount > 0)
                        Array.Copy(src.colors32, dst.colors32, colorCount);
                }
            }

            _target.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary>
        /// Applies all active parsed text effects to TMP vertices and colors.
        /// </summary>
        private void AnimateText()
        {
            if (_target == null || _baseMeshInfo == null || _baseMeshInfo.Length == 0)
                return;

            TMP_TextInfo textInfo = _target.textInfo;
            if (textInfo == null || textInfo.characterCount <= 0)
                return;

            RefreshSpanVisibleCharacterCounts(textInfo);

            int meshCount = Mathf.Min(textInfo.meshInfo.Length, _baseMeshInfo.Length);
            for (int i = 0; i < meshCount; i++)
            {
                TMP_MeshInfo dst = textInfo.meshInfo[i];
                TMP_MeshInfo src = _baseMeshInfo[i];

                if (dst.vertices == null || src.vertices == null)
                    continue;

                int vertexCount = Mathf.Min(dst.vertices.Length, src.vertices.Length);
                if (vertexCount > 0)
                    Array.Copy(src.vertices, dst.vertices, vertexCount);

                if (dst.colors32 != null && src.colors32 != null)
                {
                    int colorCount = Mathf.Min(dst.colors32.Length, src.colors32.Length);
                    if (colorCount > 0)
                        Array.Copy(src.colors32, dst.colors32, colorCount);
                }
            }

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                    continue;

                int sourceIndex = charInfo.index;
                if (sourceIndex < 0)
                    continue;

                int meshIndex = charInfo.materialReferenceIndex;
                if (meshIndex < 0 || meshIndex >= meshCount)
                    continue;

                int vertexIndex = charInfo.vertexIndex;
                Vector3[] vertices = textInfo.meshInfo[meshIndex].vertices;
                Color32[] colors = textInfo.meshInfo[meshIndex].colors32;
                Color32[] sourceColors = _baseMeshInfo[meshIndex].colors32;

                if (vertices == null || vertexIndex < 0 || vertexIndex + 3 >= vertices.Length)
                    continue;

                Color32 sourceColor = new Color32(255, 255, 255, 255);
                if (sourceColors != null && vertexIndex >= 0 && vertexIndex < sourceColors.Length)
                    sourceColor = sourceColors[vertexIndex];

                TextAnimatorCharacterState state = TextAnimatorCharacterState.CreateDefault();
                bool hasAnyEffect = false;

                for (int spanIndex = 0; spanIndex < _parsedSpans.Count; spanIndex++)
                {
                    ParsedEffectSpan span = _parsedSpans[spanIndex];
                    if (span.effect == null)
                        continue;

                    if (sourceIndex < span.sourceStartIndex || sourceIndex >= span.sourceEndIndex)
                        continue;

                    int spanCharacterIndex = _spanVisibleProgress[spanIndex];
                    _spanVisibleProgress[spanIndex] = spanCharacterIndex + 1;
                    span.effect.Apply(
                        ref state,
                        _elapsed,
                        i,
                        spanCharacterIndex,
                        span.visibleCharacterCount,
                        span.modifiers,
                        sourceColor);
                    hasAnyEffect = true;
                }

                if (!hasAnyEffect)
                    continue;

                Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
                Quaternion rotation = Quaternion.Euler(0f, 0f, state.RotationDegrees);

                for (int c = 0; c < 4; c++)
                {
                    int vIndex = vertexIndex + c;
                    Vector3 local = vertices[vIndex] - center;
                    local = Vector3.Scale(local, state.ScaleMultiplier);
                    local = rotation * local;
                    vertices[vIndex] = local + center + state.PositionOffset;

                    if (colors == null || sourceColors == null || vIndex < 0 || vIndex >= colors.Length || vIndex >= sourceColors.Length)
                        continue;

                    Color32 color = sourceColors[vIndex];
                    if (state.HasColorOverride)
                    {
                        color.r = state.ColorOverride.r;
                        color.g = state.ColorOverride.g;
                        color.b = state.ColorOverride.b;
                    }

                    if (state.AlphaMultiplier < 0.999f)
                    {
                        int alpha = Mathf.RoundToInt(color.a * Mathf.Clamp01(state.AlphaMultiplier));
                        color.a = (byte)Mathf.Clamp(alpha, 0, 255);
                    }

                    colors[vIndex] = color;
                }
            }

            _target.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary>
        /// Counts visible characters per effect span for span-relative effects like curved text.
        /// </summary>
        private void RefreshSpanVisibleCharacterCounts(TMP_TextInfo textInfo)
        {
            _spanVisibleProgress.Clear();

            for (int i = 0; i < _parsedSpans.Count; i++)
            {
                ParsedEffectSpan span = _parsedSpans[i];
                span.visibleCharacterCount = 0;
                _parsedSpans[i] = span;
                _spanVisibleProgress.Add(0);
            }

            for (int characterIndex = 0; characterIndex < textInfo.characterCount; characterIndex++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[characterIndex];
                if (!charInfo.isVisible)
                    continue;

                int sourceIndex = charInfo.index;
                for (int spanIndex = 0; spanIndex < _parsedSpans.Count; spanIndex++)
                {
                    ParsedEffectSpan span = _parsedSpans[spanIndex];
                    if (span.effect == null || sourceIndex < span.sourceStartIndex || sourceIndex >= span.sourceEndIndex)
                        continue;

                    span.visibleCharacterCount++;
                    _parsedSpans[spanIndex] = span;
                }
            }
        }
    }
}
