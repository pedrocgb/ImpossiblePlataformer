#region Usings
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#endregion

namespace Pedro.TurnBasedDeckbuilder.TextAnimation
{
    public enum TextAnimatorEffectKind
    {
        Wave = 0,
        Wiggle = 1,
        Shake = 2,
        Bounce = 3,
        Swing = 4,
        Pendulum = 5,
        Dangle = 6,
        Fade = 7,
        Rainbow = 8,
        Rotate = 9,
        SlideHorizontal = 10,
        IncreaseSize = 11,
        CustomCurve = 12,
        CurvedText = 13
    }

    [Serializable]
    public struct TextAnimatorTagModifiers
    {
        public float amplitude;
        public float speed;
        public float frequency;
        public float waveSize;
        public float delay;

        public static TextAnimatorTagModifiers Default => new TextAnimatorTagModifiers
        {
            amplitude = 1f,
            speed = 1f,
            frequency = 1f,
            waveSize = 1f,
            delay = 0f
        };
    }

    public struct TextAnimatorCharacterState
    {
        public Vector3 PositionOffset;
        public float RotationDegrees;
        public Vector3 ScaleMultiplier;
        public float AlphaMultiplier;
        public bool HasColorOverride;
        public Color32 ColorOverride;

        /// <summary>
        /// Creates the neutral per-character animation state before effects are applied.
        /// </summary>
        public static TextAnimatorCharacterState CreateDefault()
        {
            return new TextAnimatorCharacterState
            {
                PositionOffset = Vector3.zero,
                RotationDegrees = 0f,
                ScaleMultiplier = Vector3.one,
                AlphaMultiplier = 1f,
                HasColorOverride = false,
                ColorOverride = new Color32(255, 255, 255, 255)
            };
        }
    }

    [Serializable]
    public sealed class TextAnimatorEffectDefinition
    {
        private const float Tau = Mathf.PI * 2f;

        [FoldoutGroup("General"), SerializeField] private bool _enabled = true;
        [FoldoutGroup("General"), SerializeField] private string _displayName = "Wave";
        [FoldoutGroup("General"), SerializeField] private string _tagId = "wave";
        [FoldoutGroup("General"), SerializeField, EnumToggleButtons] private TextAnimatorEffectKind _kind = TextAnimatorEffectKind.Wave;

        [FoldoutGroup("Motion"), SerializeField, MinValue(0f)] private float _amplitude = 1f;
        [FoldoutGroup("Motion"), SerializeField, MinValue(0f)] private float _speed = 1f;
        [FoldoutGroup("Motion"), SerializeField, MinValue(0f)] private float _frequency = 0.25f;
        [FoldoutGroup("Motion"), SerializeField] private float _phaseOffset = 0f;

        [FoldoutGroup("Modifiers"), InfoBox("Default supported runtime modifiers: a (amplitude), s (speed), f (frequency), w (wave size), d (delay)."), SerializeField]
        private string _amplitudeModifierId = "a";
        [FoldoutGroup("Modifiers"), SerializeField] private string _speedModifierId = "s";
        [FoldoutGroup("Modifiers"), SerializeField] private string _frequencyModifierId = "f";

        [FoldoutGroup("Shake"), SerializeField, ShowIf(nameof(ShowShakeSettings))]
        private Vector2 _shakeAxisMultiplier = Vector2.one;

        [FoldoutGroup("Color"), SerializeField, ShowIf(nameof(ShowRainbowSettings))]
        private Gradient _rainbowGradient = DefaultRainbowGradient();

        [FoldoutGroup("Custom"), SerializeField, ShowIf(nameof(ShowCustomSettings))]
        private AnimationCurve _customPositionX = AnimationCurve.Linear(0f, 0f, 1f, 0f);

        [FoldoutGroup("Custom"), SerializeField, ShowIf(nameof(ShowCustomSettings))]
        private AnimationCurve _customPositionY = AnimationCurve.Linear(0f, 0f, 1f, 0f);

