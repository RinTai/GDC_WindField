using System;
using System.Collections.Generic;

using UnityEditor.VFX.Block;
using UnityEditor.VFX.UI;
using UnityEngine;
using UnityEngine.VFX;

namespace UnityEditor.VFX
{
    class VFXAttributesManager : IVFXAttributesManager
    {
        private readonly List<VFXAttribute> m_CustomAttributes = new ();

        private static readonly List<VFXAttribute> s_BuiltInAttributes = new()
        {
            VFXAttribute.Seed,
            VFXAttribute.OldPosition,
            VFXAttribute.Position,
            VFXAttribute.Velocity,
            VFXAttribute.Direction,
            VFXAttribute.Color,
            VFXAttribute.Alpha,
            VFXAttribute.Size,
            VFXAttribute.ScaleX,
            VFXAttribute.ScaleY,
            VFXAttribute.ScaleZ,
            VFXAttribute.Lifetime,
            VFXAttribute.Age,
            VFXAttribute.AngleX,
            VFXAttribute.AngleY,
            VFXAttribute.AngleZ,
            VFXAttribute.AngularVelocityX,
            VFXAttribute.AngularVelocityY,
            VFXAttribute.AngularVelocityZ,
            VFXAttribute.TexIndex,
            VFXAttribute.MeshIndex,
            VFXAttribute.PivotX,
            VFXAttribute.PivotY,
            VFXAttribute.PivotZ,
            VFXAttribute.ParticleId,
            VFXAttribute.AxisX,
            VFXAttribute.AxisY,
            VFXAttribute.AxisZ,
            VFXAttribute.Alive,
            VFXAttribute.Mass,
            VFXAttribute.TargetPosition,
            VFXAttribute.EventCount,
            VFXAttribute.SpawnTime,
            VFXAttribute.ParticleIndexInStrip,
            VFXAttribute.SpawnIndex,
            VFXAttribute.StripIndex,
            VFXAttribute.ParticleCountInStrip,
            VFXAttribute.SpawnIndexInStrip,
            VFXAttribute.SpawnCount,
			VFXAttribute.HasCollisionEvent,
			VFXAttribute.CollisionEventNormal,
			VFXAttribute.CollisionEventPosition,
            VFXAttribute.CollisionEventCount,
            // VFXAttribute.ContinuousCollisionCount,
            VFXAttribute.OldVelocity,
        };

        private static readonly List<VFXAttribute> s_ReadOnlyAttributes = new ()
        {
            VFXAttribute.Seed,
            VFXAttribute.ParticleId,
            VFXAttribute.ParticleIndexInStrip,
            VFXAttribute.SpawnTime,
            VFXAttribute.SpawnIndex,
            VFXAttribute.SpawnCount,
            VFXAttribute.StripIndex,
            VFXAttribute.ParticleCountInStrip,
            VFXAttribute.SpawnIndexInStrip,
            VFXAttribute.HasCollisionEvent,
            VFXAttribute.CollisionEventNormal,
            VFXAttribute.CollisionEventPosition,
            VFXAttribute.CollisionEventCount,
            // VFXAttribute.ContinuousCollisionCount,
            VFXAttribute.OldVelocity
        };

        private static readonly List<VFXAttribute> s_WriteOnlyAttributes = new () { VFXAttribute.EventCount };
        private static readonly List<VFXAttribute> s_LocalOnlyAttributes = new () { VFXAttribute.EventCount, VFXAttribute.ParticleIndexInStrip, VFXAttribute.StripIndex, VFXAttribute.ParticleCountInStrip, VFXAttribute.HasCollisionEvent, VFXAttribute.OldVelocity };
        private static readonly List<VFXAttribute> s_AffectingAABBAttributes = new () { VFXAttribute.Position, VFXAttribute.PivotX, VFXAttribute.PivotY, VFXAttribute.PivotZ, VFXAttribute.Size, VFXAttribute.ScaleX, VFXAttribute.ScaleY, VFXAttribute.ScaleZ, VFXAttribute.AxisX, VFXAttribute.AxisY, VFXAttribute.AxisZ, VFXAttribute.AngleX, VFXAttribute.AngleY, VFXAttribute.AngleZ, };
        private static readonly List<VFXAttribute> s_VariadicComponentsAttributes = new() { VFXAttribute.AngleX, VFXAttribute.AngleY, VFXAttribute.AngleZ, VFXAttribute.AngularVelocityX, VFXAttribute.AngularVelocityY, VFXAttribute.AngularVelocityZ, VFXAttribute.PivotX, VFXAttribute.PivotY, VFXAttribute.PivotZ, VFXAttribute.ScaleX, VFXAttribute.ScaleY, VFXAttribute.ScaleZ };
        private static readonly List<VFXAttribute> s_VariadicAttribute = new () { VFXAttribute.angle, VFXAttribute.angularVelocity, VFXAttribute.pivot, VFXAttribute.scale };

