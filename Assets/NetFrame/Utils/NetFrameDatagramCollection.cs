using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetFrame.Utils
{
	public class NetFrameDatagramCollection
	{
		private readonly Dictionary<string, INetFrameDatagram> _datagrams;

		public NetFrameDatagramCollection()
		{
			_datagrams = new Dictionary<string, INetFrameDatagram>();
		}

		public void Initialize()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var implementingTypes = assembly.GetTypes()
				.Where(t => t.GetInterfaces().Contains(typeof(INetFrameDatagram)));

			foreach (var type in implementingTypes)
			{
				if (type.IsValueType)
				{
					continue;
				}
				
				_datagrams.Add(type.Name, (INetFrameDatagram) Activator.CreateInstance(type));
			}
		}

		public INetFrameDatagram GetDatagramByKey(string key)
		{
			return _datagrams[key];
		}
	}
}