        [FoldoutGroup("Custom"), SerializeField, ShowIf(nameof(ShowCustomSettings))]
        private AnimationCurve _customScale = AnimationCurve.Linear(0f, 0f, 1f, 0f);

        [FoldoutGroup("Custom"), SerializeField, ShowIf(nameof(ShowCustomSettings))]
        private AnimationCurve _customRotation = AnimationCurve.Linear(0f, 0f, 1f, 0f);

        [FoldoutGroup("Custom"), SerializeField, ShowIf(nameof(ShowCustomSettings))]
        private AnimationCurve _customAlpha = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [FoldoutGroup("Custom"), SerializeField, MinValue(0.01f), ShowIf(nameof(ShowCustomSettings))]
        private float _customCycleDuration = 1f;

        [FoldoutGroup("Custom"), SerializeField, ShowIf(nameof(ShowCustomSettings))]
        private Vector2 _customPositionMultiplier = new Vector2(1f, 1f);

        [FoldoutGroup("Custom"), SerializeField, ShowIf(nameof(ShowCustomSettings))]
        private float _customScaleMultiplier = 0.3f;

        [FoldoutGroup("Custom"), SerializeField, ShowIf(nameof(ShowCustomSettings))]
        private float _customRotationMultiplier = 25f;

        [FoldoutGroup("Custom"), SerializeField, ShowIf(nameof(ShowCustomSettings)), Range(0f, 1f)]
        private float _customAlphaBlend = 1f;

        public bool Enabled => _enabled;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _kind.ToString() : _displayName;
        public string TagId => NormalizeTagId(_tagId);
        public string AmplitudeModifierId => NormalizeTagId(_amplitudeModifierId);
        public string SpeedModifierId => NormalizeTagId(_speedModifierId);
        public string FrequencyModifierId => NormalizeTagId(_frequencyModifierId);
        public TextAnimatorEffectKind Kind => _kind;

        private bool ShowShakeSettings => _kind == TextAnimatorEffectKind.Shake;
        private bool ShowRainbowSettings => _kind == TextAnimatorEffectKind.Rainbow;
        private bool ShowCustomSettings => _kind == TextAnimatorEffectKind.CustomCurve;

