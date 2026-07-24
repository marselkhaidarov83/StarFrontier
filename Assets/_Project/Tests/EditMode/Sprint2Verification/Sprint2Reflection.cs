using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace StarFrontier.Tests.Sprint2
{
    internal static class Sprint2Reflection
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        internal static Type FindType(string simpleOrFullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type direct = assembly.GetType(simpleOrFullName, false);
                if (direct != null)
                    return direct;

                foreach (Type type in SafeGetTypes(assembly))
                {
                    if (type != null &&
                        string.Equals(
                            type.Name,
                            simpleOrFullName,
                            StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        internal static object CreateInstance(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

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

        internal static object GetRequired(
            object target,
            params string[] candidates)
        {
            MemberInfo member = FindMember(target.GetType(), candidates);

            if (member == null)
            {
                throw new MissingMemberException(
                    target.GetType().FullName,
                    string.Join("/", candidates));
            }

            return GetValue(target, member);
        }

        internal static void SetRequired(
            object target,
            object value,
            params string[] candidates)
        {
            MemberInfo member = FindMember(target.GetType(), candidates);

            if (member == null)
            {
                throw new MissingMemberException(
                    target.GetType().FullName,
                    string.Join("/", candidates));
            }

            SetValue(target, member, value);
        }

        internal static void SetCollectionRequired(
            object target,
            IReadOnlyList<object> items,
            params string[] candidates)
        {
            MemberInfo member = FindMember(target.GetType(), candidates);

            if (member == null)
            {
                throw new MissingMemberException(
                    target.GetType().FullName,
                    string.Join("/", candidates));
            }

            object current = GetValue(target, member);

            if (current is IList existingList)
            {
                existingList.Clear();
                foreach (object item in items)
                    existingList.Add(item);
                return;
            }

            Type memberType = GetMemberType(member);
            Type elementType = GetElementType(memberType) ??
                               items.FirstOrDefault()?.GetType() ??
                               typeof(object);

            object collection;

            if (memberType.IsArray)
            {
                Array array = Array.CreateInstance(elementType, items.Count);
                for (int index = 0; index < items.Count; index++)
                    array.SetValue(items[index], index);

                collection = array;
            }
            else
            {
                Type listType = typeof(List<>).MakeGenericType(elementType);
                IList list = (IList)Activator.CreateInstance(listType);

                foreach (object item in items)
                    list.Add(item);

                collection = list;
            }

            SetValue(target, member, collection);
        }

        internal static IReadOnlyList<object> GetItems(
            object target,
            params string[] candidates)
        {
            object value = GetRequired(target, candidates);

            if (value == null)
                return Array.Empty<object>();

            if (!(value is IEnumerable enumerable))
            {
                throw new InvalidOperationException(
                    $"Member '{string.Join("/", candidates)}' on " +
                    $"{target.GetType().FullName} is not enumerable.");
            }

            List<object> result = new List<object>();
            foreach (object item in enumerable)
                result.Add(item);

            return result;
        }

        internal static string ReadString(
            object target,
            params string[] candidates)
        {
            object value = GetRequired(target, candidates);
            return value as string;
        }

        internal static int ReadInt(
            object target,
            params string[] candidates)
        {
            object value = GetRequired(target, candidates);
            return Convert.ToInt32(value);
        }

        internal static bool ReadBool(
            object target,
            params string[] candidates)
        {
            object value = GetRequired(target, candidates);
            return Convert.ToBoolean(value);
        }

        internal static MemberInfo FindMember(
            Type type,
            params string[] candidates)
        {
            if (type == null)
                return null;

            List<MemberInfo> members = new List<MemberInfo>();
            members.AddRange(type.GetFields(InstanceMembers));
            members.AddRange(
                type.GetProperties(InstanceMembers)
                    .Where(property => property.GetIndexParameters().Length == 0));

            foreach (string candidate in candidates)
            {
                MemberInfo exact = members.FirstOrDefault(
                    member => string.Equals(
                        member.Name,
                        candidate,
                        StringComparison.Ordinal));

                if (exact != null)
                    return exact;
            }

            foreach (string candidate in candidates)
            {
                MemberInfo caseInsensitive = members.FirstOrDefault(
                    member => string.Equals(
                        member.Name,
                        candidate,
                        StringComparison.OrdinalIgnoreCase));

                if (caseInsensitive != null)
                    return caseInsensitive;
            }

            HashSet<string> normalized = new HashSet<string>(
                candidates.Select(Normalize),
                StringComparer.Ordinal);

            return members.FirstOrDefault(
                member => normalized.Contains(Normalize(member.Name)));
        }

        internal static string DescribeMembers(Type type)
        {
            if (type == null)
                return "<type not found>";

            return string.Join(
                ", ",
                type.GetMembers(InstanceMembers)
                    .Where(member =>
                        member.MemberType == MemberTypes.Field ||
                        member.MemberType == MemberTypes.Property)
                    .Select(member => member.Name)
                    .Distinct()
                    .OrderBy(name => name, StringComparer.Ordinal));
        }

        private static object GetValue(object target, MemberInfo member)
        {
            if (member is FieldInfo field)
                return field.GetValue(target);

            if (member is PropertyInfo property)
                return property.GetValue(target);

            throw new NotSupportedException(member.MemberType.ToString());
        }

        private static void SetValue(
            object target,
            MemberInfo member,
            object value)
        {
            Type memberType = GetMemberType(member);
            object converted = ConvertValue(value, memberType);

            if (member is FieldInfo field)
            {
                field.SetValue(target, converted);
                return;
            }

            if (member is PropertyInfo property)
            {
                if (property.CanWrite)
                {
                    property.SetValue(target, converted);
                    return;
                }

                FieldInfo backingField = target.GetType().GetField(
                    $"<{property.Name}>k__BackingField",
                    InstanceMembers);

                if (backingField != null)
                {
                    backingField.SetValue(
                        target,
                        ConvertValue(value, backingField.FieldType));
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Member '{member.Name}' on {target.GetType().FullName} is read-only.");
        }

        private static object ConvertValue(object value, Type destinationType)
        {
            if (value == null)
                return null;

            Type sourceType = value.GetType();

            if (destinationType.IsAssignableFrom(sourceType))
                return value;

            if (destinationType.IsEnum)
            {
                if (value is string text)
                    return Enum.Parse(destinationType, text, true);

                return Enum.ToObject(destinationType, value);
            }

            if (destinationType.IsArray && value is IEnumerable enumerable)
            {
                Type elementType = destinationType.GetElementType();
                List<object> items = enumerable.Cast<object>().ToList();
                Array array = Array.CreateInstance(elementType, items.Count);

                for (int index = 0; index < items.Count; index++)
                    array.SetValue(ConvertValue(items[index], elementType), index);

                return array;
            }

            if (destinationType.IsGenericType &&
                destinationType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                Type inner = Nullable.GetUnderlyingType(destinationType);
                return ConvertValue(value, inner);
            }

            return Convert.ChangeType(value, destinationType);
        }

        private static Type GetMemberType(MemberInfo member)
        {
            if (member is FieldInfo field)
                return field.FieldType;

            if (member is PropertyInfo property)
                return property.PropertyType;

            throw new NotSupportedException(member.MemberType.ToString());
        }

        private static Type GetElementType(Type collectionType)
        {
            if (collectionType.IsArray)
                return collectionType.GetElementType();

            if (collectionType.IsGenericType)
                return collectionType.GetGenericArguments().FirstOrDefault();

            Type enumerableInterface = collectionType
                .GetInterfaces()
                .FirstOrDefault(
                    type => type.IsGenericType &&
                            type.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerableInterface?.GetGenericArguments()[0];
        }

        private static string Normalize(string value)
        {
            if (value == null)
                return string.Empty;

            return new string(
                value.Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray());
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }
    }
}
