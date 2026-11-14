using System;
using UnityEngine;

namespace CrownlessWarband.Presentation.UI
{
    /// <summary>
    /// Hệ thống hiển thị chữ nổi chiến đấu (Floating Combat Text) và Banner chiến thuật
    /// sử dụng Fixed Struct Array Pool (Zero-GC trong lúc diễn hoạt trận đấu).
    /// </summary>
    public class FloatingTextView : MonoBehaviour
    {
        private const int MaxEntries = 40;

        private struct FloatingTextEntry
        {
            public bool Active;
            public Vector3 WorldPos;
            public string Text;
            public Color Color;
            public float Elapsed;
            public float Duration;
            public int FontSize;
            public float RiseSpeed;
        }

        private readonly FloatingTextEntry[] _entries = new FloatingTextEntry[MaxEntries];

        private bool _bannerActive;
        private string _bannerText = string.Empty;
        private Color _bannerColor = Color.white;
        private float _bannerElapsed;
        private float _bannerDuration;

        private static GUIStyle _textStyle;
        private static GUIStyle _shadowStyle;
        private static GUIStyle _bannerStyle;
        private static GUIStyle _bannerShadowStyle;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < MaxEntries; i++)
            {
                if (!_entries[i].Active) continue;

                _entries[i].Elapsed += dt;
                _entries[i].WorldPos.y += _entries[i].RiseSpeed * dt;

                if (_entries[i].Elapsed >= _entries[i].Duration)
                {
                    _entries[i].Active = false;
                    _entries[i].Text = null;
                }
            }

