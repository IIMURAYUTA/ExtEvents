﻿namespace ExtEvents
{
    using System;
    using JetBrains.Annotations;
    using TypeReferences;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;

    /// <summary>
    /// An argument that can be dynamic or serialized, and is configured through editor UI as a part of <see cref="ExtEvent"/>.
    /// </summary>
    [Serializable]
    public class PersistentArgument
    {
        [SerializeField] internal int _index;

        /// <summary> An index of the argument passed through ExtEvent.Invoke(). </summary>
        [PublicAPI]
        public int Index => _index;

        [SerializeField] internal bool _isSerialized;

        /// <summary> Whether the argument is serialized or dynamic. </summary>
        [PublicAPI] public bool IsSerialized => _isSerialized;

        [SerializeField] internal TypeReference _type;

        /// <summary> The type of the argument. </summary>
        [PublicAPI] public Type Type => _type;

        [SerializeField] internal string _serializedArg;
        [SerializeField] internal bool _canBeDynamic;

        private ArgumentHolder _argumentHolder;

        // NonSerialized is required here, otherwise Unity will try to serialize the field even though we didn't put SerializeField attribute.
        [NonSerialized] internal bool _initialized;

        internal unsafe void* SerializedValuePointer
        {
            get
            {
                try
                {
                    return GetArgumentHolder(_serializedArg, _type) == null ? default : _argumentHolder.ValuePointer;
                }
#pragma warning disable CS0618
                catch (ExecutionEngineException)
#pragma warning restore CS0618
                {
                    Debug.LogWarning($"Tried to invoke a method with a serialized argument of type {_type} but there was no code generated for it ahead of time.");
                    return default;
                }
            }
        }

        /// <summary> The value of the argument if it is serialized. </summary>
        /// <exception cref="Exception">The argument is not serialized but a dynamic one.</exception>
        [PublicAPI]
        public object SerializedValue
        {
            get
            {
                if (!_isSerialized)
                    throw new Exception("Tried to access a persistent value of an argument but the argument is dynamic");

                if (_type.Type == null || string.IsNullOrEmpty(_serializedArg))
                    return null;

                try
                {
                    return GetArgumentHolder(_serializedArg, _type)?.Value;
                }
#pragma warning disable CS0618
                catch (ExecutionEngineException)
#pragma warning restore CS0618
                {
                    Debug.LogWarning($"Tried to invoke a method with a serialized argument of type {_type} but there was no code generated for it ahead of time.");
                    return null;
                }
            }
        }

        /// <summary> Creates a serialized argument. </summary>
        /// <param name="value">The initial value of the serialized argument.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <returns>An instance of the serialized argument.</returns>
        public static PersistentArgument CreateSerialized<T>(T value)
        {
            return CreateSerialized(value, typeof(T));
        }

        /// <summary> Creates a serialized argument. </summary>
        /// <param name="value">The initial value of the serialized argument.</param>
        /// <param name="argumentType">The type of the value.</param>
        /// <returns>An instance of the serialized argument.</returns>
        public static PersistentArgument CreateSerialized(object value, Type argumentType)
        {
            return new PersistentArgument
            {
                _isSerialized = true,
                _type = argumentType,
                _serializedArg = SerializeValue(value, argumentType)
            };
        }

        /// <summary> Creates a dynamic argument. </summary>
        /// <param name="eventArgumentIndex">An index of the argument passed through ExtEvent.Invoke().</param>
        /// <typeparam name="T">The type of the argument.</typeparam>
        /// <returns>An instance of the dynamic argument.</returns>
        public static PersistentArgument CreateDynamic<T>(int eventArgumentIndex)
        {
            return CreateDynamic(eventArgumentIndex, typeof(T));
        }

        /// <summary> Creates a dynamic argument. </summary>
        /// <param name="eventArgumentIndex">An index of the argument passed through ExtEvent.Invoke().</param>
        /// <param name="argumentType">The type of the argument.</param>
        /// <returns>An instance of the dynamic argument.</returns>
        public static PersistentArgument CreateDynamic(int eventArgumentIndex, Type argumentType)
        {
            return new PersistentArgument
            {
                _isSerialized = false,
                _type = argumentType,
                _index = eventArgumentIndex,
                _canBeDynamic = true
            };
        }
        internal static object GetValue(string serializedArg, Type valueType)
        {
            var type = typeof(ArgumentHolder<>).MakeGenericType(valueType);
            var argumentHolder = (ArgumentHolder) JsonUtility.FromJson(serializedArg, type);
            return argumentHolder?.Value;
        }

        internal static string SerializeValue(object value, Type valueType)
        {
            var argHolderType = typeof(ArgumentHolder<>).MakeGenericType(valueType);
            var argHolder = Activator.CreateInstance(argHolderType, value);
            return JsonUtility.ToJson(argHolder);
        }

        private ArgumentHolder GetArgumentHolder(string serializedArg, Type valueType)
        {
            if (!_initialized)
            {
                // It's important to assign argumentHolder to a field so that it is not cleaned by GC until we stop using PersistentArgument.
                _initialized = true;
                var type = typeof(ArgumentHolder<>).MakeGenericType(valueType);
                _argumentHolder = (ArgumentHolder) JsonUtility.FromJson(serializedArg, type);
            }

            return _argumentHolder;
        }
    }
}