using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DE.XIDA.PLAYFAB {
	using System;
	public interface PlayFabServerListenerInterface {
		public event Action<string> OnPlayerConnectedPlayfab;
		public event Action<string> OnPlayerDisconnectedPlayfab;
		public event Action OnGameStartingPlayfab;
	}
}