        public static VFXAttribute[] AffectingAABBAttributes => s_AffectingAABBAttributes.ToArray();

        /* To be removed when the VFXLibrary will not be static anymore */
        public static VFXAttribute FindBuiltInOnly(string name)
        {
            var attribute = s_BuiltInAttributes.Find(x => string.Compare(name, x.name, StringComparison.OrdinalIgnoreCase) == 0);
            if (!string.IsNullOrEmpty(attribute.name))
            {
                return attribute;
            }

            attribute = s_VariadicAttribute.Find(x => string.Compare(name, x.name, StringComparison.OrdinalIgnoreCase) == 0);
            if (!string.IsNullOrEmpty(attribute.name))
            {
                return attribute;
            }

            return default;
        }

        public static bool ExistsBuiltInOnly(string name)
        {
            var index = s_BuiltInAttributes.FindIndex(x => string.Compare(name, x.name, StringComparison.OrdinalIgnoreCase) == 0);
            if (index != -1)
                return true;
            return s_VariadicAttribute.FindIndex(x => string.Compare(name, x.name, StringComparison.OrdinalIgnoreCase) == 0) != -1;
        }

        public static IEnumerable<VFXAttribute> GetBuiltInAttributesOrCombination(bool includeVariadic, bool includeVariadicComponents, bool includeReadOnly, bool includeWriteOnly)
        {
            foreach (var attribute in s_BuiltInAttributes)
            {
                if (!includeVariadicComponents && s_VariadicComponentsAttributes.Contains(attribute))
                    continue;
                if (!includeReadOnly && s_ReadOnlyAttributes.Contains(attribute))
                    continue;
                if (!includeWriteOnly && s_WriteOnlyAttributes.Contains(attribute))
                    continue;

                yield return attribute;
            }

            if (includeVariadic)
            {
                foreach (var attribute in s_VariadicAttribute)
                {
                    yield return attribute;
                }
            }
        }

        public static IEnumerable<VFXAttribute> GetBuiltInAttributesAndCombination(bool includeVariadic, bool includeVariadicComponents, bool includeReadOnly, bool includeWriteOnly)
        {
            foreach (var attribute in s_BuiltInAttributes)
            {
                var isVariadicComponent = s_VariadicComponentsAttributes.Contains(attribute);
                if (includeVariadicComponents && !isVariadicComponent || !includeVariadicComponents && isVariadicComponent)
                    continue;

                var isReadOnly = s_ReadOnlyAttributes.Contains(attribute);
                if (includeReadOnly && !isReadOnly || !includeReadOnly && isReadOnly)
                    continue;

                var isWriteOnly = s_WriteOnlyAttributes.Contains(attribute);
                if (includeWriteOnly && !isWriteOnly || !includeWriteOnly && isWriteOnly)
                    continue;

                yield return attribute;
            }

            if (includeVariadic)
            {
                foreach (var attribute in s_VariadicAttribute)
                {
                    yield return attribute;
                }
            }
        }

        public static IEnumerable<string> GetBuiltInNamesOrCombination(bool includeVariadic, bool includeVariadicComponents, bool includeReadOnly, bool includeWriteOnly)
        {
            foreach (var attribute in GetBuiltInAttributesOrCombination(includeVariadic, includeVariadicComponents, includeReadOnly, includeWriteOnly))
            {
                yield return attribute.name;
            }
        }

