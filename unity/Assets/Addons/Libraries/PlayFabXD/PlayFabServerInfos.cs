using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using PlayFab.MultiplayerModels;

namespace DE.XIDA.PLAYFAB {
	[Serializable]
	public class PlayFabServerInfos {
		public string IPV4Address;
		public List<Port> Ports;
	}
}
