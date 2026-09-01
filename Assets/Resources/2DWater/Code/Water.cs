using UnityEngine;

namespace Bundos.WaterSystem
{
    public class Spring
    {
        public Vector2 weightPosition, sineOffset, velocity, acceleration;
    }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Water : MonoBehaviour
    {
        [Header("3D Water Settings")]
        public float depth = 1f;

        [Header("Dynamic Wave Settings")]
        public bool interactive = true;
        public float splashInfluence = 0.005f;
        public float waveHeight = .25f;

        [Header("Constant Waves Settings")]
        public bool hasConstantWaves = true;
        public float waveAmplitude = 1f;
        public float waveSpeed = 1f;
        public int waveStep = 1;

        [Header("Spring Settings")]
        public int numSprings = 10;
        public float spacing = 1f;
        public float springConstant = 0.05f;
        public float springDamping = 0.025f;

        [Header("Particles")]
        public GameObject splashParticle;

        [HideInInspector]
        private Spring[] springs;
        private MeshFilter meshFilter;
        private Mesh mesh;

        [HideInInspector]
        public Vector3[] vertices3D;
        [HideInInspector]
        public Vector3[] baseVertices3D;
        [HideInInspector]
        public int[] triangles;
        [HideInInspector]
        private Vector2[] uvs;

        private void Start()
        {
            Initialize();
            InitializeSprings();
            CreateShape();
        }

        public void Initialize()
        {
            mesh = new Mesh()
            {
                name = "Water3DMesh"
            };

            meshFilter = GetComponent<MeshFilter>();
            meshFilter.mesh = mesh;
        }

        private void InitializeSprings()
        {
            springs = new Spring[numSprings];

            for (int i = 0; i < numSprings; i++)
            {
                springs[i] = new Spring
                {
                    weightPosition = new Vector2()
                };
            }
        }

        public void CreateShape()
        {
            if (numSprings < 2) numSprings = 2;
            if (depth <= 0.001f) depth = 1f;

            // 4 vertices per spring slice (Bottom-Front, Top-Front, Bottom-Back, Top-Back)
            vertices3D = new Vector3[numSprings * 4];
            baseVertices3D = new Vector3[numSprings * 4];

            for (int x = 0; x < numSprings; x++)
            {
                int baseIndex = x * 4;
                vertices3D[baseIndex + 0] = new Vector3(x, 0, 0);       // Bottom Front
                vertices3D[baseIndex + 1] = new Vector3(x, 1, 0);       // Top Front
                vertices3D[baseIndex + 2] = new Vector3(x, 0, depth);   // Bottom Back
                vertices3D[baseIndex + 3] = new Vector3(x, 1, depth);   // Top Back
            }

            vertices3D.CopyTo(baseVertices3D, 0);

            // Triangles for 3D box mesh:
            // (numSprings - 1) segments * 4 faces per segment (top, front, back, bottom) * 2 tris * 3 verts + 2 caps * 2 tris * 3 verts
            int segmentCount = numSprings - 1;
            int triangleCount = (segmentCount * 8 + 4) * 3;
            triangles = new int[triangleCount];

            int tris = 0;
            for (int x = 0; x < segmentCount; x++)
            {
                int A0 = x * 4 + 0;
                int A1 = x * 4 + 1;
                int A2 = x * 4 + 2;
                int A3 = x * 4 + 3;

                int B0 = (x + 1) * 4 + 0;
                int B1 = (x + 1) * 4 + 1;
                int B2 = (x + 1) * 4 + 2;
                int B3 = (x + 1) * 4 + 3;

                // 1. Top Face (Water Surface)
                triangles[tris + 0] = A1;
                triangles[tris + 1] = A3;
                triangles[tris + 2] = B3;

                triangles[tris + 3] = A1;
                triangles[tris + 4] = B3;
                triangles[tris + 5] = B1;
                tris += 6;

                // 2. Front Face
                triangles[tris + 0] = A0;
                triangles[tris + 1] = A1;
                triangles[tris + 2] = B1;

                triangles[tris + 3] = A0;
                triangles[tris + 4] = B1;
                triangles[tris + 5] = B0;
                tris += 6;

                // 3. Back Face
                triangles[tris + 0] = B2;
                triangles[tris + 1] = B3;
                triangles[tris + 2] = A3;

                triangles[tris + 3] = B2;
                triangles[tris + 4] = A3;
                triangles[tris + 5] = A2;
                tris += 6;

                // 4. Bottom Face
                triangles[tris + 0] = A0;
                triangles[tris + 1] = B0;
                triangles[tris + 2] = B2;

                triangles[tris + 3] = A0;
                triangles[tris + 4] = B2;
                triangles[tris + 5] = A2;
                tris += 6;
            }

            // 5. Left Cap (x = 0)
            triangles[tris + 0] = 0;
            triangles[tris + 1] = 2;
            triangles[tris + 2] = 3;

            triangles[tris + 3] = 0;
            triangles[tris + 4] = 3;
            triangles[tris + 5] = 1;
            tris += 6;

            // 6. Right Cap (x = numSprings - 1)
            int E0 = (numSprings - 1) * 4 + 0;
            int E1 = (numSprings - 1) * 4 + 1;
            int E2 = (numSprings - 1) * 4 + 2;
            int E3 = (numSprings - 1) * 4 + 3;

            triangles[tris + 0] = E0;
            triangles[tris + 1] = E1;
            triangles[tris + 2] = E3;

            triangles[tris + 3] = E0;
            triangles[tris + 4] = E3;
            triangles[tris + 5] = E2;

            // UVs
            uvs = new Vector2[vertices3D.Length];
            for (int x = 0; x < numSprings; x++)
            {
                float u = (float)x / (numSprings - 1);
                int baseIndex = x * 4;
                uvs[baseIndex + 0] = new Vector2(u, 0f);
                uvs[baseIndex + 1] = new Vector2(u, 1f);
                uvs[baseIndex + 2] = new Vector2(u, 0f);
                uvs[baseIndex + 3] = new Vector2(u, 1f);
            }
        }

        private void Update()
        {
            if (springs == null || springs.Length != numSprings)
            {
                InitializeSprings();
            }

            UpdateSpringPositions();
            UpdateMeshVerticePositions();
            UpdateMesh();
        }

        private void UpdateMeshVerticePositions()
        {
            for (int i = 0; i < numSprings; i++)
            {
                int topFrontIndex = (4 * i) + 1;
                int topBackIndex = (4 * i) + 3;

                float displacementY = springs[i].weightPosition.y + springs[i].sineOffset.y;

                vertices3D[topFrontIndex] = baseVertices3D[topFrontIndex] + new Vector3(0, displacementY, 0);
                vertices3D[topBackIndex] = baseVertices3D[topBackIndex] + new Vector3(0, displacementY, 0);
            }
        }

        private void UpdateSpringPositions()
        {
            for (int i = 0; i < springs.Length; i++)
            {
                springs[i].acceleration = (-springConstant * springs[i].weightPosition.y) * Vector2.up - (springs[i].velocity * springDamping);

                if (i > 0)
                {
                    float leftDelta = splashInfluence * (springs[i].acceleration.y - springs[i - 1].acceleration.y);
                    springs[i].velocity += leftDelta * Vector2.up;
                }

                if (i < springs.Length - 1)
                {
                    float rightDelta = splashInfluence * (springs[i].acceleration.y - springs[i + 1].acceleration.y);
                    springs[i].velocity += rightDelta * Vector2.up;
                }

                springs[i].velocity += springs[i].acceleration;

                if (hasConstantWaves)
                    springs[i].sineOffset = new Vector2(0, waveAmplitude * Mathf.Sin((Time.realtimeSinceStartup * waveSpeed) + i * waveStep));

                springs[i].weightPosition += springs[i].velocity;
            }
        }

        public void UpdateMesh()
        {
            if (mesh == null) return;

            mesh.Clear();
            mesh.vertices = vertices3D;
            mesh.triangles = triangles;
            mesh.uv = uvs;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        public void Ripple(Vector3 contactPoint, bool sink)
        {
            if (splashParticle != null)
            {
                Instantiate(splashParticle, contactPoint, Quaternion.identity);
            }

            Vector3 localContactPoint = transform.InverseTransformPoint(contactPoint);

            float currSmallestDistance = 10000f;
            int index = 0;
            for (int i = 0; i < numSprings; i++)
            {
                float distance = Mathf.Abs(vertices3D[(4 * i) + 1].x - localContactPoint.x);
                if (distance < currSmallestDistance)
                {
                    currSmallestDistance = distance;
                    index = i;
                }
            }

            if (index >= 0 && index < springs.Length)
            {
                springs[index].weightPosition = (sink ? Vector2.down : Vector2.up) * waveHeight;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!interactive) return;

            Rigidbody otherRigidbody = other.GetComponent<Rigidbody>();
            if (otherRigidbody != null)
            {
                Vector3 contactPoint = other.ClosestPoint(transform.position);
                Ripple(contactPoint, false);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!interactive) return;

            Rigidbody otherRigidbody = other.GetComponent<Rigidbody>();
            if (otherRigidbody != null)
            {
                Vector3 contactPoint = other.ClosestPoint(transform.position);
                Ripple(contactPoint, true);
            }
        }
    }
}