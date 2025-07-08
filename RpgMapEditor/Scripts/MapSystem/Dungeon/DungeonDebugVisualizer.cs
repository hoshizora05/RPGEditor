using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// ダンジョンデバッグビジュアライザー
    /// </summary>
    public class DungeonDebugVisualizer : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool m_showRooms = true;
        [SerializeField] private bool m_showCorridors = true;
        [SerializeField] private bool m_showTraps = true;
        [SerializeField] private bool m_showLightSources = true;
        [SerializeField] private bool m_showRoomNumbers = true;
        [SerializeField] private bool m_showCriticalPath = true;

        [Header("Colors")]
        [SerializeField] private Color m_roomColor = Color.green;
        [SerializeField] private Color m_corridorColor = Color.blue;
        [SerializeField] private Color m_trapColor = Color.red;
        [SerializeField] private Color m_lightColor = Color.yellow;
        [SerializeField] private Color m_criticalPathColor = Color.magenta;

        private DungeonSystem m_dungeonSystem;

        private void Start()
        {
            m_dungeonSystem = DungeonSystem.Instance;
        }

        private void OnDrawGizmos()
        {
            if (m_dungeonSystem == null || m_dungeonSystem.CurrentLayout == null)
                return;

            DrawDungeonLayout();
        }

        /// <summary>
        /// ダンジョンレイアウトを描画
        /// </summary>
        private void DrawDungeonLayout()
        {
            var layout = m_dungeonSystem.CurrentLayout;

            // 部屋を描画
            if (m_showRooms)
            {
                DrawRooms(layout.rooms);
            }

            // 廊下を描画
            if (m_showCorridors)
            {
                DrawCorridors(layout.corridors);
            }

            // クリティカルパスを描画
            if (m_showCriticalPath)
            {
                DrawCriticalPath(layout.rooms);
            }

            // トラップを描画
            if (m_showTraps && TrapManager.Instance != null)
            {
                DrawTraps();
            }

            // 光源を描画
            if (m_showLightSources && DungeonVisionManager.Instance != null)
            {
                DrawLightSources();
            }
        }

        /// <summary>
        /// 部屋を描画
        /// </summary>
        private void DrawRooms(List<DungeonRoom> rooms)
        {
            foreach (var room in rooms)
            {
                Gizmos.color = GetRoomColor(room.roomType);

                Vector3 center = RpgMapHelper.GetTileCenterPosition(room.center.x, room.center.y);
                Vector3 size = new Vector3(room.bounds.width, room.bounds.height, 1f);

                Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
                if (m_showRoomNumbers)
                {
                    UnityEditor.Handles.Label(center, $"R{room.roomID}\n{room.roomType}");
                }
#endif
            }
        }

        /// <summary>
        /// 部屋タイプに応じた色を取得
        /// </summary>
        private Color GetRoomColor(eRoomType roomType)
        {
            switch (roomType)
            {
                case eRoomType.Boss: return Color.red;
                case eRoomType.Treasure: return Color.yellow;
                case eRoomType.Secret: return Color.cyan;
                case eRoomType.Puzzle: return Color.magenta;
                case eRoomType.Trap: return Color.green;
                default: return m_roomColor;
            }
        }

        /// <summary>
        /// 廊下を描画
        /// </summary>
        private void DrawCorridors(List<DungeonCorridor> corridors)
        {
            Gizmos.color = m_corridorColor;

            foreach (var corridor in corridors)
            {
                for (int i = 0; i < corridor.path.Count - 1; i++)
                {
                    Vector3 start = RpgMapHelper.GetTileCenterPosition(corridor.path[i].x, corridor.path[i].y);
                    Vector3 end = RpgMapHelper.GetTileCenterPosition(corridor.path[i + 1].x, corridor.path[i + 1].y);

                    Gizmos.DrawLine(start, end);
                }
            }
        }

        /// <summary>
        /// クリティカルパスを描画
        /// </summary>
        private void DrawCriticalPath(List<DungeonRoom> rooms)
        {
            Gizmos.color = m_criticalPathColor;

            var criticalRooms = rooms.Where(r => r.isMainPath).OrderBy(r => r.distanceFromStart).ToList();

            for (int i = 0; i < criticalRooms.Count - 1; i++)
            {
                Vector3 start = RpgMapHelper.GetTileCenterPosition(criticalRooms[i].center.x, criticalRooms[i].center.y);
                Vector3 end = RpgMapHelper.GetTileCenterPosition(criticalRooms[i + 1].center.x, criticalRooms[i + 1].center.y);

                Gizmos.DrawLine(start, end);
            }
        }

        /// <summary>
        /// トラップを描画
        /// </summary>
        private void DrawTraps()
        {
            Gizmos.color = m_trapColor;

            var traps = TrapManager.Instance.GetActiveTraps();
            foreach (var trap in traps)
            {
                Vector3 pos = RpgMapHelper.GetTileCenterPosition(trap.GridPosition.x, trap.GridPosition.y);
                Gizmos.DrawWireSphere(pos, 0.5f);
            }
        }

        /// <summary>
        /// 光源を描画
        /// </summary>
        private void DrawLightSources()
        {
            var lightSources = DungeonVisionManager.Instance.GetActiveLightSources();

            foreach (var light in lightSources)
            {
                Gizmos.color = light.IsActive ? m_lightColor : Color.gray;

                Vector3 pos = RpgMapHelper.GetTileCenterPosition(light.GridPosition.x, light.GridPosition.y);
                Gizmos.DrawWireSphere(pos, light.LightData.radius);
                Gizmos.DrawSphere(pos, 0.2f);
            }
        }
    }
}