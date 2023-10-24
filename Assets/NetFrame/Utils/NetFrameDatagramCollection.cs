using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetFrame.Utils
{
	public static class NetFrameDatagramCollection
	{
		private static Dictionary<string, INetFrameDatagram> _datagrams = new();

		public static void Initialize()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var implementingTypes = assembly.GetTypes()
				.Where(t => t.GetInterfaces().Contains(typeof(INetFrameDatagram)));

			foreach (var type in implementingTypes)
			{
				if (!type.IsValueType)
				{
					continue;
				}
				
				_datagrams.Add(type.Name, (INetFrameDatagram) Activator.CreateInstance(type));
			}
		}

		public static INetFrameDatagram GetDatagramByKey(string key)
		{
			return _datagrams[key];
		}
	}
}