        public static IEnumerable<string> GetBuiltInNamesAndCombination(bool includeVariadic, bool includeVariadicComponents, bool includeReadOnly, bool includeWriteOnly)
        {
            foreach (var attribute in GetBuiltInAttributesAndCombination(includeVariadic, includeVariadicComponents, includeReadOnly, includeWriteOnly))
            {
                yield return attribute.name;
            }
        }

        /****************************************************************/

        public static IEnumerable<VFXAttribute> LocalOnlyAttributes => s_LocalOnlyAttributes;

        public IEnumerable<VFXAttribute> GetAllAttributesOrCombination(bool includeVariadic, bool includeVariadicComponents, bool includeReadOnly, bool includeWriteOnly)
        {
            foreach (var attribute in GetBuiltInAttributesOrCombination(includeVariadic, includeVariadicComponents, includeReadOnly, includeWriteOnly))
            {
                yield return attribute;
            }

            foreach (var attribute in m_CustomAttributes)
            {
                yield return attribute;
            }
        }

        public IEnumerable<VFXAttribute> GetAllAttributesAndCombination(bool includeVariadic, bool includeVariadicComponents, bool includeReadOnly, bool includeWriteOnly)
        {
            foreach (var attribute in GetBuiltInAttributesAndCombination(includeVariadic, includeVariadicComponents, includeReadOnly, includeWriteOnly))
            {
                yield return attribute;
            }

            foreach (var attribute in m_CustomAttributes)
            {
                yield return attribute;
            }
        }

        public IEnumerable<string> GetAllNamesOrCombination(bool includeVariadic, bool includeVariadicComponents, bool includeReadOnly, bool includeWriteOnly)
        {
            foreach (var attribute in GetAllAttributesOrCombination(includeVariadic, includeVariadicComponents, includeReadOnly, includeWriteOnly))
            {
                yield return attribute.name;
            }
        }

        public IEnumerable<string> GetAllNamesAndCombination(bool includeVariadic, bool includeVariadicComponents, bool includeReadOnly, bool includeWriteOnly)
        {
            foreach (var attribute in GetAllAttributesAndCombination(includeVariadic, includeVariadicComponents, includeReadOnly, includeWriteOnly))
            {
                yield return attribute.name;
            }
        }

        public IEnumerable<string> GetCustomAttributeNames()
        {
            foreach (var attribute in m_CustomAttributes)
            {
                yield return attribute.name;
            }
        }

        public IEnumerable<string> GetBuiltInAndVariadicNames()
        {
            foreach (var attribute in s_BuiltInAttributes)
            {
                yield return attribute.name;
            }

            foreach (var attribute in s_VariadicAttribute)
            {
                yield return attribute.name;
            }
        }

        public IEnumerable<VFXAttribute> GetCustomAttributes()
        {
            foreach (var attribute in m_CustomAttributes)
            {
                yield return attribute;
            }
        }

