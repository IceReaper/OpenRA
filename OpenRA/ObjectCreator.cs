#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenRA.Primitives;

namespace OpenRA
{
	public sealed class ObjectCreator
	{
		readonly Cache<string, Type> typeCache;
		readonly Cache<Type, ConstructorInfo> ctorCache;
		readonly (Assembly Assembly, string Namespace)[] assemblies;

		public ObjectCreator()
		{
			typeCache = new Cache<string, Type>(FindType);
			ctorCache = new Cache<Type, ConstructorInfo>(GetCtor);
			assemblies = AppDomain.CurrentDomain.GetAssemblies().SelectMany(asm => asm.GetNamespaces().Select(ns => (asm, ns))).ToArray();
		}

		// Only used by the linter to prevent exceptions from being thrown during a lint run
		public static Action<string> MissingTypeAction = null;

		public T CreateObject<T>(string className)
		{
			return CreateObject<T>(className, new Dictionary<string, object>());
		}

		public T CreateObject<T>(string className, Dictionary<string, object> args)
		{
			var type = typeCache[className];
			if (type == null)
			{
				// HACK: The linter does not want to crash but only print an error instead
				if (MissingTypeAction != null)
					MissingTypeAction(className);
				else
					throw new InvalidOperationException($"Cannot locate type: {className}");

				return default;
			}

			var ctor = ctorCache[type];
			if (ctor == null)
				return (T)CreateBasic(type);
			else
				return (T)CreateUsingArgs(ctor, args);
		}

		public Type FindType(string className)
		{
			return assemblies
				.Select(pair => pair.Assembly.GetType(pair.Namespace + "." + className, false))
				.FirstOrDefault(t => t != null);
		}

		public ConstructorInfo GetCtor(Type type)
		{
			const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
			var ctors = type.GetConstructors(Flags).Where(x => x.HasAttribute<UseCtorAttribute>()).ToList();
			if (ctors.Count > 1)
				throw new InvalidOperationException("ObjectCreator: UseCtor on multiple constructors; invalid.");
			return ctors.FirstOrDefault();
		}

		public object CreateBasic(Type type)
		{
			return type.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>());
		}

		public object CreateUsingArgs(ConstructorInfo ctor, Dictionary<string, object> args)
		{
			var p = ctor.GetParameters();
			var a = new object[p.Length];
			for (var i = 0; i < p.Length; i++)
			{
				var key = p[i].Name;
				if (!args.TryGetValue(key, out var arg)) throw new InvalidOperationException($"ObjectCreator: key `{key}' not found");
				a[i] = arg;
			}

			return ctor.Invoke(a);
		}

		public IEnumerable<Type> GetTypesImplementing<T>()
		{
			var it = typeof(T);
			return GetTypes().Where(t => t != it && it.IsAssignableFrom(t));
		}

		public IEnumerable<Type> GetTypes()
		{
			return assemblies.Select(ma => ma.Assembly).Distinct()
				.SelectMany(ma => ma.GetTypes());
		}

		public TLoader GetLoader<TLoader>(string format, string name)
		{
			var loader = FindType(format + "Loader");
			if (loader == null || !loader.GetInterfaces().Contains(typeof(TLoader)))
				throw new InvalidOperationException($"Unable to find a {name} loader for type '{format}'.");

			return (TLoader)CreateBasic(loader);
		}

		public TLoader[] GetLoaders<TLoader>(IEnumerable<string> formats, string name)
		{
			var loaders = new List<TLoader>();
			foreach (var format in formats)
				loaders.Add(GetLoader<TLoader>(format, name));

			return loaders.ToArray();
		}

		[AttributeUsage(AttributeTargets.Constructor)]
		public sealed class UseCtorAttribute : Attribute { }
	}
}