        /// <summary>
        /// Applies this effect to one character state using timing, span, and modifier data.
        /// </summary>
        public void Apply(
            ref TextAnimatorCharacterState state,
            float elapsedTime,
            int visibleCharacterIndex,
            int spanCharacterIndex,
            int spanCharacterCount,
            in TextAnimatorTagModifiers modifiers,
            Color32 sourceColor)
        {
            if (!_enabled)
                return;

            float amplitude = Mathf.Max(0f, _amplitude) * Mathf.Max(0f, modifiers.amplitude);
            float speed = Mathf.Max(0f, _speed) * Mathf.Max(0f, modifiers.speed);
            float waveSize = Mathf.Max(0f, modifiers.waveSize);
            float frequency = Mathf.Max(0f, _frequency) * Mathf.Max(0f, modifiers.frequency) * waveSize;
            float delayedElapsed = Mathf.Max(0f, elapsedTime - Mathf.Max(0f, modifiers.delay));
            float phase = ComposePhase(delayedElapsed, visibleCharacterIndex, speed, frequency);

            switch (_kind)
            {
                case TextAnimatorEffectKind.Wave:
                {
                    state.PositionOffset += new Vector3(0f, Mathf.Sin(phase) * amplitude, 0f);
                    break;
                }

                case TextAnimatorEffectKind.Wiggle:
                {
                    float x = Mathf.Sin(phase) * amplitude * 0.45f;
                    float y = Mathf.Cos(phase * 1.2f) * amplitude * 0.45f;
                    state.PositionOffset += new Vector3(x, y, 0f);
                    state.RotationDegrees += Mathf.Sin(phase * 0.9f) * amplitude * 8f;
                    break;
                }

                case TextAnimatorEffectKind.Shake:
                {
                    float t = delayedElapsed * Mathf.Max(0.001f, speed);
                    float seed = visibleCharacterIndex * 0.173f + _phaseOffset;
                    float noiseX = Mathf.PerlinNoise(seed + 11.31f, t + 3.17f) * 2f - 1f;
                    float noiseY = Mathf.PerlinNoise(seed + 27.91f, t + 7.41f) * 2f - 1f;
                    state.PositionOffset += new Vector3(
                        noiseX * amplitude * _shakeAxisMultiplier.x,
                        noiseY * amplitude * _shakeAxisMultiplier.y,
                        0f);
                    break;
                }

                case TextAnimatorEffectKind.Bounce:
                {
                    float y = Mathf.Abs(Mathf.Sin(phase)) * amplitude;
                    state.PositionOffset += new Vector3(0f, y, 0f);
                    break;
                }

                case TextAnimatorEffectKind.Swing:
                {
                    state.RotationDegrees += Mathf.Sin(phase) * amplitude * 18f;
                    break;
                }

                case TextAnimatorEffectKind.Pendulum:
                {
                    state.RotationDegrees += Mathf.Sin(phase) * amplitude * 26f;
                    state.PositionOffset += new Vector3(0f, -Mathf.Abs(Mathf.Cos(phase * 0.5f)) * amplitude * 0.2f, 0f);
                    break;
                }

                case TextAnimatorEffectKind.Dangle:
                {
                    float rot = (Mathf.Sin(phase) + Mathf.Sin(phase * 0.33f)) * 0.5f * amplitude * 30f;
                    float x = Mathf.Sin(phase * 0.5f) * amplitude * 0.15f;
                    state.RotationDegrees += rot;
                    state.PositionOffset += new Vector3(x, 0f, 0f);
                    break;
                }

                case TextAnimatorEffectKind.Fade:
                {
                    float osc = 0.5f + 0.5f * Mathf.Sin(phase);
                    float faded = Mathf.Lerp(1f, 0.15f + 0.85f * osc, Mathf.Clamp01(amplitude));
                    state.AlphaMultiplier *= Mathf.Clamp01(faded);
                    break;
                }

                case TextAnimatorEffectKind.Rainbow:
                {
                    float rainbowT = Mathf.Repeat(delayedElapsed * speed + visibleCharacterIndex * frequency + _phaseOffset, 1f);
                    Color color = _rainbowGradient.Evaluate(rainbowT);
                    color.a = sourceColor.a / 255f;
                    state.HasColorOverride = true;
                    state.ColorOverride = color;
                    break;
                }

                case TextAnimatorEffectKind.Rotate:
                {
                    state.RotationDegrees += (delayedElapsed * speed * 90f + visibleCharacterIndex * frequency * 5f) * Mathf.Max(0.01f, amplitude);
                    break;
                }

                case TextAnimatorEffectKind.SlideHorizontal:
                {
                    float x = Mathf.Sin(phase) * amplitude;
                    state.PositionOffset += new Vector3(x, 0f, 0f);
                    break;
                }

                case TextAnimatorEffectKind.IncreaseSize:
                {
                    float pulse = 1f + (0.25f + 0.25f * Mathf.Sin(phase)) * amplitude;
                    state.ScaleMultiplier = Vector3.Scale(state.ScaleMultiplier, Vector3.one * Mathf.Max(0.01f, pulse));
                    break;
                }

                case TextAnimatorEffectKind.CustomCurve:
                {
                    ApplyCustomCurve(ref state, delayedElapsed, visibleCharacterIndex, speed, frequency, amplitude);
                    break;
                }

                case TextAnimatorEffectKind.CurvedText:
                {
                    ApplyCurvedText(ref state, spanCharacterIndex, spanCharacterCount, amplitude);
                    break;
                }
            }
        }

        /// <summary>
        /// Normalizes the effect tag and modifier ids for lookup.
        /// </summary>
        public void NormalizeIds()
        {
            _tagId = NormalizeTagId(_tagId);
            _amplitudeModifierId = NormalizeTagId(_amplitudeModifierId);
            _speedModifierId = NormalizeTagId(_speedModifierId);
            _frequencyModifierId = NormalizeTagId(_frequencyModifierId);
        }

