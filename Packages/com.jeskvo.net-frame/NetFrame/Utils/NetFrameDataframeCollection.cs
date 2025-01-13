using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetFrame.Utils
{
	public static class NetFrameDataframeCollection
	{
		private static readonly Dictionary<string, Func<INetworkDataframe>> Dataframes = new();

		public static void Initialize(Assembly assembly)
		{
			var implementingTypes = assembly.GetTypes()
				.Where(t => t.GetInterfaces().Contains(typeof(INetworkDataframe)));

			foreach (var type in implementingTypes)
			{
				if (!type.IsValueType)
				{
					continue;
				}
				
				Dataframes.Add(type.Name, () => (INetworkDataframe)Activator.CreateInstance(type));
			}
		}

		public static bool TryGetByKey(string key, out INetworkDataframe value)
		{
			if (!Dataframes.TryGetValue(key, out var factory))
			{
				value = default;
				return false;
			}

			value = factory.Invoke();
			return true;
		}
	}
}