        public bool TryFind(string name, out VFXAttribute attribute)
        {
            foreach (var attr in GetAllAttributesOrCombination(true, true, true, true))
            {
                if (string.Compare(attr.name, name, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    attribute = attr;
                    return true;
                }
            }

            attribute = default;
            return false;
        }

        public bool TryFindWithMode(string name, VFXAttributeMode mode, out VFXAttribute attribute)
        {
            if (TryFind(name, out attribute))
            {
                if (IsCustom(name))
                {
                    return true;
                }

                switch (mode)
                {
                    case VFXAttributeMode.Read:
                        return !s_WriteOnlyAttributes.Contains(attribute);
                    case VFXAttributeMode.Write:
                        return !s_ReadOnlyAttributes.Contains(attribute);
                    case VFXAttributeMode.ReadWrite:
                        return !s_WriteOnlyAttributes.Contains(attribute) && !s_ReadOnlyAttributes.Contains(attribute);
                    case VFXAttributeMode.ReadSource:
                        break;
                }
            }

            return false;
        }

        public bool Exist(string name)
        {
            foreach (var attribute in GetAllAttributesOrCombination(true, true, true, true))
            {
                if (string.Compare(attribute.name, name, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryUpdate(string name, CustomAttributeUtility.Signature type, string description)
        {
            var customAttribute = m_CustomAttributes.Find(x => string.Compare(x.name, name, StringComparison.OrdinalIgnoreCase) == 0);
            /*var valueType = CustomAttributeUtility.GetValueType(type);
            if (!string.IsNullOrEmpty(customAttribute.name) && (valueType != customAttribute.type || description != customAttribute.description))
            {
                m_CustomAttributes.Remove(customAttribute);
                m_CustomAttributes.Add(new VFXAttribute(name, valueType, description));

                return true;
            }*/

            return false;
        }

        public bool IsCustom(string name)
        {
            return m_CustomAttributes.FindIndex(x => string.Compare(x.name, name, StringComparison.OrdinalIgnoreCase) == 0) != -1;
        }

        public void ClearCustomAttributes()
        {
            m_CustomAttributes.Clear();
        }

        public bool TryRegisterCustomAttribute(string name, CustomAttributeUtility.Signature type, string description, out VFXAttribute newAttribute)
        {
            name = MakeValidName(name);

            newAttribute = new VFXAttribute();
            
            if (TryFind(name, out var existingAttribute))
            {
                if (existingAttribute.type == CustomAttributeUtility.GetValueType(type))
                {
                    newAttribute = existingAttribute;
                    return false;
                }
                name = FindUniqueName(name);
            }

            // newAttribute = new VFXAttribute(name, CustomAttributeUtility.GetValueType(type), description);
            // m_CustomAttributes.Add(newAttribute);
            return true;
        }

        public void UnregisterCustomAttribute(string name)
        {
            m_CustomAttributes.RemoveAll(x => string.Compare(x.name, name, StringComparison.OrdinalIgnoreCase) == 0);
        }

        public RenameStatus TryRename(string oldName, string newName)
        {
            var existingCustomAttributeIndex = m_CustomAttributes.FindIndex(x => string.Compare(x.name, oldName, StringComparison.OrdinalIgnoreCase) == 0);
            if (existingCustomAttributeIndex == -1)
            {
                return RenameStatus.NotFound;
            }

            if (ExistsBuiltInOnly(newName))
            {
                return RenameStatus.NameUsed;
            }

            var existingCustomAttribute = m_CustomAttributes[existingCustomAttributeIndex];
            var existingCustomAttributeNewNameIndex = m_CustomAttributes.FindIndex(x => string.Compare(x.name, newName, StringComparison.OrdinalIgnoreCase) == 0);
            if (existingCustomAttributeNewNameIndex != -1 && existingCustomAttributeNewNameIndex != existingCustomAttributeIndex)
            {
                return RenameStatus.NameUsed;
            }

            if (!CustomAttributeUtility.IsShaderCompilableName(newName))
            {
                Debug.LogError("Custom attribute could not be renamed, it does not start with a letter or underscore and/or contains non-alphanumeric characters. Previous name has been kept.");
                return RenameStatus.InvalidName;
            }

            m_CustomAttributes.Remove(existingCustomAttribute);
            existingCustomAttribute.Rename(newName);
            m_CustomAttributes.Add(existingCustomAttribute);
            return RenameStatus.Success;
        }

        public VFXAttribute Duplicate(string name)
        {
            if (TryFind(name, out var newAttribute))
            {
                // Do not register, let the graph do it
                newAttribute.name = FindUniqueName(newAttribute.name);
                return newAttribute;
            }

            throw new InvalidOperationException($"Trying to duplicate a custom attribute that is not found {name}");
        }

        public string FindUniqueName(string name)
        {
            var existingNames = new HashSet<string>(GetAllNamesOrCombination(true, true, true, true));
            return VFXParameterController.MakeNameUnique(name, existingNames);
        }


        private string MakeValidName(string name)
        {
            return CustomAttributeUtility.IsShaderCompilableName(name)
                ? name
                : CustomAttributeUtility.MakeShaderCompatibleName(name);
        }
    }
}