        [FoldoutGroup("Actions"), Button(ButtonSizes.Small)]
        /// <summary>
        /// Uses the effect kind to fill a default tag id and display name.
        /// </summary>
        private void FillTagFromKind()
        {
            _tagId = SuggestedTagForKind(_kind);
            _displayName = _kind.ToString();
            NormalizeIds();
        }

        /// <summary>
        /// Applies authored custom animation curves to one character state.
        /// </summary>
        private void ApplyCustomCurve(
            ref TextAnimatorCharacterState state,
            float elapsedTime,
            int visibleCharacterIndex,
            float speed,
            float frequency,
            float amplitude)
        {
            float cycle = Mathf.Max(0.01f, _customCycleDuration);
            float raw = elapsedTime * speed + (visibleCharacterIndex * frequency) + _phaseOffset;
            float t = Mathf.Repeat(raw, cycle) / cycle;

            float x = _customPositionX.Evaluate(t) * _customPositionMultiplier.x * amplitude;
            float y = _customPositionY.Evaluate(t) * _customPositionMultiplier.y * amplitude;
            state.PositionOffset += new Vector3(x, y, 0f);

            float scaleCurve = _customScale.Evaluate(t) * _customScaleMultiplier * amplitude;
            float scaled = Mathf.Max(0.01f, 1f + scaleCurve);
            state.ScaleMultiplier = Vector3.Scale(state.ScaleMultiplier, Vector3.one * scaled);

            float rot = _customRotation.Evaluate(t) * _customRotationMultiplier * amplitude;
            state.RotationDegrees += rot;

            float alphaValue = Mathf.Clamp01(_customAlpha.Evaluate(t));
            float alphaBlend = Mathf.Clamp01(_customAlphaBlend);
            state.AlphaMultiplier *= Mathf.Lerp(1f, alphaValue, alphaBlend);
        }

        /// <summary>
        /// Places one character on a span-centered arc controlled by amplitude.
        /// </summary>
        private static void ApplyCurvedText(
            ref TextAnimatorCharacterState state,
            int spanCharacterIndex,
            int spanCharacterCount,
            float amplitude)
        {
            int safeCharacterCount = Mathf.Max(1, spanCharacterCount);
            float normalizedPosition = safeCharacterCount <= 1
                ? 0f
                : (spanCharacterIndex / (float)(safeCharacterCount - 1) * 2f) - 1f;
            float curveHeight = 1f - normalizedPosition * normalizedPosition;
            float tangent = -2f * normalizedPosition * amplitude;

            state.PositionOffset += new Vector3(0f, curveHeight * amplitude, 0f);
            state.RotationDegrees += Mathf.Atan(tangent * 0.08f) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Builds the shared oscillation phase used by time-based effects.
        /// </summary>
        private float ComposePhase(float elapsedTime, int visibleCharacterIndex, float speed, float frequency)
        {
            return elapsedTime * speed * Tau + visibleCharacterIndex * frequency + _phaseOffset;
        }

        /// <summary>
        /// Creates a built-in effect definition with standard modifier ids and defaults.
        /// </summary>
        public static TextAnimatorEffectDefinition CreateBuiltIn(
            string displayName,
            string tag,
            TextAnimatorEffectKind kind,
            float amplitude = 1f,
            float speed = 1f,
            float frequency = 0.25f)
        {
            return new TextAnimatorEffectDefinition
            {
                _enabled = true,
                _displayName = displayName,
                _tagId = NormalizeTagId(tag),
                _kind = kind,
                _amplitude = Mathf.Max(0f, amplitude),
                _speed = Mathf.Max(0f, speed),
                _frequency = Mathf.Max(0f, frequency),
                _phaseOffset = 0f,
                _amplitudeModifierId = "a",
                _speedModifierId = "s",
                _frequencyModifierId = "f",
                _shakeAxisMultiplier = Vector2.one,
                _rainbowGradient = DefaultRainbowGradient(),
                _customPositionX = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                _customPositionY = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                _customScale = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                _customRotation = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                _customAlpha = AnimationCurve.Linear(0f, 1f, 1f, 1f),
                _customCycleDuration = 1f,
                _customPositionMultiplier = new Vector2(1f, 1f),
                _customScaleMultiplier = 0.3f,
                _customRotationMultiplier = 25f,
                _customAlphaBlend = 1f
            };
        }

        /// <summary>
        /// Gets the default text tag id for a built-in effect kind.
        /// </summary>
        public static string SuggestedTagForKind(TextAnimatorEffectKind kind)
        {
            switch (kind)
            {
                case TextAnimatorEffectKind.Pendulum: return "pend";
                case TextAnimatorEffectKind.Dangle: return "dangle";
                case TextAnimatorEffectKind.Fade: return "fade";
                case TextAnimatorEffectKind.Rainbow: return "rainb";
                case TextAnimatorEffectKind.Rotate: return "rot";
                case TextAnimatorEffectKind.Bounce: return "bounce";
                case TextAnimatorEffectKind.SlideHorizontal: return "slideh";
                case TextAnimatorEffectKind.Swing: return "swing";
                case TextAnimatorEffectKind.Wave: return "wave";
                case TextAnimatorEffectKind.IncreaseSize: return "incr";
                case TextAnimatorEffectKind.Shake: return "shake";
                case TextAnimatorEffectKind.Wiggle: return "wiggle";
                case TextAnimatorEffectKind.CurvedText: return "curve";
                case TextAnimatorEffectKind.CustomCurve:
                default:
                    return "custom";
            }
        }

        /// <summary>
        /// Converts a tag id to the normalized lookup format.
        /// </summary>
        private static string NormalizeTagId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Builds the default rainbow gradient used by rainbow effects.
        /// </summary>
        private static Gradient DefaultRainbowGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.15f, 0.15f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.2f),
                    new GradientColorKey(new Color(0.25f, 1f, 0.25f), 0.4f),
                    new GradientColorKey(new Color(0.2f, 0.8f, 1f), 0.6f),
                    new GradientColorKey(new Color(0.45f, 0.35f, 1f), 0.8f),
                    new GradientColorKey(new Color(1f, 0.25f, 0.75f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }
    }

