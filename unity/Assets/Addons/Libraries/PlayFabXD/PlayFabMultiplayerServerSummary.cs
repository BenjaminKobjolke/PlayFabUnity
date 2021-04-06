using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DE.XIDA.PLAYFAB {
	using PlayFab.MultiplayerModels;
	using System;
	
	[Serializable]
	public class PlayFabMultiplayerServerSummary {
		public List<MultiplayerServerSummary> MultiplayerServerSummaries;
	}
}