            if (_bannerActive)
            {
                _bannerElapsed += dt;
                if (_bannerElapsed >= _bannerDuration)
                {
                    _bannerActive = false;
                    _bannerText = null;
                }
            }
        }

        public void SpawnFloatingText(Vector3 worldPos, string text, Color color, float duration = 1.15f, int fontSize = 14, float riseSpeed = 0.85f)
        {
            if (string.IsNullOrEmpty(text)) return;

            int targetIndex = -1;
            int oldestIndex = 0;
            float highestProgress = -1f;

            // Nâng nhẹ vị trí nếu đã có chữ nổi khác xuất hiện ngay sát cùng vị trí trong cùng thời điểm
            for (int i = 0; i < MaxEntries; i++)
            {
                if (!_entries[i].Active)
                {
                    if (targetIndex == -1) targetIndex = i;
                    continue;
                }

                float dx = _entries[i].WorldPos.x - worldPos.x;
                float dy = _entries[i].WorldPos.y - worldPos.y;
                if (dx * dx + dy * dy < 0.09f)
                {
                    worldPos.y += 0.24f;
                }

                float progress = _entries[i].Duration > 0f ? (_entries[i].Elapsed / _entries[i].Duration) : 1f;
                if (progress > highestProgress)
                {
                    highestProgress = progress;
                    oldestIndex = i;
                }
            }

            if (targetIndex == -1)
            {
                targetIndex = oldestIndex;
            }

            _entries[targetIndex] = new FloatingTextEntry
            {
                Active = true,
                WorldPos = worldPos,
                Text = text,
                Color = color,
                Elapsed = 0f,
                Duration = Mathf.Max(0.2f, duration),
                FontSize = Mathf.Clamp(fontSize, 10, 28),
                RiseSpeed = riseSpeed
            };
        }

        public void SpawnTacticalBanner(string bannerText, Color color, float duration = 1.8f)
        {
            if (string.IsNullOrEmpty(bannerText)) return;

            _bannerActive = true;
            _bannerText = bannerText;
            _bannerColor = color;
            _bannerElapsed = 0f;
            _bannerDuration = Mathf.Max(0.25f, duration);
        }

        public void ClearAll()
        {
            for (int i = 0; i < MaxEntries; i++)
            {
                _entries[i].Active = false;
                _entries[i].Text = null;
            }

            _bannerActive = false;
            _bannerText = null;
        }

        private void OnGUI()
        {
            EnsureStyles();

            Camera cam = Camera.main;
            Color prevColor = GUI.color;

            if (cam != null)
            {
                for (int i = 0; i < MaxEntries; i++)
                {
                    if (!_entries[i].Active || string.IsNullOrEmpty(_entries[i].Text)) continue;

                    Vector3 screenPos = cam.WorldToScreenPoint(_entries[i].WorldPos);
                    if (screenPos.z <= 0f) continue;

                    float normalizedTime = Mathf.Clamp01(_entries[i].Elapsed / _entries[i].Duration);

                    // Nhấn mạnh kích thước khi mới bật ra (Pop-in), sau đó ổn định
                    int popBoost = normalizedTime < 0.14f
                        ? Mathf.RoundToInt(Mathf.Lerp(4f, 0f, normalizedTime / 0.14f))
                        : 0;
                    int renderFontSize = _entries[i].FontSize + popBoost;

                    // Mờ dần mượt mà ở 35% cuối thời gian tồn tại
                    float alpha = normalizedTime > 0.65f
                        ? Mathf.Clamp01((1f - normalizedTime) / 0.35f)
                        : 1f;

                    _textStyle.fontSize = renderFontSize;
                    _shadowStyle.fontSize = renderFontSize;

                    float guiX = screenPos.x;
                    float guiY = Screen.height - screenPos.y;
                    const float boxW = 220f;
                    const float boxH = 36f;

                    Rect textRect = new Rect(guiX - boxW * 0.5f, guiY - boxH * 0.5f, boxW, boxH);
                    Rect shadowRect = new Rect(textRect.x + 1.5f, textRect.y + 1.5f, boxW, boxH);

                    GUI.color = new Color(0f, 0f, 0f, 0.85f * alpha);
                    GUI.Label(shadowRect, _entries[i].Text, _shadowStyle);

                    Color c = _entries[i].Color;
                    c.a *= alpha;
                    GUI.color = c;
                    GUI.Label(textRect, _entries[i].Text, _textStyle);
                }
            }

            if (_bannerActive && !string.IsNullOrEmpty(_bannerText))
            {
                float t = Mathf.Clamp01(_bannerElapsed / _bannerDuration);
                float alpha = 1f;
                if (t < 0.10f)
                {
                    alpha = t / 0.10f;
                }
                else if (t > 0.72f)
                {
                    alpha = Mathf.Clamp01((1f - t) / 0.28f);
                }

                float bannerWidth = Mathf.Min(560f, Screen.width * 0.44f);
                float bannerHeight = 36f;
                float bannerX = (Screen.width * 0.5f) - (bannerWidth * 0.5f);
                float bannerY = 88f - (bannerHeight * 0.5f);

                Rect bgRect = new Rect(bannerX, bannerY, bannerWidth, bannerHeight);
                Rect shadowRect = new Rect(bannerX + 1.5f, bannerY + 1.5f, bannerWidth, bannerHeight);

                GUI.color = new Color(0.05f, 0.07f, 0.11f, 0.88f * alpha);
                GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

                GUI.color = new Color(0f, 0f, 0f, 0.90f * alpha);
                GUI.Label(shadowRect, _bannerText, _bannerShadowStyle);

                Color bc = _bannerColor;
                bc.a *= alpha;
                GUI.color = bc;
                GUI.Label(bgRect, _bannerText, _bannerStyle);
            }

            GUI.color = prevColor;
        }

        private static void EnsureStyles()
        {
            if (_textStyle != null) return;

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                wordWrap = false,
                normal = { textColor = Color.white }
            };

            _shadowStyle = new GUIStyle(_textStyle)
            {
                normal = { textColor = Color.white }
            };

            _bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            _bannerShadowStyle = new GUIStyle(_bannerStyle);
        }
    }
}