    [CreateAssetMenu(menuName = "Breezeblocks/Text Animation/Effect Database", fileName = "text_animator_effect_database")]
    public sealed class TextAnimatorEffectDatabase : ScriptableObject
    {
        [FoldoutGroup("Effects"), SerializeField]
        [ListDrawerSettings(ShowFoldout = true, NumberOfItemsPerPage = 20, DraggableItems = true)]
        private List<TextAnimatorEffectDefinition> _effects = new List<TextAnimatorEffectDefinition>();

        private readonly Dictionary<string, TextAnimatorEffectDefinition> _effectByTag =
            new Dictionary<string, TextAnimatorEffectDefinition>(StringComparer.Ordinal);

        private bool _lookupDirty = true;

        public IReadOnlyList<TextAnimatorEffectDefinition> Effects
        {
            get
            {
                EnsureRequiredBuiltInEffects();
                return _effects;
            }
        }

        /// <summary>
        /// Tries to find an enabled effect definition by text tag id.
        /// </summary>
        public bool TryGetEffectByTag(string tagId, out TextAnimatorEffectDefinition effect)
        {
            effect = null;
            if (string.IsNullOrWhiteSpace(tagId))
                return false;

            EnsureRequiredBuiltInEffects();
            RebuildLookupIfNeeded();

            string normalized = NormalizeTagId(tagId);
            if (_effectByTag.TryGetValue(normalized, out TextAnimatorEffectDefinition found) && found != null)
            {
                effect = found;
                return true;
            }

            return false;
        }

