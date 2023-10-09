using System.Collections.Generic;

using Samples.Datagrams;

namespace NetFrame.Utils
{
	public class NetFrameDatagramCollection
	{
		public Dictionary<string, INetFrameDatagram> _datagrams;

		public NetFrameDatagramCollection()
		{
			_datagrams = new Dictionary<string, INetFrameDatagram>();

			_datagrams.Add("TestByteDatagram", new TestByteDatagram());
			_datagrams.Add("TestStringIntDatagram", new TestStringIntDatagram());
		}

		public INetFrameDatagram GetDatagramByKey(string key)
		{
			return _datagrams[key];
		}
	}
}
