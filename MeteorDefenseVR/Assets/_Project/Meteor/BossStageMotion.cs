using System.Collections.Generic;
using UnityEngine;

namespace MeteorDefenseVR.Meteor
{
    // Reused lightweight presentation: irregular disaster drift and pooled visual debris.
    [DisallowMultipleComponent]
    public sealed class BossStageMotion : MonoBehaviour
    {
        private readonly List<Transform> fragments = new List<Transform>(8);
        private float variation, elapsed;
        private Vector3 previousOffset;

        public void Configure(float patternVariation, int fragmentCount)
        {
            variation = Mathf.Clamp01(patternVariation);
            EnsureFragments(Mathf.Clamp(fragmentCount, 0, 8));
            for (int i = 0; i < fragments.Count; i++) fragments[i].gameObject.SetActive(i < fragmentCount);
            elapsed = 0f; previousOffset = Vector3.zero;
            enabled = variation > 0f || fragmentCount > 0;
        }

        private void LateUpdate()
        {
            if (Time.timeScale <= 0) return;
            elapsed += Time.deltaTime;
            Vector3 offset = new Vector3(Mathf.Sin(elapsed * .73f), Mathf.Sin(elapsed * 1.07f + 1.8f), 0f) * (.006f * variation);
            transform.position += offset - previousOffset; previousOffset = offset;
            transform.Rotate(new Vector3(11f, 17f, 7f) * (variation * Time.deltaTime), Space.Self);
            for (int i = 0; i < fragments.Count; i++)
            {
                Transform fragment = fragments[i]; if (!fragment.gameObject.activeSelf) continue;
                float angle = elapsed * (16f + i * 1.7f) + i * 137.5f;
                float radius = 1.15f + i * .16f;
                fragment.localPosition = Quaternion.Euler(Mathf.Sin(angle * Mathf.Deg2Rad) * 24f, angle, 0) * Vector3.right * radius;
                fragment.Rotate(Vector3.one * (35f * Time.deltaTime), Space.Self);
            }
        }

        private void EnsureFragments(int count)
        {
            MeshFilter sourceFilter = GetComponentInChildren<MeshFilter>();
            MeshRenderer sourceRenderer = sourceFilter != null ? sourceFilter.GetComponent<MeshRenderer>() : null;
            while (fragments.Count < count)
            {
                var child = new GameObject("BossDebris_" + fragments.Count, typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(transform, false); child.transform.localScale = Vector3.one * Random.Range(.055f, .11f);
                if (sourceFilter != null) child.GetComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                if (sourceRenderer != null) child.GetComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
                fragments.Add(child.transform);
            }
        }

        private void OnDisable()
        {
            previousOffset = Vector3.zero;
            foreach (Transform fragment in fragments) if (fragment != null) fragment.gameObject.SetActive(false);
        }
    }
}