        [FoldoutGroup("Actions"), Button(ButtonSizes.Medium)]
        /// <summary>
        /// Replaces the database contents with the built-in effect set.
        /// </summary>
        public void PopulateWithBuiltInEffects()
        {
            _effects.Clear();

            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Pendulum", "pend", TextAnimatorEffectKind.Pendulum, 1f, 1f, 0.35f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Dangle", "dangle", TextAnimatorEffectKind.Dangle, 1f, 1f, 0.3f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Fade", "fade", TextAnimatorEffectKind.Fade, 1f, 1f, 0.2f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Rainbow", "rainb", TextAnimatorEffectKind.Rainbow, 1f, 1f, 0.08f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Rotate", "rot", TextAnimatorEffectKind.Rotate, 1f, 1f, 0.15f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Bounce", "bounce", TextAnimatorEffectKind.Bounce, 1f, 1f, 0.4f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Slide", "slideh", TextAnimatorEffectKind.SlideHorizontal, 1f, 1f, 0.35f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Slide (Legacy Tag)", "slide", TextAnimatorEffectKind.SlideHorizontal, 1f, 1f, 0.35f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Swing", "swing", TextAnimatorEffectKind.Swing, 1f, 1f, 0.35f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Wave", "wave", TextAnimatorEffectKind.Wave, 1f, 1f, 0.35f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Increase Size", "incr", TextAnimatorEffectKind.IncreaseSize, 1f, 1f, 0.35f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Shake", "shake", TextAnimatorEffectKind.Shake, 1f, 1f, 0.35f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Wiggle", "wiggle", TextAnimatorEffectKind.Wiggle, 1f, 1f, 0.35f));
            _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Curved Text", "curve", TextAnimatorEffectKind.CurvedText, 2f, 1f, 0f));

            _lookupDirty = true;
        }

        [FoldoutGroup("Actions"), Button(ButtonSizes.Medium)]
        /// <summary>
        /// Normalizes all effect tags and logs duplicate tag warnings.
        /// </summary>
        public void NormalizeAndValidateTags()
        {
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _effects.Count; i++)
            {
                TextAnimatorEffectDefinition effect = _effects[i];
                if (effect == null)
                    continue;

                effect.NormalizeIds();
                string tag = effect.TagId;
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                if (!used.Add(tag))
                    Debug.LogWarning($"[{nameof(TextAnimatorEffectDatabase)}] Duplicate text effect tag '{tag}' detected at index {i}. Only first occurrence will be used.", this);
            }

            _lookupDirty = true;
        }

        /// <summary>
        /// Marks the lookup dirty when inspector data changes.
        /// </summary>
        private void OnValidate()
        {
            _lookupDirty = true;
        }

        /// <summary>
        /// Rebuilds the tag-to-effect lookup table when serialized data changed.
        /// </summary>
        private void RebuildLookupIfNeeded()
        {
            if (!_lookupDirty)
                return;

            _effectByTag.Clear();

            for (int i = 0; i < _effects.Count; i++)
            {
                TextAnimatorEffectDefinition effect = _effects[i];
                if (effect == null || !effect.Enabled)
                    continue;

                string tag = effect.TagId;
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                if (_effectByTag.ContainsKey(tag))
                    continue;

                _effectByTag[tag] = effect;
            }

            _lookupDirty = false;
        }

        /// <summary>
        /// Adds required built-in effects that older database assets may not contain yet.
        /// </summary>
        private void EnsureRequiredBuiltInEffects()
        {
            _effects ??= new List<TextAnimatorEffectDefinition>();

            if (!HasEffectTag("curve"))
            {
                _effects.Add(TextAnimatorEffectDefinition.CreateBuiltIn("Curved Text", "curve", TextAnimatorEffectKind.CurvedText, 2f, 1f, 0f));
                _lookupDirty = true;
            }
        }

        /// <summary>
        /// Checks whether the database already contains an effect with the requested tag.
        /// </summary>
        private bool HasEffectTag(string tagId)
        {
            string normalizedTag = NormalizeTagId(tagId);

            for (int i = 0; i < _effects.Count; i++)
            {
                TextAnimatorEffectDefinition effect = _effects[i];
                if (effect != null && string.Equals(effect.TagId, normalizedTag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Converts a database tag id to the normalized lookup format.
        /// </summary>
        private static string NormalizeTagId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToLowerInvariant();
        }
    }
}
