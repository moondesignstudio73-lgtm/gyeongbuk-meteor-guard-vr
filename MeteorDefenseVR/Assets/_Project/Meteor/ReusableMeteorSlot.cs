using UnityEngine;

namespace MeteorDefenseVR.Meteor
{
    // One owner, one inactive object. Tutorial/practice/boss keep their existing events and counters.
    public sealed class ReusableMeteorSlot
    {
        private GameObject instance, sourcePrefab;
        public int CreatedCount { get; private set; }

        public void Prewarm(GameObject prefab, Component owner, string fallbackName)
        {
            if (instance != null && sourcePrefab == prefab) return;
            Ensure(prefab, owner, fallbackName);
            Release();
        }

        public GameObject Borrow(GameObject prefab, Component owner, Vector3 position, Quaternion rotation, string fallbackName)
        {
            Ensure(prefab, owner, fallbackName);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        private void Ensure(GameObject prefab, Component owner, string fallbackName)
        {
            if (instance != null && sourcePrefab == prefab) return;
            if (instance != null)
            {
                if (Application.isPlaying) Object.Destroy(instance);
                else Object.DestroyImmediate(instance);
            }
            sourcePrefab = prefab;
            instance = prefab != null ? Object.Instantiate(prefab, owner.transform) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (prefab == null) { instance.name = fallbackName; instance.transform.SetParent(owner.transform, false); }
            var member = instance.GetComponent<PreparedMeteorMember>();
            if (member == null) member = instance.AddComponent<PreparedMeteorMember>();
            member.Owner = owner;
            CreatedCount++;
        }

        public void Release()
        {
            if (instance == null) return;
            var meteor = instance.GetComponent<MeteorController>();
            if (meteor != null) meteor.ResetMeteor(true);
            else instance.SetActive(false);
        }
    }

}
