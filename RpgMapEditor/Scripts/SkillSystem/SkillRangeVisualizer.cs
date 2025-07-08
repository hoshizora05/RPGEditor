using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGSkillSystem
{
    /// <summary>
    /// スキル範囲表示ヘルパー
    /// </summary>
    public class SkillRangeVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        public Material areaMaterial;
        public Material lineMaterial;
        public Color validTargetColor = Color.green;
        public Color invalidTargetColor = Color.red;

        private GameObject currentRangeIndicator;
        private LineRenderer lineRenderer;

        public void ShowSkillRange(SkillDefinition skill, Vector3? targetPosition = null)
        {
            HideSkillRange();

            if (skill == null) return;

            switch (skill.targeting.targetType)
            {
                case TargetType.AreaCircle:
                    ShowCircleRange(skill.targeting, targetPosition ?? transform.position);
                    break;

                case TargetType.AreaCone:
                    ShowConeRange(skill.targeting, targetPosition ?? transform.forward);
                    break;

                case TargetType.AreaLine:
                    ShowLineRange(skill.targeting, targetPosition ?? transform.forward);
                    break;

                case TargetType.SingleTarget:
                    ShowSingleTargetRange(skill.targeting);
                    break;
            }
        }

        private void ShowCircleRange(TargetingData targeting, Vector3 center)
        {
            currentRangeIndicator = CreateCircleIndicator(center, targeting.areaSize);
        }

        private void ShowConeRange(TargetingData targeting, Vector3 direction)
        {
            currentRangeIndicator = CreateConeIndicator(transform.position, direction, targeting.range, targeting.coneAngle);
        }

        private void ShowLineRange(TargetingData targeting, Vector3 direction)
        {
            if (lineRenderer == null)
            {
                var lineObj = new GameObject("Line Range");
                lineRenderer = lineObj.AddComponent<LineRenderer>();
                lineRenderer.material = lineMaterial;
                lineRenderer.startWidth = 0.5f;
                lineRenderer.endWidth = 0.5f;
                lineRenderer.positionCount = 2;
            }

            lineRenderer.gameObject.SetActive(true);
            Vector3 start = transform.position;
            Vector3 end = start + direction.normalized * targeting.range;

            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }

        private void ShowSingleTargetRange(TargetingData targeting)
        {
            currentRangeIndicator = CreateCircleIndicator(transform.position, targeting.range);
            currentRangeIndicator.GetComponent<Renderer>().material.color = new Color(1, 1, 1, 0.2f);
        }

        private GameObject CreateCircleIndicator(Vector3 center, float radius)
        {
            var circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            circle.transform.position = center;
            circle.transform.localScale = new Vector3(radius * 2, 0.01f, radius * 2);

            var renderer = circle.GetComponent<Renderer>();
            renderer.material = areaMaterial;

            var collider = circle.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            return circle;
        }

        private GameObject CreateConeIndicator(Vector3 origin, Vector3 direction, float range, float angle)
        {
            var cone = new GameObject("Cone Indicator");
            var meshFilter = cone.AddComponent<MeshFilter>();
            var meshRenderer = cone.AddComponent<MeshRenderer>();
            meshRenderer.material = areaMaterial;

            // Create cone mesh
            meshFilter.mesh = CreateConeMesh(range, angle);
            cone.transform.position = origin;
            cone.transform.rotation = Quaternion.LookRotation(direction);

            return cone;
        }

        private Mesh CreateConeMesh(float range, float angle)
        {
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // Add apex vertex
            vertices.Add(Vector3.zero);

            // Add base vertices
            int segments = 20;
            float angleStep = angle / segments;
            float startAngle = -angle / 2f;

            for (int i = 0; i <= segments; i++)
            {
                float currentAngle = startAngle + angleStep * i;
                float x = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * range;
                float z = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * range;
                vertices.Add(new Vector3(x, 0, z));
            }

            // Create triangles
            for (int i = 1; i <= segments; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();

            return mesh;
        }

        public void HideSkillRange()
        {
            if (currentRangeIndicator != null)
            {
                Destroy(currentRangeIndicator);
                currentRangeIndicator = null;
            }

            if (lineRenderer != null)
            {
                lineRenderer.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            HideSkillRange();
        }
    }

}