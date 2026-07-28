#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;

internal static class SfTestReflection
{
    private static readonly Dictionary<short, OpCode> OpCodesByValue =
        typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null))
            .ToDictionary(opCode => opCode.Value);

    public static Type FindType(string simpleOrFullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type direct = assembly.GetType(simpleOrFullName, false);
            if (direct != null)
                return direct;

            Type bySimpleName = SafeGetTypes(assembly)
                .FirstOrDefault(type =>
                    string.Equals(
                        type.Name,
                        simpleOrFullName,
                        StringComparison.Ordinal));

            if (bySimpleName != null)
                return bySimpleName;
        }

        return null;
    }

    public static Type RequireType(string simpleOrFullName)
    {
        Type type = FindType(simpleOrFullName);

        Assert.That(
            type,
            Is.Not.Null,
            "Required type '" + simpleOrFullName +
            "' was not found. Create the production script and ensure Unity compiles it.");

        return type;
    }

    public static IEnumerable<Type> AllTypes()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(SafeGetTypes);
    }

    public static object CreateInstance(Type type)
    {
        Assert.That(type, Is.Not.Null);

        if (typeof(ScriptableObject).IsAssignableFrom(type))
            return ScriptableObject.CreateInstance(type);

        try
        {
            return Activator.CreateInstance(type, true);
        }
        catch
        {
#pragma warning disable SYSLIB0050
            return FormatterServices.GetUninitializedObject(type);
#pragma warning restore SYSLIB0050
        }
    }

    public static MethodInfo RequireMethod(
        Type type,
        string methodName,
        int? parameterCount = null)
    {
        MethodInfo method = type
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .FirstOrDefault(candidate =>
                candidate.Name == methodName &&
                (!parameterCount.HasValue ||
                 candidate.GetParameters().Length == parameterCount.Value));

        Assert.That(
            method,
            Is.Not.Null,
            "Method '" + type.Name + "." + methodName +
            "' with parameter count " +
            (parameterCount.HasValue ? parameterCount.Value.ToString() : "any") +
            " was not found.");

        return method;
    }

    public static MethodInfo FindMethod(
        Type type,
        string methodName,
        int? parameterCount = null)
    {
        return type
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .FirstOrDefault(candidate =>
                candidate.Name == methodName &&
                (!parameterCount.HasValue ||
                 candidate.GetParameters().Length == parameterCount.Value));
    }

    public static object Invoke(
        object instance,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = instance
            .GetType()
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .FirstOrDefault(candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == arguments.Length);

        Assert.That(
            method,
            Is.Not.Null,
            "Method '" + instance.GetType().Name + "." + methodName +
            "' with " + arguments.Length + " parameters was not found.");

        return method.Invoke(instance, arguments);
    }

    public static object GetMemberValue(object instance, params string[] names)
    {
        Assert.That(instance, Is.Not.Null);

        Type type = instance.GetType();

        foreach (string name in names)
        {
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.IgnoreCase);

            if (property != null)
                return property.GetValue(instance);

            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.IgnoreCase);

            if (field != null)
                return field.GetValue(instance);

            FieldInfo backingField = type.GetField(
                "<" + name + ">k__BackingField",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

            if (backingField != null)
                return backingField.GetValue(instance);
        }

        Assert.Fail(
            "None of the members [" +
            string.Join(", ", names) +
            "] was found on type " + type.FullName + ".");

        return null;
    }

    public static bool TryGetMemberValue(
        object instance,
        out object value,
        params string[] names)
    {
        value = null;

        if (instance == null)
            return false;

        Type type = instance.GetType();

        foreach (string name in names)
        {
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.IgnoreCase);

            if (property != null)
            {
                value = property.GetValue(instance);
                return true;
            }

            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.IgnoreCase);

            if (field != null)
            {
                value = field.GetValue(instance);
                return true;
            }

            FieldInfo backingField = type.GetField(
                "<" + name + ">k__BackingField",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

            if (backingField != null)
            {
                value = backingField.GetValue(instance);
                return true;
            }
        }

        return false;
    }

    public static void SetMemberValue(
        object instance,
        object value,
        params string[] names)
    {
        Assert.That(instance, Is.Not.Null);

        Type type = instance.GetType();

        foreach (string name in names)
        {
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.IgnoreCase);

            if (property != null && property.SetMethod != null)
            {
                property.SetValue(instance, ConvertValue(value, property.PropertyType));
                return;
            }

            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.IgnoreCase);

            if (field != null)
            {
                field.SetValue(instance, ConvertValue(value, field.FieldType));
                return;
            }

            FieldInfo backingField = type.GetField(
                "<" + name + ">k__BackingField",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

            if (backingField != null)
            {
                backingField.SetValue(
                    instance,
                    ConvertValue(value, backingField.FieldType));

                return;
            }
        }

        Assert.Fail(
            "Unable to set any member [" +
            string.Join(", ", names) +
            "] on type " + type.FullName + ".");
    }

    public static object EnumValue(
        Type enumType,
        params string[] preferredNames)
    {
        Assert.That(enumType.IsEnum, Is.True);

        foreach (string name in preferredNames)
        {
            if (Enum.GetNames(enumType)
                .Any(item => string.Equals(
                    item,
                    name,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return Enum.Parse(enumType, name, true);
            }
        }

        Array values = Enum.GetValues(enumType);

        foreach (object value in values)
        {
            if (Convert.ToInt64(value) != 0)
                return value;
        }

        return values.GetValue(0);
    }

    public static void AssertEnumContains(
        Type enumType,
        params string[] requiredNames)
    {
        string[] names = Enum.GetNames(enumType);

        foreach (string requiredName in requiredNames)
        {
            Assert.That(
                names.Any(name =>
                    string.Equals(
                        name,
                        requiredName,
                        StringComparison.Ordinal)),
                Is.True,
                "Enum " + enumType.Name +
                " must contain value " + requiredName + ".");
        }
    }

    public static bool MethodReferences(
      MethodInfo sourceMethod,
      string targetTypeName,
      string targetMethodName = null)
    {
        return GetReferencedMethods(
                sourceMethod)
            .Any(reference =>
            {
                if (reference == null)
                    return false;

                bool methodNameMatches =
                    targetMethodName == null ||
                    reference.Name ==
                    targetMethodName;

                if (!methodNameMatches)
                    return false;

                /*
                 * Обычный вызов метода или конструктора:
                 *
                 * new FuelChangedEvent(...)
                 * IRefuelService.Consume(...)
                 */
                bool declaringTypeMatches =
                    reference.DeclaringType != null &&
                    reference.DeclaringType.Name ==
                    targetTypeName;

                if (declaringTypeMatches)
                    return true;

                /*
                 * Generic-вызов:
                 *
                 * eventBus.Publish<SaveNeedEvent>(...)
                 *
                 * У пустого события компилятор может
                 * не оставить отдельный вызов конструктора.
                 * Тогда тип события находится только
                 * в generic-аргументе метода Publish.
                 */
                if (reference is MethodInfo
                    referencedMethod &&
                    referencedMethod.IsGenericMethod)
                {
                    Type[] genericArguments =
                        referencedMethod
                            .GetGenericArguments();

                    return genericArguments.Any(
                        argument =>
                            argument != null &&
                            argument.Name ==
                            targetTypeName);
                }

                return false;
            });
    }

    public static IReadOnlyList<MethodBase> GetReferencedMethods(
        MethodInfo method)
    {
        List<MethodBase> result = new List<MethodBase>();

        MethodBody body = method.GetMethodBody();
        if (body == null)
            return result;

        byte[] il = body.GetILAsByteArray();
        if (il == null)
            return result;

        Type[] typeArguments =
            method.DeclaringType != null && method.DeclaringType.IsGenericType
                ? method.DeclaringType.GetGenericArguments()
                : Type.EmptyTypes;

        Type[] methodArguments =
            method.IsGenericMethod
                ? method.GetGenericArguments()
                : Type.EmptyTypes;

        int index = 0;

        while (index < il.Length)
        {
            OpCode opCode;

            byte first = il[index++];

            if (first == 0xFE)
            {
                short key = (short)(0xFE00 | il[index++]);
                opCode = OpCodesByValue[key];
            }
            else
            {
                opCode = OpCodesByValue[first];
            }

            switch (opCode.OperandType)
            {
                case OperandType.InlineMethod:
                    {
                        int token = BitConverter.ToInt32(il, index);
                        index += 4;

                        try
                        {
                            MethodBase referenced =
                                method.Module.ResolveMethod(
                                    token,
                                    typeArguments,
                                    methodArguments);

                            if (referenced != null)
                                result.Add(referenced);
                        }
                        catch
                        {
                            // A failed token resolution should not break the test helper.
                        }

                        break;
                    }

                case OperandType.InlineSwitch:
                    {
                        int count = BitConverter.ToInt32(il, index);
                        index += 4 + count * 4;
                        break;
                    }

                case OperandType.InlineI8:
                case OperandType.InlineR:
                    index += 8;
                    break;

                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    index += 4;
                    break;

                case OperandType.InlineVar:
                    index += 2;
                    break;

                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    index += 1;
                    break;

                case OperandType.InlineNone:
                    break;

                default:
                    throw new NotSupportedException(
                        "Unsupported IL operand type: " +
                        opCode.OperandType);
            }
        }

        return result;
    }

    public static bool TypeHasDirectMemberOfType(
        Type ownerType,
        Type targetType)
    {
        bool fieldMatch = ownerType
            .GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Any(field => field.FieldType == targetType);

        bool propertyMatch = ownerType
            .GetProperties(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Any(property => property.PropertyType == targetType);

        return fieldMatch || propertyMatch;
    }

    public static float ToFloat(object value)
    {
        return Convert.ToSingle(value);
    }

    public static int ToInt(object value)
    {
        return Convert.ToInt32(value);
    }

    private static object ConvertValue(object value, Type destinationType)
    {
        if (value == null)
            return null;

        if (destinationType.IsInstanceOfType(value))
            return value;

        if (destinationType.IsEnum)
            return Enum.ToObject(destinationType, value);

        return Convert.ChangeType(value, destinationType);
    }

    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types
                .Where(type => type != null)
                .ToArray();
        }
    }
}

#endif
