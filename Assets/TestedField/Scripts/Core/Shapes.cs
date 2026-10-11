using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Сборка моделей из примитивов (герой, мобы, окружение, зоны) — как «простые блоки» в UE-прототипе.
    /// </summary>
    public static class Shapes
    {
        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool collider = true)
        {
            return Make(PrimitiveType.Cube, parent, name, localPos, Quaternion.identity, size, mat, collider);
        }

        public static GameObject Box(Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector3 size, Material mat, bool collider = true)
        {
            return Make(PrimitiveType.Cube, parent, name, localPos, localRot, size, mat, collider);
        }

        /// <summary>Цилиндр: size.x/size.z — диаметр, size.y — высота.</summary>
        public static GameObject Cylinder(Transform parent, string name, Vector3 localPos, float diameter, float height, Material mat, bool collider = true)
        {
            return Make(PrimitiveType.Cylinder, parent, name, localPos, Quaternion.identity, new Vector3(diameter, height * 0.5f, diameter), mat, collider);
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 localPos, Quaternion localRot, float diameter, float height, Material mat, bool collider = true)
        {
            return Make(PrimitiveType.Cylinder, parent, name, localPos, localRot, new Vector3(diameter, height * 0.5f, diameter), mat, collider);
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 localPos, float diameter, Material mat, bool collider = true)
        {
            return Make(PrimitiveType.Sphere, parent, name, localPos, Quaternion.identity, Vector3.one * diameter, mat, collider);
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, bool collider = true)
        {
            return Make(PrimitiveType.Sphere, parent, name, localPos, Quaternion.identity, scale, mat, collider);
        }

        /// <summary>Капсула: diameter — ширина, height — полная высота.</summary>
        public static GameObject Capsule(Transform parent, string name, Vector3 localPos, float diameter, float height, Material mat, bool collider = true)
        {
            return Make(PrimitiveType.Capsule, parent, name, localPos, Quaternion.identity, new Vector3(diameter, height * 0.5f, diameter), mat, collider);
        }

        public static GameObject Make(PrimitiveType type, Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            if (mat != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static Transform Group(Transform parent, string name, Vector3 localPos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        /// <summary>Подпись в мире (TextMesh всегда смотрит в камеру через Billboard).</summary>
        public static TextMesh Label(Transform parent, string text, Vector3 localPos, Color color, float size = 0.25f)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = size * 0.1f;
            tm.fontSize = 64;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            go.AddComponent<Billboard>();
            return tm;
        }
    }
}
