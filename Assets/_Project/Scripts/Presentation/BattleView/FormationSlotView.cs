using System;
using CrownlessWarband.Core.Battle;
using UnityEngine;

namespace CrownlessWarband.Presentation.BattleView
{
    /// <summary>
    /// Đại diện trực quan cho một ô đứng trên lưới đội hình 3x3 (GDD Sections 8, 9).
    /// Tự động dựng Sprite nền ô và BoxCollider2D để hỗ trợ click chuột xếp đội hình.
    /// </summary>
    public class FormationSlotView : MonoBehaviour
    {
        public TeamSide Team { get; private set; }
        public FormationPosition Position { get; private set; }
        public event Action<FormationSlotView> OnSlotClicked;

        private SpriteRenderer _borderRenderer;
        private SpriteRenderer _floorRenderer;
        private Color _baseFloorColor;
        private Color _baseBorderColor;
        private bool _isInitialized;

        public void Initialize(TeamSide team, FormationPosition pos, Sprite defaultSquareSprite)
        {
            Team = team;
            Position = pos;
            gameObject.name = $"Slot_{team}_{pos.Row}_{pos.Column}";

            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider2D>();
            }
            col.size = new Vector2(1.3f, 1.3f);

            if (!_isInitialized)
            {
                GameObject borderObj = new GameObject("TileBorder");
                borderObj.transform.SetParent(transform, false);
                borderObj.transform.localScale = new Vector3(1.22f, 1.22f, 1f);
                _borderRenderer = borderObj.AddComponent<SpriteRenderer>();
                _borderRenderer.sortingOrder = -2;

                GameObject floorObj = new GameObject("TileFloor");
                floorObj.transform.SetParent(transform, false);
                floorObj.transform.localScale = new Vector3(1.18f, 1.18f, 1f);
                _floorRenderer = floorObj.AddComponent<SpriteRenderer>();
                _floorRenderer.sortingOrder = -1;

                _isInitialized = true;
            }

            _borderRenderer.sprite = ProceduralUnitArtFactory.GetRoundedTileOutlineSprite();
            _floorRenderer.sprite = ProceduralUnitArtFactory.GetRoundedTileFloorSprite();

            _baseFloorColor = ResolveBaseColor(team, pos.Row);
            _baseBorderColor = new Color(
                Mathf.Min(1f, _baseFloorColor.r * 1.45f + 0.14f),
                Mathf.Min(1f, _baseFloorColor.g * 1.45f + 0.14f),
                Mathf.Min(1f, _baseFloorColor.b * 1.45f + 0.14f),
                0.55f
            );

            SetHighlighted(false, Color.white);
        }

        public void SetHighlighted(bool highlighted, Color highlightColor)
        {
            if (!_isInitialized) return;

            if (highlighted)
            {
                _borderRenderer.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.95f);
                Color lerpedFloor = Color.Lerp(_baseFloorColor, highlightColor, 0.45f);
                _floorRenderer.color = new Color(lerpedFloor.r, lerpedFloor.g, lerpedFloor.b, 0.65f);
            }
            else
            {
                _borderRenderer.color = _baseBorderColor;
                _floorRenderer.color = _baseFloorColor;
            }
        }

        private void OnMouseDown()
        {
            OnSlotClicked?.Invoke(this);
        }

        private static Color ResolveBaseColor(TeamSide team, RowType row)
        {
            if (team == TeamSide.Player)
            {
                switch (row)
                {
                    case RowType.Front: return new Color(0.12f, 0.34f, 0.42f, 0.32f);
                    case RowType.Mid:   return new Color(0.10f, 0.25f, 0.36f, 0.32f);
                    case RowType.Back:  return new Color(0.08f, 0.19f, 0.30f, 0.32f);
                }
            }
            else
            {
                switch (row)
                {
                    case RowType.Front: return new Color(0.42f, 0.14f, 0.14f, 0.32f);
                    case RowType.Mid:   return new Color(0.35f, 0.12f, 0.16f, 0.32f);
                    case RowType.Back:  return new Color(0.26f, 0.10f, 0.18f, 0.32f);
                }
            }

            return new Color(0.15f, 0.15f, 0.20f, 0.32f);
        }
    }
}

