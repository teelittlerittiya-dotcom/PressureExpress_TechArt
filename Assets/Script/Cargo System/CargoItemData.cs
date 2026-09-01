using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(menuName = "Cargo/Item Data")]
public class CargoItemData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable identifier used by save/network tooling. Do not change after shipping content.")]
    public string definitionId = "cargo.prototype";
    public string cargoName;
    [TextArea]
    public string description;
    [Min(0.01f)] public float mass = 1f;
    public float price;

    [Header("2D Presentation")]
    [Min(0.01f)]
    public float cargoScale = 1f;
    public Sprite defaultSprite;

    [Header("Polish")]
    [Tooltip("Single designer-facing profile for material, FEEL, particles and spatial SFX.")]
    public CargoPolishProfile polishProfile;

    [Header("3D Collider generated from the 2D sprite")]
    public bool autoSizeColliderFromSprite = true;
    [Tooltip("Used when Auto Size Collider From Sprite is disabled.")]
    public Vector2 colliderSizeOverride = Vector2.one;
    public Vector2 colliderOffset = Vector2.zero;
    [Min(0.05f)] public float colliderDepth = 0.5f;
    [Min(0f)] public float proximityPadding = 0.75f;
    [Range(1, 128)] public int maxGeneratedColliderTriangles = 64;
    public PhysicsMaterial physicsMaterial;

    [Header("Gameplay")]
    public bool isDrainOxigen = false;
    [Header("Modules (Drag Config Assets Here)")]
    public List<CargoModule> modules = new List<CargoModule>();

    // Helper function เพื่อดึง Module ที่ต้องการ (ถ้ามี)
    public T GetModule<T>() where T : CargoModule
    {
        return modules == null ? null : modules.OfType<T>().FirstOrDefault();
    }
    
    // Helper function เพื่อดึง Module ทั้งหมด
    public List<CargoModule> GetModules()
    {
        modules ??= new List<CargoModule>();
        return modules;
    }

    public CargoModule GetModule(CargoModuleId id)
    {
        if (modules == null) return null;
        return modules.FirstOrDefault(module => CargoModuleUtility.FromModule(module) == id);
    }

    public bool ValidateDefinition(out string error)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            error = "definitionId is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(cargoName))
        {
            error = "cargoName is empty.";
            return false;
        }

        if (defaultSprite == null)
        {
            error = "defaultSprite is missing.";
            return false;
        }

        if (polishProfile == null)
        {
            error = "polishProfile is missing.";
            return false;
        }

        if (!polishProfile.ValidateProfile(out error)) return false;

        if (mass <= 0f || cargoScale <= 0f || colliderDepth <= 0f)
        {
            error = "mass, cargoScale and colliderDepth must be greater than zero.";
            return false;
        }

        if (!autoSizeColliderFromSprite && (colliderSizeOverride.x <= 0f || colliderSizeOverride.y <= 0f))
        {
            error = "manual colliderSizeOverride must be positive.";
            return false;
        }

        if (physicsMaterial == null)
        {
            error = "physicsMaterial is missing; Cargo needs an explicit material for predictable ground grip.";
            return false;
        }

        HashSet<CargoModuleId> ids = new HashSet<CargoModuleId>();
        if (modules != null)
        {
            foreach (CargoModule module in modules)
            {
                if (module == null)
                {
                    error = "modules contains a null entry.";
                    return false;
                }

                CargoModuleId id = CargoModuleUtility.FromModule(module);
                if (id == CargoModuleId.Unknown)
                {
                    error = $"unsupported module type: {module.GetType().Name}.";
                    return false;
                }

                if (!ids.Add(id))
                {
                    error = $"duplicate module id: {id}.";
                    return false;
                }

                if (module.GetMaxValue() < module.GetMinValue())
                {
                    error = $"{module.name} has max below min.";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }
}
